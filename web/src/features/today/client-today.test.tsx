import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { sessionsApi } from '@/lib/api/endpoints';
import { todayIso } from '@/lib/dates';
import { formatGoal, formatLoad } from '@/lib/domain';
import { db } from '@/mocks/db';
import { CLIENT_EMAIL, findHeading, renderApp, signIn } from '@/test/utils';
import { findCard, findDialog } from '@/test/trainer';

function todaysAssignment() {
  const client = db.state.users.find((u) => u.email === CLIENT_EMAIL)!;
  return db.state.assignments.find((a) => a.clientId === client.id && a.date === todayIso())!;
}

describe('client today', () => {
  it('greets the client and shows the scheduled workout prescription', async () => {
    const assignment = todaysAssignment();
    const first = assignment.snapshot.exercises[0]!;
    const load = formatLoad(first.load, 'kg');
    await signIn(CLIENT_EMAIL);
    renderApp('/');

    await findHeading(/Good (morning|afternoon|evening), Alex/);
    const workout = await findCard(assignment.snapshot.name);
    expect(workout.getByText(assignment.snapshot.description!)).toBeInTheDocument();
    expect(workout.getByRole('list', { name: `${assignment.snapshot.name} exercises` })).toBeInTheDocument();
    expect(workout.getByText(`${first.sets} × ${formatGoal(first.goal)}${load ? ` @ ${load}` : ''}`)).toBeInTheDocument();
    expect(workout.getByRole('button', { name: 'Start workout' })).toBeEnabled();
  });

  it('starts the scheduled workout and opens live logging', async () => {
    const assignment = todaysAssignment();
    await signIn(CLIENT_EMAIL);
    const { user, router } = renderApp('/');
    await user.click(await screen.findByRole('button', { name: 'Start workout' }));

    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/log\/.+/));
    await findHeading(assignment.snapshot.name);
    const sessionId = router.state.location.pathname.split('/').at(-1)!;
    await waitFor(() => expect(db.state.sessions.some((s) => s.id === sessionId && s.assignmentId === assignment.id)).toBe(true));
  });

  it('skips today after confirmation and updates the card', async () => {
    const assignment = todaysAssignment();
    await signIn(CLIENT_EMAIL);
    const { user } = renderApp('/');
    await user.click(await screen.findByRole('button', { name: `More options for ${assignment.snapshot.name}` }));
    await user.click(await screen.findByRole('menuitem', { name: 'Skip today' }));
    const dialog = await findDialog(`Skip ${assignment.snapshot.name}?`);
    await user.type(dialog.getByLabelText(/Reason/), 'Recovery day');
    await user.click(dialog.getByRole('button', { name: 'Skip workout' }));

    expect(await screen.findByText('Skipped. Enjoy the rest, or log a different workout below.')).toBeInTheDocument();
    await waitFor(() => expect(assignment.status).toBe('skipped'));
    expect(screen.queryByRole('button', { name: 'Start workout' })).not.toBeInTheDocument();
  });

  it('offers Resume for an in-progress assignment', async () => {
    const assignment = todaysAssignment();
    await signIn(CLIENT_EMAIL);
    const started = await sessionsApi.start({ assignmentId: assignment.id });
    const { user, router } = renderApp('/');

    await user.click(await screen.findByRole('button', { name: 'Resume workout' }));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/log/${started.id}`));
    await findHeading(assignment.snapshot.name);
  });

  it('names and starts an ad-hoc workout', async () => {
    await signIn(CLIENT_EMAIL);
    const { user, router } = renderApp('/');
    await user.click(await screen.findByRole('button', { name: 'Log a different workout' }));
    const dialog = await findDialog('Log a different workout');
    expect(dialog.getByRole('button', { name: 'Start' })).toBeDisabled();
    await user.type(dialog.getByLabelText('Workout name'), 'Hotel gym');
    await user.click(dialog.getByRole('button', { name: 'Start' }));

    await findHeading('Hotel gym');
    expect(screen.getByText('Add your first exercise')).toBeInTheDocument();
    const id = router.state.location.pathname.split('/').at(-1);
    await waitFor(() => expect(db.state.sessions.some((s) => s.id === id && s.workoutName === 'Hotel gym' && s.assignmentId === null)).toBe(true));
  });

  it('shows recovery and the next assignment on a rest day', async () => {
    const clientId = todaysAssignment().clientId;
    db.state.assignments = db.state.assignments.filter((a) => !(a.clientId === clientId && a.date === todayIso()));
    await signIn(CLIENT_EMAIL);
    renderApp('/');

    expect(await screen.findByRole('heading', { name: 'Rest day' })).toBeInTheDocument();
    expect(screen.getByText('Nothing is scheduled today. Recovery is part of the plan.')).toBeInTheDocument();
    expect(screen.getByText(/Up next ·/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Do it today instead' })).toBeEnabled();
  });
});
