import type { QueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { sessionsApi } from '@/lib/api/endpoints';
import { ApiError, NetworkError, errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { Assignment, PutSetRequest, Session, SessionSet, StartSessionRequest } from '@/lib/api/types';
import { uuid } from '@/lib/utils';
import { aliasId, originalId, resolveId } from './id-aliases';

export const sessionKeys = {
  start: ['session', 'start'] as const,
  putSet: ['session', 'put-set'] as const,
  deleteSet: ['session', 'delete-set'] as const,
  complete: ['session', 'complete'] as const,
};

export type StartVars = StartSessionRequest & { id: string };

export interface PutSetVars {
  sessionId: string;
  setId: string;
  body: PutSetRequest;
}
export interface DeleteSetVars {
  sessionId: string;
  setId: string;
}
export interface CompleteVars {
  sessionId: string;
  completedAt: string;
  notes: string | null;
}

type SessionKey = (typeof sessionKeys)[keyof typeof sessionKeys];

/**
 * Runs a session write through the mutation defaults below. Every write for one session shares a
 * scope, so queued writes replay in order after reconnecting: start, then sets, then complete.
 * Returns the mutation promise; callers that don't await it must not let it reject unhandled.
 */
export function runSessionWrite(qc: QueryClient, key: SessionKey, sessionId: string, variables: StartVars | PutSetVars | DeleteSetVars | CompleteVars): Promise<unknown> {
  const mutation = qc.getMutationCache().build<unknown, unknown, unknown, unknown>(qc, { mutationKey: key, scope: { id: `session:${originalId(sessionId)}` } });
  return mutation.execute(variables);
}

/** Network failures retry until the write lands; the API makes every session write idempotent. */
export function retryWrite(failureCount: number, error: unknown): boolean {
  if (error instanceof NetworkError) return true;
  if (error instanceof ApiError) return error.status >= 500 && failureCount < 3;
  return false;
}

const retryDelay = (attempt: number) => Math.min(30_000, 1000 * 2 ** attempt);

/**
 * Ids made on this device can collide with an id another session already owns (`Session.IdInUse`,
 * `Set.IdInUse`). That is not the user's mistake, so the write moves to a fresh id once and
 * the alias makes later writes and the open page follow it.
 */
export async function withFreshIdOnCollision<R>(code: string, id: string, run: (id: string) => Promise<R>, onAlias: (from: string, to: string) => void): Promise<R> {
  const current = resolveId(id);
  try {
    return await run(current);
  } catch (err) {
    if (!(err instanceof ApiError && err.code === code)) throw err;
    const fresh = uuid();
    aliasId(id, fresh);
    onAlias(current, fresh);
    return run(fresh);
  }
}

function renameSet(session: Session, from: string, to: string): Session {
  return { ...session, exercises: session.exercises.map((e) => ({ ...e, sets: e.sets.map((s) => (s.id === from ? { ...s, id: to } : s)) })) };
}

export function upsertSet(session: Session, set: SessionSet): Session {
  return {
    ...session,
    exercises: session.exercises.map((e) => {
      if (e.position !== set.position) return e;
      const others = e.sets.filter((s) => s.id !== set.id);
      return { ...e, sets: [...others, set].sort((a, b) => a.setNumber - b.setNumber) };
    }),
  };
}

/**
 * Server copy plus what exists only on this device: sets logged while the start request was
 * queued, and exercises added mid-session that have no sets on the server yet.
 */
export function mergeLocal(server: Session, local: Session): Session {
  const known = new Set(server.exercises.map((e) => e.position));
  const extra = local.exercises.filter((e) => !known.has(e.position)).map((e) => ({ ...e, sets: [] }));
  const withExtra = { ...server, exercises: [...server.exercises, ...extra].sort((a, b) => a.position - b.position) };
  return local.exercises.flatMap((e) => e.sets).reduce(upsertSet, withExtra);
}

export function removeSet(session: Session, setId: string): Session {
  return { ...session, exercises: session.exercises.map((e) => ({ ...e, sets: e.sets.filter((s) => s.id !== setId) })) };
}

/** Local stand-in for a session the server has not confirmed yet, so logging works offline. */
export function optimisticSession(id: string, assignment: Assignment, startedAt: string): Session {
  return {
    id,
    clientId: assignment.clientId,
    assignmentId: assignment.id,
    workoutName: assignment.workout.name,
    status: 'in-progress',
    startedAt,
    completedAt: null,
    notes: null,
    exercises: assignment.workout.exercises.map((e) => ({
      position: e.position,
      exerciseId: e.exerciseId,
      exercise: e.exercise,
      prescription: {
        sets: e.sets,
        goal: e.goal,
        load: e.load,
        restSeconds: e.restSeconds,
        tempo: e.tempo,
        supersetGroup: e.supersetGroup,
        notes: e.notes,
      },
      lastPerformance: null,
      sets: [],
    })),
    summary: { totalVolumeKg: 0, completedSets: 0, totalReps: 0, durationSeconds: null, personalRecords: [] },
  };
}

export function optimisticAdHocSession(id: string, clientId: string, workoutName: string, startedAt: string): Session {
  return {
    id,
    clientId,
    assignmentId: null,
    workoutName,
    status: 'in-progress',
    startedAt,
    completedAt: null,
    notes: null,
    exercises: [],
    summary: { totalVolumeKg: 0, completedSets: 0, totalReps: 0, durationSeconds: null, personalRecords: [] },
  };
}

/**
 * Mutation functions are registered by key (not inline in components) so that writes queued
 * offline and persisted to storage can resume after a reload, when no component is mounted.
 */
export function registerMutationDefaults(qc: QueryClient) {
  const detailKey = (id: string) => queries.session(resolveId(id)).queryKey;
  const moveSession = (from: string, to: string) => {
    const local = qc.getQueryData<Session>(queries.session(from).queryKey);
    if (local) qc.setQueryData(queries.session(to).queryKey, { ...local, id: to });
  };

  qc.setMutationDefaults(sessionKeys.start, {
    mutationFn: (body: StartVars) => withFreshIdOnCollision('Session.IdInUse', body.id, (id) => sessionsApi.start({ ...body, id }), moveSession),
    retry: retryWrite,
    retryDelay,
    onSuccess: (session: Session) => {
      const local = qc.getQueryData<Session>(detailKey(session.id));
      qc.setQueryData(detailKey(session.id), local ? mergeLocal(session, local) : session);
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      void qc.invalidateQueries({ queryKey: keys.assignments });
    },
  });

  qc.setMutationDefaults(sessionKeys.putSet, {
    mutationFn: ({ sessionId, setId, body }: PutSetVars) =>
      withFreshIdOnCollision('Set.IdInUse', setId, (id) => sessionsApi.putSet(resolveId(sessionId), id, body), (from, to) => {
        const s = qc.getQueryData<Session>(detailKey(sessionId));
        if (s) qc.setQueryData(detailKey(sessionId), renameSet(s, from, to));
      }),
    retry: retryWrite,
    retryDelay,
    onMutate: async ({ sessionId, setId, body }: PutSetVars) => {
      await qc.cancelQueries({ queryKey: detailKey(sessionId) });
      const prev = qc.getQueryData<Session>(detailKey(sessionId));
      if (prev) qc.setQueryData(detailKey(sessionId), upsertSet(prev, { id: resolveId(setId), ...body, updatedAt: new Date().toISOString() }));
      return { prev };
    },
    onError: (err, { sessionId }: PutSetVars, ctx) => {
      const prev = (ctx as { prev?: Session } | undefined)?.prev;
      if (prev) qc.setQueryData(detailKey(sessionId), prev);
      toast.error(`Set not saved: ${errorMessage(err)}`);
    },
  });

  qc.setMutationDefaults(sessionKeys.deleteSet, {
    mutationFn: ({ sessionId, setId }: DeleteSetVars) => sessionsApi.deleteSet(resolveId(sessionId), resolveId(setId)),
    retry: retryWrite,
    retryDelay,
    onMutate: async ({ sessionId, setId }: DeleteSetVars) => {
      await qc.cancelQueries({ queryKey: detailKey(sessionId) });
      const prev = qc.getQueryData<Session>(detailKey(sessionId));
      if (prev) qc.setQueryData(detailKey(sessionId), removeSet(prev, resolveId(setId)));
      return { prev };
    },
    onError: (err, { sessionId }: DeleteSetVars, ctx) => {
      const prev = (ctx as { prev?: Session } | undefined)?.prev;
      if (prev) qc.setQueryData(detailKey(sessionId), prev);
      toast.error(`Set not removed: ${errorMessage(err)}`);
    },
  });

  qc.setMutationDefaults(sessionKeys.complete, {
    mutationFn: ({ sessionId, completedAt, notes }: CompleteVars) => sessionsApi.complete(resolveId(sessionId), { completedAt, notes }),
    retry: retryWrite,
    retryDelay,
    onMutate: ({ sessionId, completedAt, notes }: CompleteVars) => {
      const prev = qc.getQueryData<Session>(detailKey(sessionId));
      if (prev) qc.setQueryData(detailKey(sessionId), { ...prev, status: 'completed', completedAt, notes });
      return { prev };
    },
    onError: (err, { sessionId }: CompleteVars, ctx) => {
      const prev = (ctx as { prev?: Session } | undefined)?.prev;
      if (prev) qc.setQueryData(detailKey(sessionId), prev);
      toast.error(`Workout not finished: ${errorMessage(err)}`);
    },
    onSuccess: (session: Session) => {
      qc.setQueryData(detailKey(session.id), session);
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      void qc.invalidateQueries({ queryKey: keys.sessions, predicate: (q) => q.queryKey[1] === 'list' });
      void qc.invalidateQueries({ queryKey: keys.assignments });
      void qc.invalidateQueries({ queryKey: keys.clients });
    },
  });
}
