import { screen, within } from '@testing-library/react';
import { format, parseISO } from 'date-fns';
import { describe, expect, it } from 'vitest';
import { db, exerciseRef, sessionTotals } from '@/mocks/db';
import { CLIENT_EMAIL, findHeading, renderApp, signIn } from '@/test/utils';
import { findCard } from '@/test/trainer';

function clientSessions() {
  const client = db.state.users.find((u) => u.email === CLIENT_EMAIL)!;
  return db.state.sessions.filter((s) => s.clientId === client.id && s.status === 'completed');
}

describe('client history', () => {
  it('groups completed sessions by month and opens a session with PR badges', async () => {
    const withPr = clientSessions().find((s) => s.personalRecords.length > 0)!;
    withPr.startedAt = new Date().toISOString();
    await signIn(CLIENT_EMAIL);
    const { user, router } = renderApp('/history');
    await findHeading('History');

    const month = format(parseISO(withPr.startedAt), 'MMMM yyyy');
    const group = await screen.findByRole('region', { name: month });
    expect(within(group).getByRole('heading', { name: month, level: 3 })).toBeInTheDocument();
    const link = within(group).getAllByRole('link', { name: new RegExp(withPr.workoutName) }).find((a) => a.getAttribute('href') === `/sessions/${withPr.id}`)!;
    expect(link).toBeInTheDocument();
    const exerciseCount = new Set(withPr.personalRecords.map((p) => p.exerciseId)).size;
    expect(within(link).getByLabelText(`Records on ${exerciseCount} exercise${exerciseCount === 1 ? '' : 's'}`)).toBeInTheDocument();

    await user.click(link);
    await findHeading(withPr.workoutName);
    expect(router.state.location.pathname).toBe(`/sessions/${withPr.id}`);
    const totals = sessionTotals(withPr);
    expect(screen.getByText('Sets').parentElement).toHaveTextContent(String(totals.completedSets));
    expect(screen.getByText('Reps', { selector: 'span' }).parentElement).toHaveTextContent(String(totals.totalReps));
    const records = await findCard(`PRs on ${exerciseCount} exercise${exerciseCount === 1 ? '' : 's'}`);
    expect(records.getAllByRole('listitem')).toHaveLength(exerciseCount);
    expect(records.getAllByText(withPr.personalRecords[0]!.exercise.name)).toHaveLength(1);
    expect(screen.getAllByRole('table').length).toBeGreaterThan(0);
    expect(screen.getAllByRole('columnheader', { name: 'Set' }).length).toBeGreaterThan(0);
  });

  it('lists one row per exercise when an exercise set several records', async () => {
    const session = clientSessions()[0]!;
    const [a, b] = session.plan.map((p) => p.exerciseId);
    const base = { clientId: session.clientId, weightKg: null, reps: null, sessionId: session.id, achievedAt: session.startedAt };
    session.personalRecords = [
      { ...base, id: 'r1', exerciseId: a!, exercise: exerciseRef(a!), type: 'estimated-1rm', value: 109.25, previousValue: 103.1 },
      { ...base, id: 'r2', exerciseId: a!, exercise: exerciseRef(a!), type: 'max-weight', value: 80, previousValue: 75 },
      { ...base, id: 'r3', exerciseId: a!, exercise: exerciseRef(a!), type: 'max-volume', value: 2560, previousValue: 2400 },
      { ...base, id: 'r4', exerciseId: b!, exercise: exerciseRef(b!), type: 'estimated-1rm', value: 30.75, previousValue: 29.4 },
    ];
    await signIn(CLIENT_EMAIL);
    renderApp(`/sessions/${session.id}`);

    const records = await findCard('PRs on 2 exercises');
    const rows = records.getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent(exerciseRef(a!).name);
    expect(rows[0]).toHaveTextContent('Heaviest set · also e1RM, volume');
    expect(rows[0]).toHaveTextContent('80 kg');
    expect(rows[1]).toHaveTextContent('Estimated 1RM');
    expect(within(rows[1]!).getByText('31 kg')).toHaveAttribute('title', 'Estimated 30.75 kg');
  });

  it('renders exercise set values and the prescription on the detail page', async () => {
    const completed = clientSessions()[0]!;
    const first = completed.plan[0]!;
    const firstSet = completed.sets.find((s) => s.position === first.position)!;
    const exercise = db.state.exercises.find((e) => e.id === first.exerciseId)!;
    await signIn(CLIENT_EMAIL);
    renderApp(`/sessions/${completed.id}`);

    await findHeading(completed.workoutName);
    expect(await screen.findByRole('heading', { name: exercise.name, level: 2 })).toBeInTheDocument();
    expect(screen.getByText(new RegExp(`Planned ${first.prescription.sets} ×`))).toBeInTheDocument();
    const row = screen.getAllByRole('row').find((r) => within(r).queryByText(String(firstSet.setNumber), { selector: 'td' }))!;
    expect(row).toHaveTextContent(String(firstSet.reps));
    expect(within(row).getByLabelText('Completed')).toBeInTheDocument();
  });
});
