import { describe, expect, it } from 'vitest';
import { api, ApiError } from '@/lib/api/http';
import type { ClientSummary, Paged, Program, Workout } from '@/lib/api/types';
import { CLIENT_EMAIL, signIn } from '@/test/utils';
import { db } from './db';

const fail = (p: Promise<unknown>) => p.then(() => { throw new Error('expected the request to fail'); }, (e: unknown) => e as ApiError);
const userId = (email: string) => db.state.users.find((u) => u.email === email)!.id;
const workoutId = (name: string) => db.state.workouts.find((w) => w.name === name)!.id;

describe('mock API, contract v1.6/v1.7', () => {
  it('rejects a client session id that another client already owns (Session.IdInUse)', async () => {
    const other = db.state.sessions.find((s) => s.clientId !== userId(CLIENT_EMAIL))!;
    await signIn(CLIENT_EMAIL);
    const err = await fail(api('/sessions', { method: 'POST', body: { id: other.id, workoutName: 'Ad hoc' } }));
    expect(err).toMatchObject({ status: 409, code: 'Session.IdInUse' });

    const mine = await api<{ id: string }>('/sessions', { method: 'POST', body: { workoutName: 'Ad hoc' } });
    const again = await api<{ id: string }>('/sessions', { method: 'POST', body: { id: mine.id, workoutName: 'Ad hoc' } });
    expect(again.id).toBe(mine.id);
  });

  it('rejects a set id that belongs to another session (Set.IdInUse) and unknown exercises', async () => {
    const taken = db.state.sessions.find((s) => s.sets.length > 0)!.sets[0]!;
    await signIn(CLIENT_EMAIL);
    const s = await api<{ id: string }>('/sessions', { method: 'POST', body: { workoutName: 'Ad hoc' } });
    const body = { exerciseId: taken.exerciseId, position: 1, setNumber: 1, completed: true };
    const err = await fail(api('/sessions/' + s.id + '/sets/' + taken.id, { method: 'PUT', body }));
    expect(err).toMatchObject({ status: 409, code: 'Set.IdInUse' });

    const unknown = await fail(api('/sessions/' + s.id + '/sets/' + crypto.randomUUID(), { method: 'PUT', body: { ...body, exerciseId: crypto.randomUUID() } }));
    expect(unknown.status).toBe(400);
    expect(Object.keys(unknown.problem.errors ?? {})).toEqual(['exerciseId']);
  });

  it('caps program days at 3 slots (422) and keys bad workout refs by slot index (400)', async () => {
    await signIn();
    const w = workoutId('Upper A');
    const crowded = { name: 'Crowded', description: null, weeks: 1, slots: [1, 2, 3, 4].map(() => ({ week: 1, day: 2, workoutId: w })) };
    const tooMany = await fail(api('/programs', { method: 'POST', body: crowded }));
    expect(tooMany).toMatchObject({ status: 422, code: 'Program.TooManySlots' });
    expect(tooMany.problem.errors).toBeUndefined();

    const archived = db.state.workouts.find((x) => x.name === 'Lower B')!;
    archived.archived = true;
    const bad = await fail(
      api('/programs', { method: 'POST', body: { name: 'Bad refs', description: null, weeks: 1, slots: [{ week: 1, day: 1, workoutId: w }, { week: 1, day: 2, workoutId: crypto.randomUUID() }, { week: 1, day: 3, workoutId: archived.id }] } }),
    );
    expect(bad).toMatchObject({ status: 400, code: 'Validation' });
    expect(Object.keys(bad.problem.errors ?? {}).sort()).toEqual(['slots[1].workoutId', 'slots[2].workoutId']);
  });

  it('keeps an archived workout a program already uses, but not a new reference to it', async () => {
    await signIn();
    const p = db.state.programs.find((x) => x.trainerId === userId('coach@trainersphere.dev'))!;
    const used = db.state.workouts.find((w) => w.id === p.slots[0]!.workoutId)!;
    used.archived = true;
    const slots = p.slots.map(({ week, day, workoutId }) => ({ week, day, workoutId }));
    const saved = await api<Program>('/programs/' + p.id, { method: 'PUT', body: { name: p.name, description: p.description, weeks: p.weeks, slots } });
    expect(saved.slots.some((s) => s.workoutId === used.id)).toBe(true);

    const err = await fail(api('/programs', { method: 'POST', body: { name: 'New block', description: null, weeks: 1, slots: [{ week: 1, day: 1, workoutId: used.id }] } }));
    expect(err.problem.errors?.['slots[0].workoutId']).toEqual([`The workout '${used.id}' is archived.`]);
  });

  it('reports workout exercise refs per item: unknown 404, newly archived 422, already used archived ok', async () => {
    await signIn();
    const w = db.state.workouts.find((x) => x.name === 'Lower A')!;
    const lines = w.exercises.map((e) => ({ exerciseId: e.exerciseId, sets: e.sets, goal: e.goal, load: e.load, restSeconds: e.restSeconds, tempo: e.tempo, supersetGroup: e.supersetGroup, notes: e.notes }));
    const used = db.state.exercises.find((e) => e.id === lines[0]!.exerciseId)!;
    used.archived = true;
    const kept = await api<Workout>('/workouts/' + w.id, { method: 'PUT', body: { name: w.name, description: w.description, exercises: lines } });
    expect(kept.exercises[0]!.exerciseId).toBe(used.id);

    const unknown = await fail(api('/workouts', { method: 'POST', body: { name: 'Unknown ref', exercises: [{ ...lines[1]!, exerciseId: crypto.randomUUID() }] } }));
    expect(unknown).toMatchObject({ status: 404, code: 'Exercise.NotFound' });
    expect(Object.keys(unknown.problem.errors ?? {})).toEqual(['exercises[0].exerciseId']);

    const fresh = await fail(api('/workouts', { method: 'POST', body: { name: 'Archived ref', exercises: [lines[1]!, lines[0]!] } }));
    expect(fresh).toMatchObject({ status: 422, code: 'Exercise.Archived' });
    expect(Object.keys(fresh.problem.errors ?? {})).toEqual(['exercises[1].exerciseId']);
  });

  it('carries the session id on Assignment.NotScheduled whenever a session exists', async () => {
    const a = db.state.assignments.find((x) => x.status === 'scheduled' && !x.sessionId && x.clientId === userId(CLIENT_EMAIL) && x.date > db.state.seededOn)!;
    await signIn(CLIENT_EMAIL);
    const s = await api<{ id: string }>('/sessions', { method: 'POST', body: { assignmentId: a.id } });
    await signIn();
    for (const req of [api('/assignments/' + a.id, { method: 'DELETE' }), api('/assignments/' + a.id + '/skip', { method: 'POST', body: {} })]) {
      const err = await fail(req);
      expect(err).toMatchObject({ status: 409, code: 'Assignment.NotScheduled' });
      expect(err.problem.sessionId).toBe(s.id);
    }

    const done = db.state.assignments.find((x) => x.status === 'completed' && x.sessionId)!;
    for (const req of [api('/assignments/' + done.id, { method: 'DELETE' }), api('/assignments/' + done.id, { method: 'PUT', body: { date: done.date, notes: null } })]) {
      const err = await fail(req);
      expect(err.code).toBe('Assignment.NotScheduled');
      expect(err.problem.sessionId).toBe(done.sessionId);
    }
    const skipped = db.state.assignments.find((x) => x.status === 'skipped' && !x.sessionId);
    if (skipped) expect((await fail(api('/assignments/' + skipped.id, { method: 'DELETE' }))).problem.sessionId).toBeUndefined();
  });

  it('sorts clients by every documented value, nulls last', async () => {
    await signIn();
    const sorts = ['name', '-name', 'lastSessionAt', '-lastSessionAt', 'adherence7d', '-adherence7d'] as const;
    for (const sort of sorts) {
      const page = await api<Paged<ClientSummary>>('/clients', { query: { sort } });
      const field = sort.replace('-', '') as 'name' | 'lastSessionAt' | 'adherence7d';
      if (field === 'name') {
        const names = page.items.map((c) => c.firstName);
        expect(names).toEqual([...names].sort((x, y) => (sort === 'name' ? x.localeCompare(y) : y.localeCompare(x))));
        continue;
      }
      const values = page.items.map((c) => c[field]);
      const firstNull = values.indexOf(null);
      if (firstNull >= 0) expect(values.slice(firstNull).every((v) => v === null)).toBe(true);
    }
    const bad = await fail(api('/clients', { query: { sort: 'adherence28d' } }));
    expect(bad.problem.errors?.sort?.[0]).toMatch(/^Must be one of: name, -name, lastSessionAt/);
  });

  it('rounds rates to 2 decimals or null', async () => {
    await signIn();
    const page = await api<Paged<ClientSummary>>('/clients');
    for (const c of page.items) {
      for (const r of [c.adherence7d, c.adherence28d]) if (r !== null) expect(Math.round(r * 100) / 100).toBe(r);
    }
  });
});
