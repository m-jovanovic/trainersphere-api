import { screen, within } from '@testing-library/react';
import { format, parseISO } from 'date-fns';
import { describe, expect, it } from 'vitest';
import { db, sessionTotals } from '@/mocks/db';
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
    const records = await findCard(`${withPr.personalRecords.length} personal record${withPr.personalRecords.length === 1 ? '' : 's'}`);
    expect(records.getAllByText(withPr.personalRecords[0]!.exercise.name).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('table').length).toBeGreaterThan(0);
    expect(screen.getAllByRole('columnheader', { name: 'Set' }).length).toBeGreaterThan(0);
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
