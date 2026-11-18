import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import { authApi } from '@/lib/api/endpoints';
import { session } from '@/lib/auth/session';
import { DEMO_PASSWORD, db } from '@/mocks/db';
import { server } from '@/mocks/node';
import { CLIENT_EMAIL, findHeading, renderApp, signIn } from '@/test/utils';

afterEach(() => document.documentElement.classList.remove('dark'));

async function openSettings() {
  await signIn(CLIENT_EMAIL);
  const app = renderApp('/settings');
  await findHeading('Settings');
  return app;
}

describe('settings', () => {
  it('saves the complete profile and updates the signed-in user', async () => {
    const { user } = await openSettings();
    const firstName = screen.getByLabelText('First name');
    await user.clear(firstName);
    await user.type(firstName, 'Alexis');
    await user.selectOptions(screen.getByLabelText('Units'), 'lb');
    await user.click(screen.getByRole('button', { name: 'Save profile' }));

    expect(await screen.findByText('Profile saved')).toBeInTheDocument();
    expect(session.get().user).toMatchObject({ firstName: 'Alexis', lastName: 'Rivera', units: 'lb', timeZone: 'Europe/Belgrade' });
    expect(db.state.users.find((u) => u.email === CLIENT_EMAIL)).toMatchObject({ firstName: 'Alexis', units: 'lb' });
  });

  it('binds a profile validation error to first name', async () => {
    server.use(
      http.put('/api/auth/me', () =>
        HttpResponse.json(
          { title: 'Validation failed', status: 400, code: 'Validation', errors: { firstName: ['First name is not allowed.'] } },
          { status: 400 },
        ),
      ),
    );
    const { user } = await openSettings();
    const firstName = screen.getByLabelText('First name');
    await user.clear(firstName);
    await user.type(firstName, 'Alexis');
    await user.click(screen.getByRole('button', { name: 'Save profile' }));

    expect(await screen.findByText('First name is not allowed.')).toBeInTheDocument();
    expect(firstName).toHaveAccessibleDescription('First name is not allowed.');
  });

  it('shows an unmatched profile validation error at form level', async () => {
    server.use(
      http.put('/api/auth/me', () =>
        HttpResponse.json(
          { title: 'Validation failed', status: 400, code: 'Validation', errors: { $: ['Profile could not be updated.'] } },
          { status: 400 },
        ),
      ),
    );
    const { user } = await openSettings();
    await user.selectOptions(screen.getByLabelText('Units'), 'lb');
    await user.click(screen.getByRole('button', { name: 'Save profile' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Profile could not be updated.');
  });

  it('shows the wrong current password on its field', async () => {
    const { user } = await openSettings();
    await user.type(screen.getByLabelText('Current password'), 'incorrect');
    await user.type(screen.getByLabelText('New password'), 'NewPass123!');
    await user.type(screen.getByLabelText('Confirm new password'), 'NewPass123!');
    await user.click(screen.getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText('Current password is incorrect.')).toBeInTheDocument();
    expect(screen.getByLabelText('Current password')).toHaveAccessibleDescription('Current password is incorrect.');
  });

  it('changes the password and accepts the new password at login', async () => {
    const { user } = await openSettings();
    await user.type(screen.getByLabelText('Current password'), DEMO_PASSWORD);
    await user.type(screen.getByLabelText('New password'), 'NewPass123!');
    await user.type(screen.getByLabelText('Confirm new password'), 'NewPass123!');
    await user.click(screen.getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText('Password updated')).toBeInTheDocument();
    expect(db.state.users.find((u) => u.email === CLIENT_EMAIL)?.password).toBe('NewPass123!');
    const login = await authApi.login({ email: CLIENT_EMAIL, password: 'NewPass123!' });
    expect(login.user.email).toBe(CLIENT_EMAIL);
  });

  it('switches theme and persists rest timer preferences', async () => {
    const { user } = await openSettings();
    await user.click(screen.getByRole('radio', { name: 'Dark' }));
    expect(document.documentElement).toHaveClass('dark');
    expect(localStorage.getItem('ts-theme')).toBe('dark');

    const sound = screen.getByRole('checkbox', { name: /^Sound/ });
    const vibration = screen.getByRole('checkbox', { name: /^Vibration/ });
    expect(sound).toBeChecked();
    expect(vibration).toBeChecked();
    await user.click(sound);
    await user.click(vibration);
    expect(localStorage.getItem('ts-rest-sound')).toBe('false');
    expect(localStorage.getItem('ts-rest-vibrate')).toBe('false');
  });
});
