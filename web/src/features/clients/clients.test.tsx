import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { db } from '@/mocks/db';
import { findHeading, renderApp, signIn } from '@/test/utils';
import { findCard, findDialog } from '@/test/trainer';

const clientId = (email: string) => db.state.users.find((u) => u.email === email)!.id;
const ALEX = 'alex@trainersphere.dev';
const JORDAN = 'jordan@trainersphere.dev';

describe('trainer dashboard', () => {
  it("shows today's schedule, adherence, recent PRs and pending invitations", async () => {
    await signIn();
    renderApp('/');
    await findHeading('Dashboard');

    const today = await findCard('Today · 1 of 3 done');
    expect(today.getByText('3 workouts on the schedule')).toBeInTheDocument();
    for (const name of ['Alex Rivera', 'Sam Lee', 'Jordan Patel']) expect(today.getByRole('link', { name })).toBeInTheDocument();
    expect(today.getByText('Completed')).toBeInTheDocument();
    expect(today.getByRole('link', { name: "View Jordan Patel's session" })).toBeInTheDocument();

    const adherence = await findCard(/^Adherence · avg \d+% \(7d\)$/);
    for (const name of ['Alex Rivera', 'Sam Lee', 'Jordan Patel'])
      expect(adherence.getByRole('meter', { name: `${name} adherence, last 7 days` })).toBeInTheDocument();

    const prs = await findCard('Recent PRs');
    expect(db.state.personalRecords.length).toBeGreaterThan(0);
    expect(prs.getAllByRole('listitem').length).toBeGreaterThan(0);

    const invites = await findCard(/^Pending invitations · \d+$/);
    expect(invites.getByText('Riley Morgan')).toBeInTheDocument();
    expect(invites.getByRole('button', { name: 'Actions for invitation to Riley Morgan' })).toBeInTheDocument();
  });
});

describe('clients', () => {
  it('searches by name through the URL and filters by status', async () => {
    db.state.clients.find((c) => c.id === clientId(JORDAN))!.status = 'archived';
    await signIn();
    const { user, router } = renderApp('/clients');
    await findHeading('Clients');
    const list = await screen.findByRole('list', { name: 'Clients' });
    await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(2));

    await user.type(screen.getByRole('searchbox', { name: 'Search clients' }), 'sam');
    await waitFor(() => expect(router.state.location.search).toMatchObject({ q: 'sam' }));
    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Clients' })).getAllByRole('listitem')).toHaveLength(1));
    expect(screen.getByRole('link', { name: /Sam Lee/ })).toBeInTheDocument();
    expect(screen.queryByText('Waiting to join')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Clear search' }));
    await user.click(screen.getByRole('radio', { name: 'Archived' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ status: 'archived' }));
    await waitFor(() => expect(screen.getByRole('link', { name: /Jordan Patel/ })).toBeInTheDocument());
    expect(screen.queryByRole('link', { name: /Alex Rivera/ })).not.toBeInTheDocument();
  });

  it('shows the empty state when nothing matches the search', async () => {
    await signIn();
    renderApp('/clients?q=zzz');
    expect(await screen.findByText('No clients match “zzz”')).toBeInTheDocument();
  });

  it('opens a client and switches between the detail tabs', async () => {
    await signIn();
    const { user, router } = renderApp('/clients');
    await user.click(await screen.findByRole('link', { name: /Alex Rivera/ }));
    await findHeading('Alex Rivera');
    expect(router.state.location.pathname).toBe(`/clients/${clientId(ALEX)}`);
    expect(await screen.findByRole('heading', { name: 'Next two weeks' })).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Schedule' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ tab: 'schedule' }));
    expect(await screen.findByRole('heading', { name: 'Upcoming' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Past' })).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'History' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ tab: 'history' }));
    expect((await screen.findAllByRole('link', { name: /Lower|Upper|Full Body/ })).length).toBeGreaterThan(0);

    await user.click(screen.getByRole('tab', { name: 'Progress' }));
    expect(await screen.findByRole('heading', { name: 'Exercise progress' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Training volume' })).toBeInTheDocument();
    expect(await screen.findByRole('combobox', { name: 'Exercise' })).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Body' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ tab: 'body' }));
    expect(await screen.findByRole('heading', { name: 'Body metrics' })).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Overview' }));
    await waitFor(() => expect(router.state.location.search).toEqual({}));
  });

  it('saves goals and private notes', async () => {
    await signIn();
    const { user } = renderApp(`/clients/${clientId(ALEX)}`);
    await findHeading('Alex Rivera');
    const notes = await findCard('Goals and notes');
    const save = notes.getByRole('button', { name: 'Save notes' });
    expect(save).toBeDisabled();

    const goals = notes.getByLabelText('Goals');
    expect(goals).toHaveValue('Squat 120 kg by March.');
    await user.clear(goals);
    await user.type(goals, 'Squat 130 kg by June.');
    await user.clear(notes.getByLabelText('Private notes'));
    await user.click(save);

    await waitFor(() => expect(db.state.clients.find((c) => c.id === clientId(ALEX))).toMatchObject({ goals: 'Squat 130 kg by June.', trainerNotes: null }));
    await waitFor(() => expect(notes.getByRole('button', { name: 'Save notes' })).toBeDisabled());
  });

  it('archives a client after confirming and restores them', async () => {
    await signIn();
    const id = clientId(ALEX);
    const { user } = renderApp(`/clients/${id}`);
    await findHeading('Alex Rivera');

    await user.click(screen.getByRole('button', { name: 'More client actions' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Archive client' }));
    const confirm = await findDialog('Archive Alex Rivera?');
    await user.click(confirm.getByRole('button', { name: 'Archive client' }));

    await waitFor(() => expect(db.state.clients.find((c) => c.id === id)!.status).toBe('archived'));
    await findHeading(/Alex Rivera\s*Archived/);
    expect(screen.queryByRole('button', { name: 'Schedule workout' })).not.toBeInTheDocument();
    expect(db.state.assignments.some((a) => a.clientId === id && a.status === 'scheduled' && a.date > db.state.seededOn)).toBe(false);

    await user.click(screen.getByRole('button', { name: 'More client actions' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Restore client' }));
    await waitFor(() => expect(db.state.clients.find((c) => c.id === id)!.status).toBe('active'));
    expect(await screen.findByRole('button', { name: 'Schedule workout' })).toBeInTheDocument();
  });
});

describe('invitations', () => {
  it('validates, sends an invitation and lists it as pending', async () => {
    await signIn();
    const { user, router } = renderApp('/clients');
    await findHeading('Clients');
    await user.click(screen.getByRole('button', { name: 'Invite client' }));
    await waitFor(() => expect(router.state.location.search).toEqual({ invite: true }));
    const dialog = await findDialog('Invite a client');

    await user.click(dialog.getByRole('button', { name: 'Send invitation' }));
    expect(await dialog.findByText('Enter their first name.')).toBeInTheDocument();
    expect(dialog.getByText('Enter their last name.')).toBeInTheDocument();
    expect(dialog.getByText('Enter your email.')).toBeInTheDocument();

    await user.type(dialog.getByLabelText('First name'), 'Casey');
    await user.type(dialog.getByLabelText('Last name'), 'Ng');
    await user.type(dialog.getByLabelText('Email'), 'not-an-email');
    await user.click(dialog.getByRole('button', { name: 'Send invitation' }));
    expect(await dialog.findByText('Enter a valid email address.')).toBeInTheDocument();

    await user.clear(dialog.getByLabelText('Email'));
    await user.type(dialog.getByLabelText('Email'), 'Casey@Example.com');
    await user.type(dialog.getByLabelText(/Personal message/), 'Welcome aboard');
    await user.click(dialog.getByRole('button', { name: 'Send invitation' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Invite a client' })).not.toBeInTheDocument());
    expect(router.state.location.search).toEqual({});
    expect(db.state.invitations.find((i) => i.email === 'casey@example.com')).toMatchObject({ status: 'pending', firstName: 'Casey', message: 'Welcome aboard' });
    const pending = await findCard('Waiting to join');
    expect(await pending.findByText('Casey Ng')).toBeInTheDocument();
    expect(pending.getByText('Riley Morgan')).toBeInTheDocument();
  });

  it('maps a duplicate pending invitation onto the email field', async () => {
    await signIn();
    const { user } = renderApp('/clients?invite=true');
    const dialog = await findDialog('Invite a client');
    await user.type(dialog.getByLabelText('First name'), 'Riley');
    await user.type(dialog.getByLabelText('Last name'), 'Morgan');
    await user.type(dialog.getByLabelText('Email'), 'riley@trainersphere.dev');
    await user.click(dialog.getByRole('button', { name: 'Send invitation' }));

    expect(await dialog.findByText("There is already a pending invitation for 'riley@trainersphere.dev'.")).toBeInTheDocument();
    expect(dialog.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
    expect(db.state.invitations.filter((i) => i.email === 'riley@trainersphere.dev')).toHaveLength(1);

    await user.clear(dialog.getByLabelText('Email'));
    await user.type(dialog.getByLabelText('Email'), ALEX);
    await user.click(dialog.getByRole('button', { name: 'Send invitation' }));
    expect(await dialog.findByText(`'${ALEX}' already has an account.`)).toBeInTheDocument();
  });

  it('resends and cancels a pending invitation', async () => {
    await signIn();
    const riley = db.state.invitations.find((i) => i.email === 'riley@trainersphere.dev')!;
    const oldToken = riley.token;
    const { user } = renderApp('/clients');
    const pending = await findCard('Waiting to join');

    await user.click(pending.getByRole('button', { name: 'Actions for invitation to Riley Morgan' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Resend invitation' }));
    await waitFor(() => expect(riley.token).not.toBe(oldToken));
    expect(await screen.findByText('New invitation sent to riley@trainersphere.dev')).toBeInTheDocument();

    await user.click(pending.getByRole('button', { name: 'Actions for invitation to Riley Morgan' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Cancel invitation' }));
    const confirm = await findDialog('Cancel invitation?');
    expect(confirm.getByText('The link sent to riley@trainersphere.dev will stop working.')).toBeInTheDocument();
    await user.click(confirm.getByRole('button', { name: 'Cancel invitation' }));

    await waitFor(() => expect(riley.status).toBe('cancelled'));
    await waitFor(() => expect(screen.queryByText('Waiting to join')).not.toBeInTheDocument());
  });
});
