import { onlineManager } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { assignmentsApi, sessionsApi } from '@/lib/api/endpoints';
import { queries } from '@/lib/api/queries';
import type { Session } from '@/lib/api/types';
import { todayIso } from '@/lib/dates';
import { formatDuration } from '@/lib/domain';
import { db } from '@/mocks/db';
import { CLIENT_EMAIL, findHeading, renderApp, signIn } from '@/test/utils';
import { findDialog } from '@/test/trainer';
import { optimisticSession, runSessionWrite, sessionKeys } from './offline';

function todaysAssignment() {
  const client = db.state.users.find((u) => u.email === CLIENT_EMAIL)!;
  return db.state.assignments.find((a) => a.clientId === client.id && a.date === todayIso())!;
}

async function openScheduledWorkout() {
  const assignment = todaysAssignment();
  await signIn(CLIENT_EMAIL);
  const app = renderApp('/');
  await app.user.click(await screen.findByRole('button', { name: 'Start workout' }));
  await findHeading(assignment.snapshot.name);
  await waitFor(() => expect(app.router.state.location.pathname).toMatch(/^\/log\/.+/));
  const sessionId = app.router.state.location.pathname.split('/').at(-1)!;
  await waitFor(() => expect(db.state.sessions.some((s) => s.id === sessionId)).toBe(true));
  return { ...app, assignment, sessionId };
}

describe('live session logging', () => {
  it('logs a set in one tap, persists it and adjusts or skips the rest timer', async () => {
    const { user, sessionId, assignment } = await openScheduledWorkout();
    await user.click(screen.getByRole('button', { name: 'Set 1 done' }));

    await waitFor(() => expect(db.state.sessions.find((s) => s.id === sessionId)?.sets.some((x) => x.position === 1 && x.setNumber === 1 && x.completed)).toBe(true));
    expect(screen.getByRole('button', { name: 'Set 1 done' })).toHaveAttribute('aria-pressed', 'true');
    const rest = assignment.snapshot.exercises[0]!.restSeconds;
    const timer = screen.getByRole('timer', { name: 'Rest remaining' });
    expect(timer).toHaveTextContent(formatDuration(rest));
    await user.click(screen.getByRole('button', { name: '15 seconds more rest' }));
    expect(timer).toHaveTextContent(formatDuration(rest + 15));
    await user.click(screen.getByRole('button', { name: '15 seconds less rest' }));
    expect(timer).toHaveTextContent(formatDuration(rest));
    await user.click(screen.getByRole('button', { name: 'Skip rest' }));
    expect(screen.queryByRole('timer', { name: 'Rest remaining' })).not.toBeInTheDocument();
  });

  it('debounces edits to a logged set and saves the new values', async () => {
    const { user, sessionId } = await openScheduledWorkout();
    await user.click(screen.getByRole('button', { name: 'Set 1 done' }));
    await waitFor(() => expect(db.state.sessions.find((s) => s.id === sessionId)?.sets).toHaveLength(1));
    await user.click(screen.getByRole('button', { name: 'Skip rest' }));
    await user.click(screen.getByRole('button', { name: /^Edit set 1:/ }));
    const weight = screen.getByRole('textbox', { name: 'Set 1 weight (kg)' });
    await user.clear(weight);
    await user.type(weight, '65');
    const reps = screen.getByRole('textbox', { name: 'Set 1 reps' });
    await user.clear(reps);
    await user.type(reps, '6');
    await user.tab();

    await waitFor(() => {
      const set = db.state.sessions.find((s) => s.id === sessionId)?.sets[0];
      expect(set).toMatchObject({ weightKg: 65, reps: 6, completed: true });
    }, { timeout: 2500 });
  });

  it('finishes with notes and shows the completion summary', async () => {
    const { user, sessionId, assignment } = await openScheduledWorkout();
    await user.click(screen.getByRole('button', { name: 'Set 1 done' }));
    await user.click(screen.getByRole('button', { name: 'Skip rest' }));
    await user.click(screen.getByRole('button', { name: 'Finish' }));
    const dialog = await findDialog('Finish workout?');
    const planned = assignment.snapshot.exercises.reduce((sum, e) => sum + e.sets, 0);
    expect(dialog.getByText(`1 of ${planned}`)).toBeInTheDocument();
    await user.type(dialog.getByLabelText(/Notes/), 'Felt strong');
    await user.click(dialog.getByRole('button', { name: 'Finish workout' }));

    await findHeading('Workout complete');
    expect(screen.getByText('Sets').parentElement).toHaveTextContent('1');
    await waitFor(() => expect(db.state.sessions.find((s) => s.id === sessionId)).toMatchObject({ status: 'completed', notes: 'Felt strong' }));
    expect(screen.getByRole('link', { name: 'View workout details' })).toHaveAttribute('href', `/sessions/${sessionId}`);
  });

  it('discards an in-progress workout after confirmation', async () => {
    const { user, router, assignment, sessionId } = await openScheduledWorkout();
    await user.click(screen.getByRole('button', { name: 'Workout options' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Discard workout' }));
    const dialog = await findDialog('Discard this workout?');
    await user.click(dialog.getByRole('button', { name: 'Discard workout' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(db.state.sessions.some((s) => s.id === sessionId)).toBe(false);
    expect(assignment.sessionId).toBeNull();
  });

  it('offers the existing session when the assignment was already started elsewhere', async () => {
    const assignment = todaysAssignment();
    await signIn(CLIENT_EMAIL);
    const existing = await sessionsApi.start({ assignmentId: assignment.id });
    const fullAssignment = await assignmentsApi.get(assignment.id);
    const attemptedId = crypto.randomUUID();
    const { user, router, queryClient } = renderApp(`/log/${attemptedId}`);
    queryClient.setQueryData(queries.session(attemptedId).queryKey, optimisticSession(attemptedId, fullAssignment, new Date().toISOString()));
    void runSessionWrite(queryClient, sessionKeys.start, attemptedId, { id: attemptedId, assignmentId: assignment.id }).catch(() => undefined);

    expect(await screen.findByText('This workout was already started')).toBeInTheDocument();
    const open = screen.getByRole('link', { name: 'Open that workout' });
    expect(open).toHaveAttribute('href', `/log/${existing.id}`);
    await user.click(open);
    await waitFor(() => expect(router.state.location.pathname).toBe(`/log/${existing.id}`));
    await findHeading(assignment.snapshot.name);
  });

  it('keeps a set done offline and replays the write after reconnecting', async () => {
    const { user, sessionId } = await openScheduledWorkout();
    onlineManager.setOnline(false);
    try {
      await user.click(screen.getByRole('button', { name: 'Set 1 done' }));
      expect(await screen.findByText('Offline · 1 change pending')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Set 1 done' })).toHaveAttribute('aria-pressed', 'true');
      expect(db.state.sessions.find((s) => s.id === sessionId)?.sets).toHaveLength(0);

      onlineManager.setOnline(true);
      await waitFor(() => expect(db.state.sessions.find((s) => s.id === sessionId)?.sets.some((x) => x.completed)).toBe(true), { timeout: 3000 });
    } finally {
      onlineManager.setOnline(true);
    }
  });

  it('moves a start to a fresh id when another client already owns the generated one', async () => {
    const assignment = todaysAssignment();
    const taken = db.state.sessions.find((s) => s.clientId !== assignment.clientId)!;
    await signIn(CLIENT_EMAIL);
    const fullAssignment = await assignmentsApi.get(assignment.id);
    const { user, router, queryClient } = renderApp(`/log/${taken.id}`);
    queryClient.setQueryData(queries.session(taken.id).queryKey, optimisticSession(taken.id, fullAssignment, new Date().toISOString()));
    void runSessionWrite(queryClient, sessionKeys.start, taken.id, { id: taken.id, assignmentId: assignment.id }).catch(() => undefined);

    await waitFor(() => expect(router.state.location.pathname).not.toBe(`/log/${taken.id}`));
    const freshId = router.state.location.pathname.split('/').at(-1)!;
    await waitFor(() => expect(db.state.sessions.find((s) => s.id === freshId)).toMatchObject({ clientId: assignment.clientId, assignmentId: assignment.id }));
    await findHeading(assignment.snapshot.name);
    const takenSets = taken.sets.length;
    await user.click(screen.getByRole('button', { name: 'Set 1 done' }));
    await waitFor(() => expect(db.state.sessions.find((s) => s.id === freshId)?.sets).toHaveLength(1));
    expect(taken.sets).toHaveLength(takenSets);
  });

  it('moves a set to a fresh id when another session owns the generated one, and keeps later writes on it', async () => {
    const { sessionId, queryClient } = await openScheduledWorkout();
    const taken = db.state.sessions.find((s) => s.id !== sessionId && s.sets.length > 0)!.sets[0]!;
    const first = queryClient.getQueryData<Session>(queries.session(sessionId).queryKey)!.exercises[0]!;
    const body = { exerciseId: first.exerciseId, position: first.position, setNumber: 1, weightKg: 50, reps: 5, durationSeconds: null, distanceMeters: null, rpe: null, completed: true, notes: null };
    await runSessionWrite(queryClient, sessionKeys.putSet, sessionId, { sessionId, setId: taken.id, body });
    await runSessionWrite(queryClient, sessionKeys.putSet, sessionId, { sessionId, setId: taken.id, body: { ...body, reps: 7 } });

    const sets = db.state.sessions.find((s) => s.id === sessionId)!.sets;
    expect(sets).toHaveLength(1);
    expect(sets[0]!.id).not.toBe(taken.id);
    expect(sets[0]).toMatchObject({ reps: 7 });
    const cached = queryClient.getQueryData<Session>(queries.session(sessionId).queryKey)!.exercises[0]!.sets;
    expect(cached.map((s) => s.id)).toEqual([sets[0]!.id]);
  });

  it('explains when the workout was completed or skipped before the start reached the server', async () => {
    const assignment = todaysAssignment();
    await signIn(CLIENT_EMAIL);
    const fullAssignment = await assignmentsApi.get(assignment.id);
    assignment.status = 'skipped';
    const attemptedId = crypto.randomUUID();
    const { queryClient } = renderApp(`/log/${attemptedId}`);
    queryClient.setQueryData(queries.session(attemptedId).queryKey, optimisticSession(attemptedId, fullAssignment, new Date().toISOString()));
    void runSessionWrite(queryClient, sessionKeys.start, attemptedId, { id: attemptedId, assignmentId: assignment.id }).catch(() => undefined);

    expect(await screen.findByText('This workout could not be started')).toBeInTheDocument();
    expect(screen.getByText(/completed or skipped in the meantime/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to today' })).toHaveAttribute('href', '/');
    expect(db.state.sessions.some((s) => s.id === attemptedId)).toBe(false);
  });

  it('redirects trainers from live logging to the read-only session', async () => {
    const existing = db.state.sessions.find((s) => s.status === 'completed')!;
    await signIn();
    const { router } = renderApp(`/log/${existing.id}`);
    await waitFor(() => expect(router.state.location.pathname).toBe(`/sessions/${existing.id}`));
  });
});
