import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { db } from '@/mocks/db';
import { server } from '@/mocks/node';
import { findHeading, renderApp, signIn } from '@/test/utils';
import { findDialog } from '@/test/trainer';

const exercise = (slug: string) => db.state.exercises.find((e) => e.slug === slug)!;
const workout = (name: string) => db.state.workouts.find((w) => w.name === name)!;
const backToWorkouts = () => screen.getAllByRole('link', { name: 'Workouts' }).find((l) => !l.closest('nav'))!;
const itemNames = () =>
  within(screen.getByRole('list', { name: 'Workout exercises' }))
    .getAllByRole('listitem')
    .map((li) => li.getAttribute('aria-label'));

describe('workout builder', () => {
  it('builds a new workout: picks exercises, edits the prescription, reorders, removes and saves', async () => {
    const bench = exercise('barbell-bench-press');
    const row = exercise('barbell-bent-over-row');
    const pullUp = exercise('pull-up');
    await signIn();
    const { user, router } = renderApp('/workouts');
    await findHeading('Workouts');
    await user.click(screen.getByRole('link', { name: 'New workout' }));
    await findHeading('New workout');

    await user.type(screen.getByLabelText('Workout name'), 'Push Pull');
    await user.type(screen.getByLabelText(/Description/), 'Upper body mix.');
    await user.click(screen.getAllByRole('button', { name: 'Add exercises' })[0]!);
    const picker = await findDialog('Add exercises');
    expect(picker.getByRole('button', { name: 'Add exercises' })).toBeDisabled();
    for (const [query, e] of [['bench', bench], ['bent', row], ['pull-up', pullUp]] as const) {
      const search = picker.getByRole('searchbox', { name: 'Search exercises' });
      await user.clear(search);
      await user.type(search, query);
      await user.click(await picker.findByRole('button', { name: new RegExp(`^${e.name}`) }));
    }
    await user.click(picker.getByRole('button', { name: 'Add 3 exercises' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Add exercises' })).not.toBeInTheDocument());
    expect(itemNames()).toEqual([`1. ${bench.name}`, `2. ${row.name}`, `3. ${pullUp.name}`]);

    await user.click(screen.getByRole('button', { name: `Increase Sets for ${bench.name}` }));
    expect(screen.getByRole('textbox', { name: `Sets for ${bench.name}` })).toHaveValue('4');
    const min = screen.getByRole('spinbutton', { name: `Minimum reps for ${bench.name}` });
    await user.clear(min);
    await user.type(min, '5');
    const max = screen.getByRole('spinbutton', { name: `Maximum reps for ${bench.name}` });
    await user.clear(max);
    await user.type(max, '7');
    const weight = screen.getByRole('textbox', { name: `Weight for ${bench.name}` });
    await user.clear(weight);
    await user.type(weight, '62.5');
    await user.tab();
    await user.selectOptions(screen.getByRole('combobox', { name: `Rest for ${bench.name}` }), '150');

    await user.click(screen.getByRole('button', { name: `Actions for ${bench.name}` }));
    expect(screen.queryByRole('menuitem', { name: 'Move up' })).not.toBeInTheDocument();
    await user.click(await screen.findByRole('menuitem', { name: 'Move down' }));
    expect(itemNames()).toEqual([`1. ${row.name}`, `2. ${bench.name}`, `3. ${pullUp.name}`]);

    await user.click(screen.getByRole('button', { name: `Actions for ${pullUp.name}` }));
    await user.click(await screen.findByRole('menuitem', { name: 'Remove' }));
    expect(itemNames()).toEqual([`1. ${row.name}`, `2. ${bench.name}`]);
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Create workout' }));
    await findHeading('Edit workout');
    const saved = workout('Push Pull');
    expect(router.state.location.pathname).toBe(`/workouts/${saved.id}`);
    expect(saved.description).toBe('Upper body mix.');
    expect(saved.exercises.map((e) => e.exerciseId)).toEqual([row.id, bench.id]);
    expect(saved.exercises[1]).toMatchObject({ sets: 4, goal: { type: 'reps', min: 5, max: 7 }, load: { type: 'weight', value: 62.5 }, restSeconds: 150 });
    expect(saved.exercises[0]).toMatchObject({ sets: 3, goal: { type: 'reps', min: 8, max: 12 }, restSeconds: 90 });
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument();
  });

  it('reorders an existing workout with Move up and persists the order on save', async () => {
    const lowerA = workout('Lower A');
    const before = lowerA.exercises.map((e) => e.exerciseId);
    const second = exercise(db.state.exercises.find((e) => e.id === before[1])!.slug);
    await signIn();
    const { user } = renderApp(`/workouts/${lowerA.id}`);
    await findHeading('Edit workout');
    await user.click(await screen.findByRole('button', { name: `Actions for ${second.name}` }));
    await user.click(await screen.findByRole('menuitem', { name: 'Move up' }));
    expect(itemNames()[0]).toBe(`1. ${second.name}`);
    await user.click(screen.getByRole('button', { name: 'Save workout' }));
    await waitFor(() => expect(lowerA.exercises.map((e) => e.exerciseId)).toEqual([before[1], before[0], ...before.slice(2)]));
    expect(await screen.findByText('Workout saved')).toBeInTheDocument();
  });

  it('validates before saving', async () => {
    await signIn();
    const { user } = renderApp('/workouts/new');
    await findHeading('New workout');
    await user.click(screen.getByRole('button', { name: 'Create workout' }));
    expect(await screen.findByText('Name the workout.')).toBeInTheDocument();
    expect(screen.getByText('Add at least one exercise.')).toBeInTheDocument();
    expect(db.state.workouts).toHaveLength(5);
  });

  it('shows server validation errors on the matching exercise field', async () => {
    server.use(
      http.put('/api/workouts/:id', () =>
        HttpResponse.json(
          { title: 'One or more validation errors occurred.', status: 400, code: 'Validation', errors: { 'exercises[0].sets': ['Sets must be between 1 and 20.'] } },
          { status: 400 },
        ),
      ),
    );
    const lowerA = workout('Lower A');
    const first = db.state.exercises.find((e) => e.id === lowerA.exercises[0]!.exerciseId)!;
    await signIn();
    const { user } = renderApp(`/workouts/${lowerA.id}`);
    await findHeading('Edit workout');
    await user.click(await screen.findByRole('button', { name: 'Save workout' }));

    expect(await screen.findByText('Some fields need attention.')).toBeInTheDocument();
    const item = within(screen.getByRole('listitem', { name: `1. ${first.name}` }));
    expect(item.getByRole('alert')).toHaveTextContent('Sets must be between 1 and 20.');
    expect(item.getByText('Fix the highlighted fields')).toBeInTheDocument();
    expect(item.getByRole('textbox', { name: `Sets for ${first.name}` })).toBeVisible();
    expect(within(screen.getByRole('listitem', { name: `2. ${db.state.exercises.find((e) => e.id === lowerA.exercises[1]!.exerciseId)!.name}` })).queryByRole('alert')).not.toBeInTheDocument();
  });

  it('asks before leaving with unsaved changes', async () => {
    const lowerA = workout('Lower A');
    await signIn();
    const { user, router } = renderApp(`/workouts/${lowerA.id}`);
    await findHeading('Edit workout');
    await user.type(screen.getByLabelText('Workout name'), ' (heavy)');

    await user.click(backToWorkouts());
    const guard = await findDialog('Discard unsaved changes?');
    await user.click(guard.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe(`/workouts/${lowerA.id}`);
    expect(screen.getByLabelText('Workout name')).toHaveValue('Lower A (heavy)');

    await user.click(backToWorkouts());
    await user.click((await findDialog('Discard unsaved changes?')).getByRole('button', { name: 'Discard changes' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/workouts'));
    expect(lowerA.name).toBe('Lower A');
  });

  it('does not ask when leaving a saved workout', async () => {
    const lowerA = workout('Lower A');
    await signIn();
    const { user, router } = renderApp(`/workouts/${lowerA.id}`);
    await findHeading('Edit workout');
    await user.click(backToWorkouts());
    await waitFor(() => expect(router.state.location.pathname).toBe('/workouts'));
  });
});

describe('workouts list', () => {
  it('searches, duplicates and archives workouts', async () => {
    await signIn();
    const { user, router } = renderApp('/workouts');
    await findHeading('Workouts');
    const list = await screen.findByRole('list', { name: 'Workouts' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(5);

    await user.type(screen.getByRole('searchbox', { name: 'Search workouts' }), 'upper');
    await waitFor(() => expect(router.state.location.search).toEqual({ q: 'upper' }));
    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Workouts' })).getAllByRole('listitem')).toHaveLength(2));
    await user.click(screen.getByRole('button', { name: 'Clear search' }));

    await user.click(await screen.findByRole('button', { name: 'Actions for Lower B' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Archive' }));
    await user.click((await findDialog('Archive Lower B?')).getByRole('button', { name: 'Archive' }));
    await waitFor(() => expect(workout('Lower B').archived).toBe(true));
    await waitFor(() => expect(screen.queryByRole('link', { name: 'Lower B' })).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Actions for Upper B' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Duplicate' }));
    await findHeading('Edit workout');
    const copy = workout('Upper B (copy)');
    expect(router.state.location.pathname).toBe(`/workouts/${copy.id}`);
    expect(screen.getByLabelText('Workout name')).toHaveValue('Upper B (copy)');
  });
});
