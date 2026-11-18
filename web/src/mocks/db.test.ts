import { describe, expect, it } from 'vitest';
import { db, seed, seedSessionTimes } from '@/mocks/db';
import { todayIso } from '@/lib/dates';

describe('seeded times', () => {
  it('never puts a session start or end in the future, whatever the hour', () => {
    const today = todayIso();
    for (const hour of [0, 5, 11, 17, 23]) {
      const { start, end } = seedSessionTimes(today, today, hour, 45, 60);
      expect(Date.parse(end)).toBeLessThanOrEqual(Date.now());
      expect(Date.parse(start)).toBeLessThanOrEqual(Date.parse(end));
    }
  });

  it('seeds no session or body metric timestamps in the future', () => {
    seed();
    const now = Date.now();
    expect(db.state.sessions.length).toBeGreaterThan(0);
    for (const s of db.state.sessions) {
      expect(Date.parse(s.startedAt)).toBeLessThanOrEqual(now);
      if (s.completedAt) expect(Date.parse(s.completedAt)).toBeLessThanOrEqual(now);
    }
    for (const m of db.state.bodyMetrics) expect(Date.parse(m.updatedAt)).toBeLessThanOrEqual(now);
  });
});
