import { expect, type Page } from '@playwright/test';

/** True when running against the docker compose stack instead of the MSW dev server. */
export const COMPOSE = process.env.E2E_TARGET === 'compose';

export const TRAINER = 'coach@trainersphere.dev';
export const CLIENT = 'alex@trainersphere.dev';
export const PASSWORD = 'Demo123!';

export async function logIn(page: Page, email: string, password = PASSWORD) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
  await expect(page).not.toHaveURL(/\/login/);
  // The MSW mock answers after 250 ms and persists its db to localStorage 100 ms after that;
  // navigating before the save loses the just-issued refresh token and bounces to /login.
  await page.waitForTimeout(600);
}

/**
 * Full-page navigation against the MSW mock. Every load rotates the refresh token, and the
 * rotation only reaches localStorage after the mock's debounced save, so consecutive loads
 * need a settle delay or the next bootstrap refresh fails and bounces to /login.
 */
export async function goto(page: Page, path: string) {
  await page.goto(path);
  await page.waitForTimeout(600);
}
