import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDaysIso } from '@/lib/dates';
import { db } from '@/mocks/db';
import { findHeading, renderApp, signIn } from '@/test/utils';
import { findDialog } from '@/test/trainer';

const workoutId = (name: string) => db.state.workouts.find((w) => w.name === name)!.id;
const clientId = (email: string) => db.state.users.find((u) => u.email === email)!.id;
const program = (name: string) => db.state.programs.find((p) => p.name === name)!;
const slotsOf = (name: string) =>
  [...program(name).slots].sort((a, b) => a.week - b.week || a.day - b.day).map((s) => [s.week, s.day, s.workoutId]);

async function addWorkout(user: ReturnType<typeof renderApp>['user'], week: number, day: number, workout: string) {
  await user.click(screen.getByRole('button', { name: `Add workout to week ${week}, day ${day}` }));
  await user.click(await screen.findByRole('menuitem', { name: workout }));
}

describe('program editor', () => {
  it('creates a program: sets weeks, fills days, removes, repeats week 1 and saves', async () => {
    await signIn();
    const { user, router } = renderApp('/programs');
    await findHeading('Programs');
    await user.click(screen.getByRole('link', { name: 'New program' }));
    await findHeading('New program');
    expect(screen.getAllByRole('heading', { level: 2, name: /^Week \d$/ })).toHaveLength(4);

    await user.type(screen.getByLabelText('Program name'), 'Hypertrophy Block');
    await user.click(screen.getByRole('button', { name: 'Decrease Weeks' }));
    expect(screen.getByRole('textbox', { name: 'Weeks' })).toHaveValue('3');
    expect(screen.getAllByRole('heading', { level: 2, name: /^Week \d$/ })).toHaveLength(3);

    await addWorkout(user, 1, 1, 'Upper A');
    await addWorkout(user, 1, 4, 'Lower A');
    await addWorkout(user, 1, 4, 'Upper B');
    const day4 = within(screen.getByRole('group', { name: 'Week 1, day 4' }));
    expect(day4.getByText('Lower A')).toBeInTheDocument();
    await user.click(day4.getByRole('button', { name: 'Remove Upper B from week 1, day 4' }));
    expect(day4.queryByText('Upper B')).not.toBeInTheDocument();
    expect(screen.getByText('2 workouts')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Repeat week 1' }));
    expect(within(screen.getByRole('group', { name: 'Week 3, day 1' })).getByText('Upper A')).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Week 2, day 4' })).getByText('Lower A')).toBeInTheDocument();
    expect(screen.getByText('6 workouts')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Decrease Weeks' }));
    expect(screen.getByRole('status')).toHaveTextContent('2 workouts in weeks beyond 2 will be dropped when you save.');

    await user.click(screen.getByRole('button', { name: 'Create program' }));
    await findHeading('Edit program');
    const saved = program('Hypertrophy Block');
    expect(router.state.location.pathname).toBe(`/programs/${saved.id}`);
    expect(saved.weeks).toBe(2);
    expect(slotsOf('Hypertrophy Block')).toEqual([
      [1, 1, workoutId('Upper A')],
      [1, 4, workoutId('Lower A')],
      [2, 1, workoutId('Upper A')],
      [2, 4, workoutId('Lower A')],
    ]);
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument();
  });

  it('requires a name', async () => {
    await signIn();
    const { user } = renderApp('/programs/new');
    await findHeading('New program');
    await user.click(screen.getByRole('button', { name: 'Create program' }));
    expect(await screen.findByText('Name the program.')).toBeInTheDocument();
    expect(db.state.programs).toHaveLength(2);
  });

  it('edits an existing program, then assigns it to clients', async () => {
    const block = program('Conditioning Block');
    await signIn();
    const { user, router } = renderApp(`/programs/${block.id}`);
    await findHeading('Edit program');
    await user.click(screen.getByRole('button', { name: 'Remove Full Body Conditioning from week 2, day 5' }));
    await addWorkout(user, 2, 6, 'Lower B');
    expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Save program' }));
    await waitFor(() => expect(slotsOf('Conditioning Block')).toEqual([
      [1, 2, workoutId('Full Body Conditioning')],
      [1, 5, workoutId('Full Body Conditioning')],
      [2, 2, workoutId('Full Body Conditioning')],
      [2, 6, workoutId('Lower B')],
    ]));

    await user.click(await screen.findByRole('button', { name: 'Assign' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ assign: true }));
    const dialog = await findDialog('Assign program');
    await user.click(dialog.getByRole('button', { name: 'Assign program' }));
    expect(await dialog.findByRole('alert')).toHaveTextContent('Choose at least one client.');

    await user.click(await dialog.findByRole('checkbox', { name: 'Alex Rivera' }));
    await user.click(dialog.getByRole('checkbox', { name: 'Sam Lee' }));
    const start = addDaysIso(db.state.seededOn, 14);
    const date = dialog.getByLabelText('Start date');
    await user.clear(date);
    await user.click(dialog.getByRole('button', { name: 'Assign program' }));
    expect(await dialog.findByRole('alert')).toHaveTextContent('Pick a start date.');
    await user.type(date, start);
    expect(await dialog.findByText(/for each of 2 clients/)).toBeInTheDocument();
    const before = db.state.assignments.length;
    await user.click(dialog.getByRole('button', { name: 'Assign program' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Assign program' })).not.toBeInTheDocument());
    const created = db.state.programAssignments.filter((pa) => pa.programId === block.id);
    expect(created.map((pa) => pa.clientId).sort()).toEqual([clientId('alex@trainersphere.dev'), clientId('sam@trainersphere.dev')].sort());
    expect(created[0]).toMatchObject({ startDate: start, endDate: addDaysIso(start, 13), assignmentCount: 4 });
    expect(db.state.assignments.length).toBe(before + 8);
    const alexDates = db.state.assignments
      .filter((a) => a.programAssignmentId === created.find((pa) => pa.clientId === clientId('alex@trainersphere.dev'))!.id)
      .map((a) => [a.date, a.workoutId])
      .sort();
    expect(alexDates).toEqual([
      [addDaysIso(start, 1), workoutId('Full Body Conditioning')],
      [addDaysIso(start, 4), workoutId('Full Body Conditioning')],
      [addDaysIso(start, 8), workoutId('Full Body Conditioning')],
      [addDaysIso(start, 12), workoutId('Lower B')],
    ]);
  });

  it('marks a slot whose workout was archived before saving', async () => {
    const block = program('Conditioning Block');
    await signIn();
    const { user } = renderApp(`/programs/${block.id}`);
    await findHeading('Edit program');
    await addWorkout(user, 2, 6, 'Lower B');
    db.state.workouts.find((w) => w.name === 'Lower B')!.archived = true;
    await user.click(screen.getByRole('button', { name: 'Save program' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('A workout in this program is no longer available (archived or deleted). Remove the marked one and save again.');
    const day = within(screen.getByRole('group', { name: 'Week 2, day 6' }));
    expect(day.getByText(/no longer available/)).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Week 1, day 2' })).queryByText(/no longer available/)).not.toBeInTheDocument();
    expect(slotsOf('Conditioning Block').some(([, , w]) => w === workoutId('Lower B'))).toBe(false);

    await user.click(day.getByRole('button', { name: 'Remove Lower B from week 2, day 6' }));
    await user.click(screen.getByRole('button', { name: 'Save program' }));
    expect((await screen.findAllByText('Program saved')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('assigns a program from the client page', async () => {
    const alex = clientId('alex@trainersphere.dev');
    await signIn();
    const { user } = renderApp(`/clients/${alex}`);
    await findHeading('Alex Rivera');
    await user.click(screen.getByRole('button', { name: 'Assign program' }));
    const dialog = await findDialog('Assign program');
    expect(dialog.queryByRole('checkbox')).not.toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Assign program' }));
    expect(await dialog.findByRole('alert')).toHaveTextContent('Choose a program.');

    const block = program('Conditioning Block');
    await user.selectOptions(await dialog.findByLabelText('Program'), block.id);
    expect(await dialog.findByText('4 workouts')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Assign program' }));
    await waitFor(() => expect(db.state.programAssignments.some((pa) => pa.programId === block.id && pa.clientId === alex && pa.startDate === db.state.seededOn)).toBe(true));
    const programs = await screen.findByRole('link', { name: 'Conditioning Block' });
    expect(programs).toBeInTheDocument();
  });
});

describe('programs list', () => {
  it('archives a program', async () => {
    await signIn();
    const { user } = renderApp('/programs');
    await user.click(await screen.findByRole('button', { name: 'Actions for Conditioning Block' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Archive' }));
    await user.click((await findDialog('Archive Conditioning Block?')).getByRole('button', { name: 'Archive' }));
    await waitFor(() => expect(program('Conditioning Block').archived).toBe(true));
    await waitFor(() => expect(screen.queryByRole('link', { name: 'Conditioning Block' })).not.toBeInTheDocument());
  });
});
