import { expect, test } from '@playwright/test';
import { CLIENT, PASSWORD, TRAINER, logIn } from './helpers';

// These journeys run against both the MSW dev server and the compose stack.
test.describe('auth and session', () => {
  test('trainer logs in and a hard reload keeps the session', async ({ page }) => {
    await logIn(page, TRAINER);
    await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible();
  });

  test('client logs in and a deep-link hard load keeps the session', async ({ page }) => {
    await logIn(page, CLIENT);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.goto('/history');
    await expect(page.getByRole('heading', { name: 'History', level: 1 })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('heading', { name: 'History', level: 1 })).toBeVisible();
    await expect(page).not.toHaveURL(/\/login/);
  });

  test('wrong credentials show an error', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email', { exact: true }).fill(TRAINER);
    await page.getByLabel('Password', { exact: true }).fill('wrong-password-1');
    await page.getByRole('button', { name: 'Log in' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
  });

  test('email links survive a hard load', async ({ page }) => {
    await page.goto('/invite/not-a-real-token');
    await expect(page.getByRole('heading', { name: 'Invitation not found' })).toBeVisible();

    await page.goto('/reset-password?token=abc&email=x%40example.com');
    await expect(page.getByRole('heading', { name: 'Choose a new password' })).toBeVisible();
  });

  test('anonymous users are sent to login and back after signing in', async ({ page }) => {
    await page.goto('/settings');
    await expect(page).toHaveURL(/\/login/);
    await page.getByLabel('Email', { exact: true }).fill(CLIENT);
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByRole('button', { name: 'Log in' }).click();
    await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible();
  });
});
