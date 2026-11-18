import { expect, test, type APIRequestContext, type Page, type TestInfo } from '@playwright/test';
import { COMPOSE, PASSWORD, TRAINER, logIn } from './helpers';

// Journeys against the real API and its demo seed (docker compose). Assertions avoid exact
// counts that other tests or reruns change; anything a journey needs is ensured via the API first.
test.skip(!COMPOSE, 'Compose-only: runs against the real API and its demo seed');

type Person = { email: string; name: string; firstName: string };

// Both projects share one database, so each logs in as a different seeded client.
const CLIENTS: Record<string, Person> = {
  desktop: { email: 'alex@trainersphere.dev', name: 'Alex Rivera', firstName: 'Alex' },
  mobile: { email: 'sam@trainersphere.dev', name: 'Sam Chen', firstName: 'Sam' },
};
const SEEDED_CLIENTS = ['Alex Rivera', 'Jordan Patel', 'Sam Chen'];
const SEEDED_WORKOUTS = ['Full Body Conditioning', 'Lower A', 'Lower B', 'Upper A', 'Upper B'];

function clientFor(info: TestInfo): Person {
  return CLIENTS[info.project.name] ?? CLIENTS.desktop;
}

type Paged<T> = { items: T[] };

async function apiAs(request: APIRequestContext, email: string) {
  const login = await request.post('/api/auth/login', { data: { email, password: PASSWORD } });
  expect(login.status(), 'API login for ' + email).toBe(200);
  const { accessToken } = (await login.json()) as { accessToken: string };
  const headers = { Authorization: 'Bearer ' + accessToken };
  return {
    async get<T>(path: string): Promise<T> {
      const r = await request.get(path, { headers });
      expect(r.status(), 'GET ' + path).toBe(200);
      return (await r.json()) as T;
    },
    async post<T>(path: string, data: unknown): Promise<T> {
      const r = await request.post(path, { headers, data });
      expect(r.status(), 'POST ' + path).toBe(201);
      return (await r.json()) as T;
    },
  };
}

/** Makes sure the client has a workout it can start today; reruns find the seeded one already done. */
async function ensureStartableToday(request: APIRequestContext, client: Person) {
  const trainer = await apiAs(request, TRAINER);
  const dash = await trainer.get<{ date: string }>('/api/dashboard/trainer');
  const clients = await trainer.get<Paged<{ id: string; email: string }>>('/api/clients?pageSize=100');
  const clientId = clients.items.find((c) => c.email === client.email)?.id;
  expect(clientId, client.email + ' is an active client').toBeTruthy();
  const today = await trainer.get<{ status: string }[]>(`/api/assignments?clientId=${clientId ?? ''}&from=${dash.date}&to=${dash.date}`);
  if (today.some((a) => a.status === 'scheduled')) return dash.date;
  const workouts = await trainer.get<Paged<{ id: string; name: string }>>('/api/workouts?search=Upper%20B');
  const workout = workouts.items.find((w) => w.name === 'Upper B');
  expect(workout, 'seeded Upper B workout').toBeTruthy();
  await trainer.post('/api/assignments', { clientId, workoutId: workout?.id, date: dash.date, notes: 'Scheduled by the e2e suite.' });
  return dash.date;
}

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
/** The UI's `EEEE d MMMM` label for an ISO date. */
function dayLabel(iso: string) {
  const d = new Date(iso + 'T12:00:00Z');
  return `${DAYS[d.getUTCDay()] ?? ''} ${String(d.getUTCDate())} ${MONTHS[d.getUTCMonth()] ?? ''}`;
}

async function openSessionList(page: Page) {
  await page.goto('/history');
  await expect(page.getByRole('heading', { name: 'History', level: 1 })).toBeVisible();
  return page.getByRole('link').filter({ hasText: ' sets · ' });
}

test.describe('client journeys on real data', () => {
  test.describe.configure({ mode: 'serial' });

  test('today shows the seeded plan; log a workout end to end; the trainer sees it', async ({ page, request, browser }, info) => {
    const client = clientFor(info);
    await ensureStartableToday(request, client);

    await logIn(page, client.email);
    await expect(page.getByRole('heading', { level: 1 })).toContainText(client.firstName);
    await expect(page.getByText('Recent records')).toBeVisible();
    // The seed logged several sessions in the last two weeks.
    await expect(page.locator('a[href^="/sessions/"]').first()).toBeVisible();

    const start = page.getByRole('button', { name: /^(Start|Resume) workout$/ }).first();
    await start.click();
    await expect(page).toHaveURL(/\/log\/[0-9a-f-]+$/);
    const sessionId = new URL(page.url()).pathname.split('/').pop() ?? '';
    // The URL changes before the live page replaces the Today heading.
    const title = page.getByRole('heading', { level: 1 });
    await expect(title).toHaveText(new RegExp('^(' + SEEDED_WORKOUTS.join('|') + ')$'));
    const workoutName = (await title.textContent())?.trim() ?? '';

    const setSaved = page.waitForResponse((r) => r.url().includes('/sets/') && r.request().method() === 'PUT');
    await page.getByRole('button', { name: 'Set 1 done' }).first().click();
    await expect(page.getByRole('timer', { name: 'Rest remaining' })).toBeVisible();
    await page.getByRole('button', { name: 'Skip rest' }).click();
    expect((await setSaved).status()).toBe(200);

    // The reloaded page rebuilds the session from the server.
    await page.reload();
    await expect(page.getByRole('button', { name: /Edit set 1: .*done/ }).first()).toBeVisible();

    await page.getByRole('button', { name: 'Finish', exact: true }).click();
    const completed = page.waitForResponse((r) => r.url().endsWith('/complete') && r.request().method() === 'POST');
    await page.getByRole('dialog').getByRole('button', { name: 'Finish workout' }).click();
    expect((await completed).status()).toBe(200);
    await expect(page.getByRole('heading', { name: 'Workout complete' })).toBeVisible();
    await page.getByRole('link', { name: 'Done' }).click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('link', { name: 'See how it went' }).first()).toBeVisible();

    // History lists it first, and the detail shows the logged set.
    const sessions = await openSessionList(page);
    await expect(sessions.first()).toContainText(workoutName);
    await sessions.first().click();
    await expect(page).toHaveURL(new RegExp('/sessions/' + sessionId + '$'));
    await expect(page.getByRole('heading', { level: 1 })).toContainText(workoutName);
    await expect(page.getByText('Volume', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Completed').first()).toBeVisible();

    // The trainer's dashboard links today's entry to the finished session.
    const trainerContext = await browser.newContext({ baseURL: info.project.use.baseURL, viewport: info.project.use.viewport });
    const trainerPage = await trainerContext.newPage();
    await logIn(trainerPage, TRAINER);
    await expect(trainerPage.getByRole('link', { name: `View ${client.name}'s session` }).and(trainerPage.locator(`[href="/sessions/${sessionId}"]`))).toBeVisible();
    await trainerPage.goto('/sessions/' + sessionId);
    await expect(trainerPage.getByRole('heading', { level: 1 })).toContainText(workoutName);
    await trainerContext.close();
  });

  test('progress shows records, exercise charts and body metrics from the seed', async ({ page }, info) => {
    await logIn(page, clientFor(info).email);
    await page.goto('/progress');
    await expect(page.getByRole('heading', { name: 'Progress', level: 1 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Training volume' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Personal records table' }).locator('tbody tr').first()).toBeVisible();
    expect(await page.getByLabel('Exercise', { exact: true }).locator('option').count()).toBeGreaterThan(1);
    await expect(page.getByText('No measurements yet')).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Actions for \d{1,2} \w{3} \d{4}$/ }).first()).toBeVisible();
  });

  test('settings: units change sticks and converts weights, then switches back', async ({ page }, info) => {
    await logIn(page, clientFor(info).email);

    async function saveUnits(units: 'kg' | 'lb') {
      await page.goto('/settings');
      await page.getByLabel('Units').selectOption(units);
      const saved = page.waitForResponse((r) => r.url().endsWith('/auth/me') && r.request().method() === 'PUT');
      await page.getByRole('button', { name: 'Save profile' }).click();
      expect((await saved).status()).toBe(200);
      await expect(page.getByText('Profile saved').first()).toBeVisible();
    }

    await saveUnits('lb');
    try {
      await page.reload();
      await expect(page.getByLabel('Units')).toHaveValue('lb');
      const sessions = await openSessionList(page);
      await expect(sessions.first()).toContainText(' lb');
    } finally {
      await saveUnits('kg');
    }
  });
});

test.describe('trainer journeys on real data', () => {
  test('dashboard shows today, adherence, PRs and recent sessions', async ({ page }) => {
    await logIn(page, TRAINER);
    await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible();
    await expect(page.getByText(/^Today · \d+ of \d+ done$/)).toBeVisible();
    for (const name of SEEDED_CLIENTS) {
      await expect(page.getByRole('link', { name, exact: true }).first()).toBeVisible();
    }
    await expect(page.getByText(/^Adherence/).first()).toBeVisible();
    await expect(page.getByText('Recent PRs')).toBeVisible();
    await expect(page.getByText('No new records yet.', { exact: false })).toHaveCount(0);
    await expect(page.getByText(/^Pending invitations · \d+$/)).toBeVisible();
    await expect(page.getByText('riley@trainersphere.dev').first()).toBeVisible();
    await expect(page.locator('a[href^="/sessions/"]').first()).toBeVisible();
  });

  test('clients: seeded list, sorting, and a client profile', async ({ page }) => {
    await logIn(page, TRAINER);
    await page.goto('/clients');
    const rows = page.getByRole('list', { name: 'Clients' }).getByRole('listitem');
    await expect(rows).toHaveCount(3);
    await expect(rows.first()).toContainText('Alex Rivera');
    await expect(rows.last()).toContainText('Sam Chen');

    await page.getByLabel('Sort clients').selectOption('-name');
    await expect(page).toHaveURL(/sort=-name/);
    await expect(rows.first()).toContainText('Sam Chen');
    await expect(rows.last()).toContainText('Alex Rivera');

    await page.getByLabel('Sort clients').selectOption('name');
    await expect(rows.first()).toContainText('Alex Rivera');
    await rows.first().getByRole('link').click();
    await expect(page.getByRole('heading', { name: 'Alex Rivera', level: 1 })).toBeVisible();
    await page.getByRole('tab', { name: 'History' }).click();
    await expect(page.locator('a[href^="/sessions/"]').first()).toBeVisible();
    await page.getByRole('tab', { name: 'Progress' }).click();
    await expect(page.getByRole('region', { name: 'Personal records table' }).locator('tbody tr').first()).toBeVisible();
  });

  test('workouts and programs show the seeded library', async ({ page }) => {
    await logIn(page, TRAINER);
    await page.goto('/workouts');
    const workouts = page.getByRole('list', { name: 'Workouts' });
    for (const name of SEEDED_WORKOUTS) {
      await expect(workouts.getByRole('link', { name, exact: true })).toBeVisible();
    }
    await workouts.getByRole('link', { name: 'Upper A', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Edit workout', level: 1 })).toBeVisible();
    expect(await page.getByRole('list', { name: 'Workout exercises' }).getByRole('listitem').count()).toBeGreaterThan(2);

    await page.goto('/programs');
    await page.getByRole('list', { name: 'Programs' }).getByRole('link', { name: 'Strong Start' }).click();
    await expect(page.getByRole('heading', { name: 'Edit program', level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Remove .+ from week \d, day \d$/ })).toHaveCount(12);
  });

  test("schedule shows today's seeded workouts", async ({ page, request }) => {
    const trainer = await apiAs(request, TRAINER);
    const { date } = await trainer.get<{ date: string }>('/api/dashboard/trainer');
    await logIn(page, TRAINER);
    await page.goto('/schedule');
    await expect(page.getByRole('heading', { name: 'Schedule', level: 1 })).toBeVisible();
    // Desktop renders a month grid, phones an agenda; both label each day the same way.
    const day = page.locator(`[aria-label="${dayLabel(date)}"]`);
    await expect(day).toContainText('Upper A');
    await expect(day).toContainText('Jordan Patel');
  });

  test('invite a client and see the pending invitation', async ({ page }) => {
    await logIn(page, TRAINER);
    await page.goto('/clients');
    await page.getByRole('button', { name: 'Invite client' }).first().click();
    const dialog = page.getByRole('dialog');
    const email = `e2e-${String(Date.now())}-${String(Math.floor(Math.random() * 1e6))}@example.com`;
    await dialog.getByLabel('First name').fill('E2E');
    await dialog.getByLabel('Last name').fill('Invitee');
    await dialog.getByLabel('Email').fill(email);
    const sent = page.waitForResponse((r) => r.url().endsWith('/api/invitations') && r.request().method() === 'POST');
    await dialog.getByRole('button', { name: /Send invitation|Invite/ }).click();
    expect((await sent).status()).toBe(201);
    await expect(page.getByText(/Invitation sent/).first()).toBeVisible();
    // The pending list shows the address; the toast repeats it.
    await expect(page.locator('p', { hasText: email + ' · sent' })).toBeVisible();
  });
});
