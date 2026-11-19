import { screen, waitFor, within } from '@testing-library/react';
import { format, parseISO } from 'date-fns';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { addDaysIso } from '@/lib/dates';
import { db, type DbAssignment } from '@/mocks/db';
import { findHeading, renderApp, signIn } from '@/test/utils';
import { findDialog, useDesktopViewport } from '@/test/trainer';

const clientId = (email: string) => db.state.users.find((u) => u.email === email)!.id;
const workoutId = (name: string) => db.state.workouts.find((w) => w.name === name)!.id;
const dayLabel = (iso: string) => format(parseISO(iso), 'EEEE d MMMM');
const ALEX = 'alex@trainersphere.dev';

function upcomingFor(email: string): DbAssignment[] {
  const id = clientId(email);
  return db.state.assignments
    .filter((a) => a.clientId === id && a.status === 'scheduled' && !a.sessionId && a.date > db.state.seededOn)
    .sort((a, b) => a.date.localeCompare(b.date));
}

async function openFromAgenda(user: ReturnType<typeof renderApp>['user'], a: DbAssignment) {
  const day = within(await screen.findByRole('region', { name: dayLabel(a.date) }, { timeout: 5000 }));
  await user.click(day.getByRole('button', { name: new RegExp(a.snapshot.name) }));
  return findDialog(a.snapshot.name);
}

describe('schedule (month grid)', () => {
  let restore: () => void;
  beforeEach(() => {
    restore = useDesktopViewport();
  });
  afterEach(() => restore());

  it('schedules a workout for a client on a chosen day', async () => {
    const target = addDaysIso(db.state.seededOn, 45);
    await signIn();
    const { user } = renderApp(`/schedule?month=${target}`);
    await findHeading('Schedule');
    const grid = await screen.findByRole('grid', { name: `${format(parseISO(target), 'MMMM yyyy')} schedule` });
    await user.click(within(grid).getByRole('button', { name: `Schedule on ${dayLabel(target)}` }));

    const dialog = await findDialog('Schedule a workout');
    expect(dialog.getByLabelText('Date')).toHaveValue(target);
    await user.click(dialog.getByRole('button', { name: 'Schedule' }));
    expect(await dialog.findByText('Choose a client.')).toBeInTheDocument();
    expect(dialog.getByText('Choose a workout.')).toBeInTheDocument();

    await user.selectOptions(dialog.getByLabelText('Client'), clientId('sam@trainersphere.dev'));
    await user.selectOptions(dialog.getByLabelText('Workout'), workoutId('Upper B'));
    await user.type(dialog.getByLabelText(/Notes for the client/), 'Hotel gym is fine.');
    await user.click(dialog.getByRole('button', { name: 'Schedule' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Schedule a workout' })).not.toBeInTheDocument());
    const created = db.state.assignments.find((a) => a.date === target && a.clientId === clientId('sam@trainersphere.dev'));
    expect(created).toMatchObject({ workoutId: workoutId('Upper B'), status: 'scheduled', notes: 'Hotel gym is fine.', programAssignmentId: null });
    const cell = within(screen.getByRole('gridcell', { name: dayLabel(target) }));
    expect(await cell.findByRole('button', { name: /Upper B.*Sam Lee.*scheduled/ })).toBeInTheDocument();
  });

  it('pages between months and filters by client', async () => {
    await signIn();
    const { user, router } = renderApp('/schedule');
    await findHeading('Schedule');
    const thisMonth = format(parseISO(db.state.seededOn), 'MMMM yyyy');
    expect(await screen.findByRole('grid', { name: `${thisMonth} schedule` })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next month' }));
    await waitFor(() => expect(router.state.location.search).toHaveProperty('month'));
    await user.click(await screen.findByRole('button', { name: 'Today' }));
    await waitFor(() => expect(router.state.location.search).toEqual({}));

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Filter by client' }), clientId(ALEX));
    await waitFor(() => expect(router.state.location.search).toEqual({ client: clientId(ALEX) }));
    const todayCell = within(await screen.findByRole('gridcell', { name: dayLabel(db.state.seededOn) }));
    await waitFor(() => expect(todayCell.getAllByRole('button', { name: /Alex Rivera/ })).toHaveLength(1));
    expect(todayCell.queryByRole('button', { name: /Sam Lee/ })).not.toBeInTheDocument();
  });
});

describe('schedule (agenda)', () => {
  it('reschedules a workout and updates its notes', async () => {
    const [a] = upcomingFor(ALEX);
    const moved = addDaysIso(a!.date, 1);
    await signIn();
    const { user } = renderApp(`/schedule?month=${a!.date}&client=${clientId(ALEX)}`);
    await findHeading('Schedule');
    const dialog = await openFromAgenda(user, a!);
    expect(dialog.getByText(new RegExp(`Alex Rivera · ${format(parseISO(a!.date), 'EEEE d MMMM yyyy')}`))).toBeInTheDocument();

    const date = dialog.getByLabelText('Date');
    await user.clear(date);
    await user.type(date, moved);
    await user.type(dialog.getByLabelText(/Notes for the client/), 'Moved for travel.');
    await user.click(dialog.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(a).toMatchObject({ date: moved, notes: 'Moved for travel.' }));
    expect(await screen.findByText(`Moved to ${dayLabel(moved)}`)).toBeInTheDocument();
  });

  it('marks a workout skipped and deletes another', async () => {
    const [first, second] = upcomingFor(ALEX);
    await signIn();
    const view = renderApp(`/schedule?month=${first!.date}&client=${clientId(ALEX)}`);
    await findHeading('Schedule');
    const dialog = await openFromAgenda(view.user, first!);
    await view.user.click(dialog.getByRole('button', { name: 'Mark skipped' }));
    await waitFor(() => expect(first!.status).toBe('skipped'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    view.unmount();

    const { user } = renderApp(`/schedule?month=${second!.date}&client=${clientId(ALEX)}`);
    await findHeading('Schedule');
    const next = await openFromAgenda(user, second!);
    await user.click(next.getByRole('button', { name: 'Delete' }));
    const confirm = await findDialog('Delete this workout?');
    expect(confirm.getByText(new RegExp(`will be removed from Alex Rivera's schedule`))).toBeInTheDocument();
    await user.click(confirm.getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(db.state.assignments.some((x) => x.id === second!.id)).toBe(false));
  });

  it('explains when the workout can no longer be changed', async () => {
    const [a] = upcomingFor(ALEX);
    await signIn();
    const { user } = renderApp(`/schedule?month=${a!.date}&client=${clientId(ALEX)}`);
    await findHeading('Schedule');
    const dialog = await openFromAgenda(user, a!);
    a!.status = 'completed';

    await user.click(dialog.getByRole('button', { name: 'Save changes' }));
    expect(await dialog.findByRole('alert')).toHaveTextContent('This workout was already completed or skipped, so it can no longer be changed.');
    expect(dialog.queryByRole('link', { name: 'View the session' })).not.toBeInTheDocument();
    expect(a!.status).toBe('completed');
  });

  it('links to the session when the client started the workout meanwhile', async () => {
    const [a] = upcomingFor(ALEX);
    const started = db.state.sessions.find((s) => s.clientId === clientId(ALEX))!;
    await signIn();
    const { user } = renderApp(`/schedule?month=${a!.date}&client=${clientId(ALEX)}`);
    await findHeading('Schedule');
    const dialog = await openFromAgenda(user, a!);
    a!.sessionId = started.id;

    await user.click(dialog.getByRole('button', { name: 'Mark skipped' }));
    expect(await dialog.findByRole('alert')).toHaveTextContent('The client already started or finished this workout');
    expect(dialog.getByRole('link', { name: 'View the session' })).toHaveAttribute('href', `/sessions/${started.id}`);
    expect(a!.status).toBe('scheduled');
  });

  it('schedules from the client page with the client preset', async () => {
    const alex = clientId(ALEX);
    const target = addDaysIso(db.state.seededOn, 3);
    await signIn();
    const { user } = renderApp(`/clients/${alex}?tab=schedule`);
    await findHeading('Alex Rivera');
    await user.click((await screen.findAllByRole('button', { name: 'Schedule workout' }))[0]!);
    const dialog = await findDialog('Schedule a workout');
    expect(dialog.queryByLabelText('Client')).not.toBeInTheDocument();
    await user.selectOptions(dialog.getByLabelText('Workout'), workoutId('Lower B'));
    const date = dialog.getByLabelText('Date');
    await user.clear(date);
    await user.type(date, target);
    await user.click(dialog.getByRole('button', { name: 'Schedule' }));
    await waitFor(() => expect(db.state.assignments.some((x) => x.clientId === alex && x.date === target && x.workoutId === workoutId('Lower B'))).toBe(true));
    const upcoming = within(screen.getByRole('heading', { name: 'Upcoming' }).parentElement!.parentElement!.parentElement!);
    const expected = db.state.assignments.filter((x) => x.clientId === alex && x.date >= db.state.seededOn && x.date <= addDaysIso(db.state.seededOn, 55) && x.snapshot.name === 'Lower B').length;
    await waitFor(() => expect(upcoming.getAllByRole('button', { name: /Lower B/ })).toHaveLength(expected));
  });
});
