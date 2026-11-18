import { describe, expect, it } from 'vitest';
import { formatAgo } from './dates';

describe('formatAgo', () => {
  it('renders past UTC timestamps relative to now', () => {
    expect(formatAgo(new Date(Date.now() - 3_600_000).toISOString())).toBe('1 hour ago');
    expect(formatAgo(new Date(Date.now() - 26 * 3_600_000).toISOString())).toBe('1 day ago');
  });

  it('reads timestamps slightly ahead of the device clock as "just now"', () => {
    const now = Date.now();
    expect(formatAgo(new Date(now + 60_000).toISOString(), 'Never', now)).toBe('just now');
  });

  it('keeps the suffix for timestamps far in the future', () => {
    expect(formatAgo(new Date(Date.now() + 26 * 3_600_000).toISOString())).toBe('in 1 day');
  });

  it('falls back for null', () => {
    expect(formatAgo(null)).toBe('Never');
  });
});
