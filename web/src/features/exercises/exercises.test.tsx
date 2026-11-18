import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { db } from '@/mocks/db';
import { findHeading, renderApp, signIn } from '@/test/utils';
import { findDialog } from '@/test/trainer';

const libraryNames = () =>
  within(screen.getByRole('list', { name: 'Exercise library' }))
    .getAllByRole('button')
    .map((b) => b.textContent ?? '');

describe('exercise library', () => {
  it('searches, switches between library and custom, and filters by muscle', async () => {
    await signIn();
    const { user, router } = renderApp('/exercises');
    await findHeading('Exercises');
    const total = db.state.exercises.length;
    await waitFor(() => expect(libraryNames()).toHaveLength(total));

    await user.type(screen.getByRole('searchbox', { name: 'Search exercises' }), 'squat');
    await waitFor(() => expect(router.state.location.search).toMatchObject({ q: 'squat' }));
    await waitFor(() => expect(libraryNames().length).toBeLessThan(total));
    expect(libraryNames().every((n) => /squat/i.test(n))).toBe(true);
    await user.click(screen.getByRole('button', { name: 'Clear search' }));

    await user.click(screen.getByRole('radio', { name: 'Custom' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ custom: true }));
    await waitFor(() => expect(libraryNames()).toEqual([expect.stringMatching(/^Landmine Press\s*Custom/)]));

    await user.click(screen.getByRole('radio', { name: 'All' }));
    await user.click(screen.getByRole('button', { name: 'Filters' }));
    const muscles = within(await screen.findByLabelText('Filter by muscle'));
    await user.click(muscles.getByRole('button', { name: 'Front delts' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ muscle: ['front-delts'] }));
    const expected = db.state.exercises.filter((e) => [...e.primaryMuscles, ...e.secondaryMuscles].includes('front-delts')).length;
    await waitFor(() => expect(libraryNames()).toHaveLength(expected));
    expect(screen.getByRole('button', { name: 'Filters (1)' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    await waitFor(() => expect(router.state.location.search).toEqual({}));
  });

  it('opens an exercise in the detail sheet', async () => {
    await signIn();
    const { user, router } = renderApp('/exercises');
    await user.click(await screen.findByRole('button', { name: /^Landmine Press/ }));
    const sheet = await findDialog('Landmine Press');
    expect(sheet.getByText('Strength · Vertical push · unilateral')).toBeInTheDocument();
    expect(sheet.getByText('Stagger stance, press up and across, finish with the arm by the ear.')).toBeInTheDocument();
    expect(sheet.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    const id = db.state.exercises.find((e) => e.slug === 'landmine-press')!.id;
    expect(router.state.location.search).toEqual({ id });

    await user.click(sheet.getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(router.state.location.search).toEqual({}));

    const squat = db.state.exercises.find((e) => e.slug === 'barbell-back-squat')!;
    await user.click(screen.getByRole('button', { name: new RegExp(`^${squat.name}`) }));
    const library = await findDialog(squat.name);
    expect(library.queryByRole('button', { name: 'Archive' })).not.toBeInTheDocument();
  });

  it('creates a custom exercise after validation', async () => {
    await signIn();
    const { user } = renderApp('/exercises');
    await findHeading('Exercises');
    await user.click(screen.getByRole('button', { name: 'New exercise' }));
    const form = await findDialog('New exercise');

    await user.click(form.getByRole('button', { name: 'Create exercise' }));
    expect(await form.findByText('Name the exercise.')).toBeInTheDocument();
    expect(form.getByText('Choose a movement pattern.')).toBeInTheDocument();
    expect(form.getByText('Pick at least one primary muscle.')).toBeInTheDocument();

    await user.type(form.getByLabelText('Name'), 'Half-kneeling Press');
    await user.selectOptions(form.getByLabelText('Movement pattern'), 'vertical-push');
    await user.click(within(form.getByRole('group', { name: 'Primary muscles' })).getByRole('button', { name: 'Front delts' }));
    await user.click(within(form.getByRole('group', { name: 'Equipment (optional)' })).getByRole('button', { name: 'Dumbbell' }));
    await user.click(form.getByRole('checkbox', { name: /Unilateral/ }));
    await user.type(form.getByLabelText(/Coaching cue/), 'Squeeze the glute on the down side.');
    await user.click(form.getByRole('button', { name: 'Create exercise' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'New exercise' })).not.toBeInTheDocument());
    expect(db.state.exercises.find((e) => e.name === 'Half-kneeling Press')).toMatchObject({
      isCustom: true,
      ownerId: db.state.users[0]!.id,
      movementPattern: 'vertical-push',
      primaryMuscles: ['front-delts'],
      equipment: ['dumbbell'],
      unilateral: true,
      cue: 'Squeeze the glute on the down side.',
    });
    expect(await screen.findByRole('button', { name: /^Half-kneeling Press\s*Custom/ })).toBeInTheDocument();
  });

  it('shows a duplicate custom name on the name field', async () => {
    await signIn();
    const { user } = renderApp('/exercises');
    await findHeading('Exercises');
    await user.click(screen.getByRole('button', { name: 'New exercise' }));
    const form = await findDialog('New exercise');
    await user.type(form.getByLabelText('Name'), 'landmine press');
    await user.selectOptions(form.getByLabelText('Movement pattern'), 'vertical-push');
    await user.click(within(form.getByRole('group', { name: 'Primary muscles' })).getByRole('button', { name: 'Chest' }));
    await user.click(form.getByRole('button', { name: 'Create exercise' }));

    expect(await form.findByText("You already have an exercise named 'landmine press'.")).toBeInTheDocument();
    expect(form.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true');
    expect(db.state.exercises.filter((e) => e.name.toLowerCase() === 'landmine press')).toHaveLength(1);
  });

  it('archives a custom exercise after confirming', async () => {
    await signIn();
    const landmine = db.state.exercises.find((e) => e.slug === 'landmine-press')!;
    const { user, router } = renderApp(`/exercises?id=${landmine.id}`);
    const sheet = await findDialog('Landmine Press');
    await user.click(sheet.getByRole('button', { name: 'Archive' }));
    const confirm = await findDialog('Archive exercise?');
    await user.click(confirm.getByRole('button', { name: 'Archive' }));

    await waitFor(() => expect(landmine.archived).toBe(true));
    await waitFor(() => expect(router.state.location.search).toEqual({}));
    await waitFor(() => expect(screen.queryByRole('button', { name: /^Landmine Press/ })).not.toBeInTheDocument());
  });
});
