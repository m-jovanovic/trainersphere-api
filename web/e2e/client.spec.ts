import { expect, test } from '@playwright/test';
import { CLIENT, COMPOSE, goto, logIn } from './helpers';

// Mock-seed journeys; compose.spec.ts covers the same flows on the real API seed.
test.skip(COMPOSE, 'MSW-only: relies on the mock seed');

test.describe('client workout logging', () => {
  test('start a scheduled workout, log a set, rest, and finish', async ({ page }) => {
    await logIn(page, CLIENT);
    const start = page.getByRole('button', { name: /^(Start|Resume) workout$/ }).first();
    await expect(start).toBeVisible();
    await start.click();
    await expect(page).toHaveURL(/\/log\//);

    // Mark the first set done; the rest timer starts.
    const setSaved = page.waitForResponse((r) => r.url().includes('/sets/') && r.request().method() === 'PUT');
    await page.getByRole('button', { name: 'Set 1 done' }).first().click();
    await expect(page.getByRole('timer', { name: 'Rest remaining' })).toBeVisible();
    await page.getByRole('button', { name: 'Skip rest' }).click();

    // The set survives a reload once the write and the mock's localStorage save have landed.
    await setSaved;
    await page.waitForTimeout(400);
    await page.reload();
    await expect(page.getByRole('button', { name: /Edit set 1: .*done/ })).toBeVisible();

    await page.getByRole('button', { name: 'Finish', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Finish workout' }).click();
    await expect(page.getByRole('heading', { name: 'Workout complete' })).toBeVisible();
    await page.getByRole('link', { name: 'Done' }).click();
    await expect(page).toHaveURL(/\/$/);
  });

  test('history lists sessions and opens the detail', async ({ page }) => {
    await logIn(page, CLIENT);
    await goto(page, '/history');
    await expect(page.getByRole('heading', { name: 'History', level: 1 })).toBeVisible();
    await page.getByRole('link').filter({ hasText: 'sets' }).first().click();
    await expect(page.getByText('Volume', { exact: true })).toBeVisible();
  });

  test('settings: change units and they stick', async ({ page }) => {
    await logIn(page, CLIENT);
    await goto(page, '/settings');
    await page.getByLabel('Units').selectOption('lb');
    const saved = page.waitForResponse((r) => r.url().includes('/auth/me') && r.request().method() === 'PUT');
    await page.getByRole('button', { name: 'Save profile' }).click();
    await expect(page.getByText('Profile saved')).toBeVisible();
    await saved;
    await page.waitForTimeout(400);
    await page.reload();
    await expect(page.getByLabel('Units')).toHaveValue('lb');
  });
});
