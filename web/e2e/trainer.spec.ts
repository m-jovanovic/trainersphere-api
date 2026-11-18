import { expect, test } from '@playwright/test';
import { COMPOSE, TRAINER, goto, logIn } from './helpers';

test.skip(COMPOSE, 'MSW-only: relies on the mock seed');

test.describe('trainer journeys', () => {
  test('invite a client and see the pending invitation', async ({ page }) => {
    await logIn(page, TRAINER);
    await goto(page, '/clients');
    await page.getByRole('button', { name: 'Invite client' }).first().click();
    const dialog = page.getByRole('dialog');
    const stamp = Date.now();
    await dialog.getByLabel('First name').fill('E2E');
    await dialog.getByLabel('Last name').fill('Invitee');
    await dialog.getByLabel('Email').fill(`e2e-${stamp}@example.com`);
    await dialog.getByRole('button', { name: /Send invitation|Invite/ }).click();
    await expect(page.getByText(/Invitation sent/)).toBeVisible();
    await expect(page.getByText(`e2e-${stamp}@example.com`)).toBeVisible();
  });

  test('dashboard, workouts and exercises render', async ({ page }) => {
    await logIn(page, TRAINER);
    await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible();
    await goto(page, '/workouts');
    await expect(page.getByRole('heading', { name: 'Workouts', level: 1 })).toBeVisible();
    await goto(page, '/exercises');
    await expect(page.getByRole('heading', { name: 'Exercises', level: 1 })).toBeVisible();
  });
});
