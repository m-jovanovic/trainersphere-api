import { describe, expect, it } from 'vitest';
import type { PersonalRecord, PrType } from '@/lib/api/types';
import { formatEstimate, formatPrDelta, formatPrExact, formatPrValue, groupPersonalRecords, prGroupLabel, prHeading } from './domain';

function pr(type: PrType, value: number, previousValue: number, exerciseId = 'leg-press', sessionId = 's1'): PersonalRecord {
  return {
    id: `${exerciseId}-${type}`,
    clientId: 'c1',
    exerciseId,
    exercise: { id: exerciseId, slug: exerciseId, name: exerciseId, illustration: null },
    type,
    value,
    previousValue,
    weightKg: null,
    reps: null,
    sessionId,
    achievedAt: '2026-11-16T17:30:00Z',
  };
}

describe('estimated 1RM display', () => {
  it('rounds to the nearest 0.5 kg or 1 lb', () => {
    expect(formatPrValue(pr('estimated-1rm', 109.25, 100), 'kg')).toBe('109.5 kg');
    expect(formatPrValue(pr('estimated-1rm', 30.75, 20), 'kg')).toBe('31 kg');
    expect(formatPrValue(pr('estimated-1rm', 101.33, 98.6), 'kg')).toBe('101.5 kg');
    expect(formatPrValue(pr('estimated-1rm', 109.25, 100), 'lb')).toBe('241 lb');
    expect(formatEstimate(null, 'kg')).toBe('–');
  });

  it('keeps measured records on their own steps', () => {
    expect(formatPrValue(pr('max-weight', 22.5, 20), 'kg')).toBe('22.5 kg');
    expect(formatPrValue(pr('max-weight', 61.25, 60), 'kg')).toBe('61.25 kg');
    expect(formatPrValue(pr('max-reps', 12, 10), 'kg')).toBe('12 reps');
  });

  it('shows the exact estimate for tooltips only', () => {
    expect(formatPrExact(pr('estimated-1rm', 109.25, 100), 'kg')).toBe('Estimated 109.25 kg');
    expect(formatPrExact(pr('max-weight', 80, 75), 'kg')).toBeUndefined();
  });

  it('derives the delta from the rounded values and never shows +0', () => {
    expect(formatPrDelta(pr('estimated-1rm', 109.25, 103.1), 'kg')).toBe('+6.5 kg');
    // 101.5 shown against 98.5 shown, not the raw 2.73.
    expect(formatPrDelta(pr('estimated-1rm', 101.33, 98.6), 'kg')).toBe('+3 kg');
    expect(formatPrDelta(pr('estimated-1rm', 100.2, 100.1), 'kg')).toBe('+0.1 kg');
  });
});

describe('session record grouping', () => {
  const records = [
    pr('max-volume', 2560, 2400),
    pr('estimated-1rm', 109.25, 103.1),
    pr('max-weight', 80, 75),
    pr('max-weight', 40, 35, 'calf-raise'),
    pr('max-volume', 1640, 1435, 'calf-raise'),
  ];

  it('makes one entry per exercise led by the heaviest set', () => {
    const groups = groupPersonalRecords(records);
    expect(groups).toHaveLength(2);
    expect(groups[0]!.primary.type).toBe('max-weight');
    expect(prGroupLabel(groups[0]!)).toBe('Heaviest set · also e1RM, volume');
    expect(prGroupLabel(groups[1]!)).toBe('Heaviest set · also volume');
  });

  it('counts exercises, not records, in the heading', () => {
    expect(prHeading(records)).toBe('PRs on 2 exercises');
    expect(prHeading(records.slice(0, 3), 'New PRs')).toBe('New PRs on 1 exercise');
  });
});
