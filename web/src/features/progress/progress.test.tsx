import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { todayIso } from '@/lib/dates';
import type { PersonalRecord, PrType } from '@/lib/api/types';
import { db, exerciseRef } from '@/mocks/db';
import { server } from '@/mocks/node';
import { CLIENT_EMAIL, findHeading, renderApp, signIn } from '@/test/utils';
import { findDialog } from '@/test/trainer';

async function openProgress(path = '/progress#body') {
  await signIn(CLIENT_EMAIL);
  const app = renderApp(path);
  await findHeading('Progress');
  await screen.findByRole('heading', { name: 'Body metrics', level: 2 });
  return app;
}

describe('client progress', () => {
  it('renders accessible training charts, exercise controls, records and the body anchor', async () => {
    const { user } = await openProgress();

    expect(await screen.findByRole('table', { name: 'Weekly training volume' })).toBeInTheDocument();
    expect(screen.getByText(/Weekly volume for the last 12 weeks/)).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Exercise' })).toBeEnabled();
    expect(screen.getByRole('radiogroup', { name: 'Metric' })).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Top set' }));
    expect(await screen.findByRole('table', { name: /^Top set for / })).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Personal records table' })).getByRole('table')).toBeInTheDocument();
    expect(document.getElementById('body')).toHaveAttribute('aria-label', 'Body measurements');
  });

  it('adds a body weight entry and refreshes the chart summary', async () => {
    const { user } = await openProgress();
    await user.click(screen.getByRole('button', { name: 'Log measurements' }));
    const dialog = await findDialog('Log measurements');
    await user.type(dialog.getByLabelText('Body weight (kg)'), '83.25');
    await user.click(dialog.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Log measurements' })).not.toBeInTheDocument());
    const clientId = db.state.users.find((u) => u.email === CLIENT_EMAIL)!.id;
    expect(db.state.bodyMetrics.find((m) => m.clientId === clientId && m.date === todayIso())).toMatchObject({ weightKg: 83.25, recordedBy: 'client' });
    expect(await screen.findByText(/Body weight: \d+ entries, latest 83.3 kg\./)).toBeInTheDocument();
  });

  it('maps body metric server validation onto the weight field', async () => {
    server.use(
      http.put('/api/clients/:id/body-metrics/:date', () =>
        HttpResponse.json(
          { title: 'Validation failed', status: 400, code: 'Validation', errors: { weightKg: ['Weight must be between 20 and 400 kg.'] } },
          { status: 400 },
        ),
      ),
    );
    const { user } = await openProgress();
    await user.click(screen.getByRole('button', { name: 'Log measurements' }));
    const dialog = await findDialog('Log measurements');
    await user.type(dialog.getByLabelText('Body weight (kg)'), '83');
    await user.click(dialog.getByRole('button', { name: 'Save' }));

    expect(await dialog.findByText('Weight must be between 20 and 400 kg.')).toBeInTheDocument();
    expect(dialog.getByLabelText('Body weight (kg)')).toHaveAccessibleDescription('Weight must be between 20 and 400 kg.');
  });

  it('shows an unmatched body metric error at form level', async () => {
    server.use(
      http.put('/api/clients/:id/body-metrics/:date', () =>
        HttpResponse.json(
          { title: 'Validation failed', status: 400, code: 'Validation', errors: { $: ['This measurement could not be saved.'] } },
          { status: 400 },
        ),
      ),
    );
    const { user } = await openProgress();
    await user.click(screen.getByRole('button', { name: 'Log measurements' }));
    const dialog = await findDialog('Log measurements');
    await user.type(dialog.getByLabelText('Body weight (kg)'), '83');
    await user.click(dialog.getByRole('button', { name: 'Save' }));

    expect(await dialog.findByRole('alert')).toHaveTextContent('This measurement could not be saved.');
  });
  it('explains a missing estimated 1RM when only high-rep weighted sets were logged', async () => {
    const point = { sessionId: 's1', estimatedOneRepMaxKg: null, topSetWeightKg: 22.5, topSetReps: 15, volumeKg: 1575, totalReps: 45, completedSets: 3 };
    server.use(
      http.get('/api/clients/:id/progress/exercises/:exerciseId', () =>
        HttpResponse.json({ exercise: null, points: [{ ...point, date: '2026-11-10' }, { ...point, date: '2026-11-13' }] }),
      ),
    );
    const { user } = await openProgress();

    // The default skips the empty e1RM chart; asking for it explains why it is empty.
    expect(await screen.findByRole('table', { name: /^Top set for / })).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'e1RM' }));
    expect(await screen.findByText(/No estimated 1RM yet: it counts sets of 1 to 12 reps\./)).toBeInTheDocument();
    expect(screen.getAllByText('22.5 kg').length).toBeGreaterThan(0);
  });
});
describe('exercise progress defaults', () => {
  function setUp({ e1rmRecordFor }: { e1rmRecordFor: 'older' | 'none' }) {
    const clientId = db.state.users.find((u) => u.email === CLIENT_EMAIL)!.id;
    const [recent, older] = db.state.exercises.slice(0, 2).map((e) => exerciseRef(e.id));
    const point = (date: string, e1rm: number | null, top: number) => ({ date, sessionId: 's', estimatedOneRepMaxKg: e1rm, topSetWeightKg: top, topSetReps: e1rm === null ? 15 : 8, volumeKg: top * 30, totalReps: 30, completedSets: 3 });
    const record = (exerciseId: string, type: PrType, value: number): PersonalRecord => ({
      id: exerciseId + type, clientId, exerciseId, exercise: exerciseRef(exerciseId), type, value, previousValue: value - 2, weightKg: null, reps: null, sessionId: 's', achievedAt: '2026-11-13T17:00:00Z',
    });
    server.use(
      // The most recent and most trained exercise only ever had high-rep sets, so it has no e1RM.
      http.get('/api/clients/:id/progress/exercises', () =>
        HttpResponse.json([
          { exerciseId: recent!.id, exercise: recent, sessionCount: 9, lastPerformedAt: '2026-11-16T17:00:00Z' },
          { exerciseId: older!.id, exercise: older, sessionCount: 4, lastPerformedAt: '2026-11-13T17:00:00Z' },
        ]),
      ),
      http.get('/api/clients/:id/progress/exercises/:exerciseId', ({ params }) =>
        HttpResponse.json({
          exercise: null,
          points: params.exerciseId === older!.id && e1rmRecordFor === 'older' ? [point('2026-11-06', 103.1, 80), point('2026-11-13', 109.25, 82.5)] : [point('2026-11-09', null, 20), point('2026-11-16', null, 22.5)],
        }),
      ),
      http.get('/api/clients/:id/personal-records', () =>
        HttpResponse.json([
          record(recent!.id, 'max-weight', 22.5),
          ...(e1rmRecordFor === 'older' ? [record(older!.id, 'estimated-1rm', 109.25), record(older!.id, 'max-weight', 82.5)] : []),
        ]),
      ),
    );
    return { recent: recent!, older: older! };
  }

  it('opens on the most recent exercise with an e1RM and rounds the estimate for display', async () => {
    const { older } = setUp({ e1rmRecordFor: 'older' });
    await openProgress('/progress');

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Exercise' })).toHaveValue(older.id));
    expect(screen.getByRole('radio', { name: 'e1RM' })).toBeChecked();
    const table = await screen.findByRole('table', { name: 'Estimated 1RM for ' + older.name });
    expect(within(table).getByText('109.5 kg')).toBeInTheDocument();
    expect(screen.getByText('109.5 kg', { selector: 'span' })).toBeInTheDocument();
    expect(screen.getByText('+6.5 kg since 6 Nov')).toBeInTheDocument();
  });

  it('falls back to the top set when no exercise has an e1RM, and back to the default on another exercise', async () => {
    const { recent, older } = setUp({ e1rmRecordFor: 'none' });
    const { user } = await openProgress('/progress');

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Exercise' })).toHaveValue(recent.id));
    expect(await screen.findByRole('table', { name: 'Top set for ' + recent.name })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Top set' })).toBeChecked();

    await user.click(screen.getByRole('radio', { name: 'Volume' }));
    expect(await screen.findByRole('table', { name: 'Volume for ' + recent.name })).toBeInTheDocument();
    await user.selectOptions(screen.getByRole('combobox', { name: 'Exercise' }), older.id);
    expect(await screen.findByRole('table', { name: 'Top set for ' + older.name })).toBeInTheDocument();
  });

  it('hides record columns nobody has a value in and keeps the exact e1RM as a tooltip', async () => {
    setUp({ e1rmRecordFor: 'older' });
    await openProgress('/progress');

    const region = await screen.findByRole('region', { name: 'Personal records table' });
    const headers = within(region).getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual(['Exercise', 'e1RM', 'Heaviest set']);
    expect(within(region).getByText('109.5 kg')).toHaveAttribute('title', 'Estimated 109.25 kg');
  });
});
