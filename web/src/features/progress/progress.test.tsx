import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { todayIso } from '@/lib/dates';
import { db } from '@/mocks/db';
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

    expect(await screen.findByText(/No estimated 1RM yet: it counts sets of 1 to 12 reps\./)).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Top set' }));
    expect(await screen.findByRole('table', { name: /^Top set for / })).toBeInTheDocument();
    expect(screen.getAllByText('22.5 kg').length).toBeGreaterThan(0);
  });
});
