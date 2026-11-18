import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { z } from 'zod';
import { ApiError, errorMessage } from '@/lib/api/http';

export const emailSchema = z.string().trim().min(1, 'Enter your email.').max(254, 'Email is too long.').pipe(z.email('Enter a valid email address.'));
export const passwordSchema = z.string().min(8, 'Use at least 8 characters.').max(128, 'Use at most 128 characters.');
export const nameSchema = (what: string) => z.string().trim().min(1, `Enter ${what}.`).max(100, 'Use at most 100 characters.');

/**
 * Maps API validation errors onto form fields. Returns a message for the form-level
 * banner when the error is not tied to a known field.
 */
export function applyServerErrors<F extends FieldValues>(err: unknown, setError: UseFormSetError<F>, fields: readonly Path<F>[]): string | null {
  if (err instanceof ApiError) {
    const byField = err.fieldErrors;
    let matched = false;
    for (const [key, message] of Object.entries(byField)) {
      if ((fields as readonly string[]).includes(key)) {
        setError(key as Path<F>, { type: 'server', message });
        matched = true;
      }
    }
    if (matched) return null;
  }
  return errorMessage(err);
}

/** Only allow same-origin relative paths as post-login redirects. */
export function safeRedirect(target: string | undefined): string {
  if (!target || !target.startsWith('/') || target.startsWith('//') || target.startsWith('/\\')) return '/';
  return target;
}
