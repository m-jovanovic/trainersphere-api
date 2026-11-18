import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/node';
import { db, DEMO_PASSWORD, TRAINER_EMAIL } from '@/mocks/db';
import { mockConfig } from '@/mocks/handlers';
import { session } from '@/lib/auth/session';
import { CLIENT_EMAIL, findHeading, renderApp, signIn } from '@/test/utils';

describe('auth journeys', () => {
  it('redirects anonymous users to login and back after signing in', async () => {
    const { user, router } = renderApp('/clients?q=alex');
    await findHeading('Log in');
    expect(router.state.location.pathname).toBe('/login');

    await user.type(screen.getByLabelText('Email'), TRAINER_EMAIL);
    await user.type(screen.getByLabelText('Password'), DEMO_PASSWORD);
    await user.click(screen.getByRole('button', { name: 'Log in' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/clients'));
    expect(router.state.location.search).toEqual({ q: 'alex' });
    expect(session.get().user?.email).toBe(TRAINER_EMAIL);
  });

  it('validates the login form before calling the API', async () => {
    const { user } = renderApp('/login');
    await findHeading('Log in');
    await user.click(screen.getByRole('button', { name: 'Log in' }));
    expect(await screen.findByText('Enter your email.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows the API message for wrong credentials', async () => {
    const { user } = renderApp('/login');
    await findHeading('Log in');
    await user.type(screen.getByLabelText('Email'), TRAINER_EMAIL);
    await user.type(screen.getByLabelText('Password'), 'wrong-password');
    await user.click(screen.getByRole('button', { name: 'Log in' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
  });

  it('ignores open redirects', async () => {
    const { user, router } = renderApp('/login?redirect=%2F%2Fevil.example');
    await findHeading('Log in');
    await user.type(screen.getByLabelText('Email'), TRAINER_EMAIL);
    await user.type(screen.getByLabelText('Password'), DEMO_PASSWORD);
    await user.click(screen.getByRole('button', { name: 'Log in' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
  });

  it('signs up a trainer and maps a duplicate email onto the field', async () => {
    const { user, router } = renderApp('/signup');
    await findHeading('Create your trainer account');
    await user.type(screen.getByLabelText('First name'), 'Nina');
    await user.type(screen.getByLabelText('Last name'), 'Ortiz');
    await user.type(screen.getByLabelText('Email'), TRAINER_EMAIL);
    await user.type(screen.getByLabelText('Password'), 'long-enough-1');
    await user.click(screen.getByRole('button', { name: 'Create account' }));
    expect(await screen.findByText('An account with this email already exists.')).toBeInTheDocument();

    await user.clear(screen.getByLabelText('Email'));
    await user.type(screen.getByLabelText('Email'), 'nina@example.com');
    await user.click(screen.getByRole('button', { name: 'Create account' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(session.get().user).toMatchObject({ email: 'nina@example.com', role: 'trainer' });
  });

  it('binds server validation errors (400 errors map) to fields', async () => {
    server.use(
      http.post('/api/auth/signup', () =>
        HttpResponse.json(
          { title: 'One or more validation errors occurred.', status: 400, code: 'Validation', errors: { password: ['Password is too common.'] } },
          { status: 400 },
        ),
      ),
    );
    const { user } = renderApp('/signup');
    await findHeading('Create your trainer account');
    await user.type(screen.getByLabelText('First name'), 'Nina');
    await user.type(screen.getByLabelText('Last name'), 'Ortiz');
    await user.type(screen.getByLabelText('Email'), 'nina@example.com');
    await user.type(screen.getByLabelText('Password'), 'password1');
    await user.click(screen.getByRole('button', { name: 'Create account' }));
    expect(await screen.findByText('Password is too common.')).toBeInTheDocument();
  });

  it('accepts an invitation, sets a password and lands on the client home', async () => {
    const { user, router } = renderApp('/invite/demo-invite-riley-0000000000000000000000000');
    await findHeading('Join Maya Novak on TrainerSphere');
    expect(screen.getByText('“Excited to work with you!”')).toBeInTheDocument();
    expect(screen.getByLabelText('First name')).toHaveValue('Riley');

    await user.type(screen.getByLabelText('Password'), 'riley-pass-1');
    await user.type(screen.getByLabelText('Confirm password'), 'riley-pass-2');
    await user.click(screen.getByRole('button', { name: 'Accept and continue' }));
    expect(await screen.findByText('Passwords do not match.')).toBeInTheDocument();

    await user.clear(screen.getByLabelText('Confirm password'));
    await user.type(screen.getByLabelText('Confirm password'), 'riley-pass-1');
    await user.click(screen.getByRole('button', { name: 'Accept and continue' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(session.get().user).toMatchObject({ email: 'riley@trainersphere.dev', role: 'client' });
  });

  it('explains an invitation that was already used', async () => {
    renderApp('/invite/seed-accepted-0');
    await findHeading('Invitation no longer valid');
  });

  it('explains an unknown invitation link', async () => {
    renderApp('/invite/nope');
    await findHeading('Invitation not found');
  });

  it('requests a reset link without revealing whether the account exists', async () => {
    const { user } = renderApp('/forgot-password');
    await findHeading('Forgot your password?');
    await user.type(screen.getByLabelText('Email'), 'nobody@example.com');
    await user.click(screen.getByRole('button', { name: 'Send reset link' }));
    await findHeading('Check your email');
  });

  it('resets the password with a valid token and rejects an invalid one', async () => {
    const trainer = db.state.users.find((u) => u.email === TRAINER_EMAIL)!;
    db.state.resetTokens['good-token'] = trainer.id;

    const bad = renderApp('/reset-password?token=bad');
    await findHeading('Choose a new password');
    await bad.user.type(screen.getByLabelText('New password'), 'brand-new-pass');
    await bad.user.type(screen.getByLabelText('Confirm password'), 'brand-new-pass');
    await bad.user.click(screen.getByRole('button', { name: 'Update password' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('invalid or has expired');
    bad.unmount();

    const good = renderApp('/reset-password?token=good-token');
    await findHeading('Choose a new password');
    await good.user.type(screen.getByLabelText('New password'), 'brand-new-pass');
    await good.user.type(screen.getByLabelText('Confirm password'), 'brand-new-pass');
    await good.user.click(screen.getByRole('button', { name: 'Update password' }));
    await waitFor(() => expect(good.router.state.location.pathname).toBe('/login'));
    expect(trainer.password).toBe('brand-new-pass');
  });

  it('restores the session from the refresh cookie on load', async () => {
    await signIn(CLIENT_EMAIL);
    session.reset();
    const { router } = renderApp('/');
    await waitFor(() => expect(session.get().user?.email).toBe(CLIENT_EMAIL));
    expect(router.state.location.pathname).toBe('/');
  });

  it('sends signed-in users away from the login page', async () => {
    await signIn();
    const { router } = renderApp('/login');
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
  });

  it('logs out: clears the session and revokes the refresh token', async () => {
    await signIn();
    const { user, router } = renderApp('/');
    await user.click((await screen.findAllByRole('button', { name: /Maya Novak|Account menu/ }))[0]!);
    await user.click(await screen.findByRole('menuitem', { name: 'Log out' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(session.get().user).toBeNull();
    expect(mockConfig.refreshCookie).toBeNull();
  });

  it('keeps trainers out of client pages and clients out of trainer pages', async () => {
    await signIn(CLIENT_EMAIL);
    const client = renderApp('/clients');
    await waitFor(() => expect(client.router.state.location.pathname).toBe('/'));
    client.unmount();

    await signIn();
    const trainer = renderApp('/history');
    await waitFor(() => expect(trainer.router.state.location.pathname).toBe('/'));
  });
});
