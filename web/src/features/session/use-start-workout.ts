import { useMutationState, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { queries } from '@/lib/api/queries';
import type { Assignment } from '@/lib/api/types';
import { useUser } from '@/lib/auth/hooks';
import { uuid } from '@/lib/utils';
import { optimisticAdHocSession, optimisticSession, runSessionWrite, sessionKeys, type StartVars } from './offline';

const ignore = () => undefined;

/**
 * Starts a workout locally first: the session gets a client-generated id, its stand-in goes into
 * the cache and the start request is queued, so logging begins immediately even offline.
 */
export function useStartWorkout() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const user = useUser();

  const open = (sessionId: string) => navigate({ to: '/log/$sessionId', params: { sessionId } });

  const fromAssignment = (a: Assignment, localSessionId?: string) => {
    const existing = a.sessionId ?? localSessionId;
    if (existing) return open(existing);
    const id = uuid();
    const startedAt = new Date().toISOString();
    qc.setQueryData(queries.session(id).queryKey, optimisticSession(id, a, startedAt));
    runSessionWrite(qc, sessionKeys.start, id, { id, assignmentId: a.id, startedAt } satisfies StartVars).catch(ignore);
    return open(id);
  };

  const adHoc = (workoutName: string) => {
    const id = uuid();
    const startedAt = new Date().toISOString();
    qc.setQueryData(queries.session(id).queryKey, optimisticAdHocSession(id, user.id, workoutName, startedAt));
    runSessionWrite(qc, sessionKeys.start, id, { id, workoutName, startedAt } satisfies StartVars).catch(ignore);
    return open(id);
  };

  return { fromAssignment, adHoc, open };
}

/** Starts still queued on this device (offline), so the dashboard can offer Resume instead of a second start. */
export function useLocalStarts(): { byAssignment: Map<string, string>; adHoc: string[] } {
  const starts = useMutationState({
    filters: { mutationKey: sessionKeys.start, status: 'pending' },
    select: (m) => m.state.variables as StartVars | undefined,
  });
  const byAssignment = new Map<string, string>();
  const adHoc: string[] = [];
  for (const v of starts) {
    if (!v) continue;
    if (v.assignmentId) byAssignment.set(v.assignmentId, v.id);
    else adHoc.push(v.id);
  }
  return { byAssignment, adHoc };
}
