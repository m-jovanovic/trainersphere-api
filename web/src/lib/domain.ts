import type { Goal, Load, PersonalRecord, PrType, Units } from '@/lib/api/types';

/** Epley estimate; the contract counts 1-12 rep sets and treats a single as the weight itself. */
export function estimateOneRepMax(weightKg: number, reps: number): number | null {
  if (reps < 1 || reps > 12 || weightKg <= 0) return null;
  if (reps === 1) return weightKg;
  return round2(weightKg * (1 + reps / 30));
}

export function round2(n: number): number {
  return Math.round(n * 100) / 100;
}

const KG_PER_LB = 0.45359237;

export function kgToUnits(kg: number, units: Units): number {
  return units === 'kg' ? kg : kg / KG_PER_LB;
}

export function unitsToKg(value: number, units: Units): number {
  return units === 'kg' ? value : round2(value * KG_PER_LB);
}

/** Display weight rounded to a sensible plate step for the unit. */
export function formatWeight(kg: number | null, units: Units, withUnit = true): string {
  if (kg === null) return '–';
  const v = kgToUnits(kg, units);
  const rounded = units === 'kg' ? Math.round(v * 4) / 4 : Math.round(v * 2) / 2;
  const text = String(Number(rounded.toFixed(2)));
  return withUnit ? `${text} ${units}` : text;
}

export function formatVolume(kg: number, units: Units): string {
  const v = Math.round(kgToUnits(kg, units));
  return `${v.toLocaleString('en-US')} ${units}`;
}

export function formatPercent(rate: number | null): string {
  return rate === null ? '–' : `${Math.round(rate * 100)}%`;
}

export function weightStep(units: Units): number {
  return units === 'kg' ? 2.5 : 5;
}

export function formatGoal(goal: Goal): string {
  const unit = goal.type === 'reps' ? '' : goal.type === 'time' ? 's' : 'm';
  const range = goal.min === goal.max ? `${goal.min}` : `${goal.min}–${goal.max}`;
  if (goal.type === 'time' && goal.max >= 60) {
    const fmt = (s: number) => formatDuration(s);
    return goal.min === goal.max ? fmt(goal.min) : `${fmt(goal.min)}–${fmt(goal.max)}`;
  }
  return goal.type === 'reps' ? `${range} reps` : `${range}${unit}`;
}

export function formatLoad(load: Load, units: Units): string | null {
  switch (load.type) {
    case 'none':
      return null;
    case 'bodyweight':
      return load.value ? `BW + ${formatWeight(load.value, units)}` : 'Bodyweight';
    case 'weight':
      return load.value === null ? null : formatWeight(load.value, units);
    case 'percent-1rm':
      return load.value === null ? null : `${load.value}% 1RM`;
    case 'rpe':
      return load.value === null ? null : `RPE ${load.value}`;
  }
}

export function formatDuration(totalSeconds: number): string {
  const s = Math.max(0, Math.round(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
  return `${m}:${String(sec).padStart(2, '0')}`;
}

export function fullName(p: { firstName: string; lastName: string }): string {
  return `${p.firstName} ${p.lastName}`.trim();
}

export function initials(p: { firstName: string; lastName: string }): string {
  return `${p.firstName.charAt(0)}${p.lastName.charAt(0)}`.toUpperCase();
}

export function humanize(value: string): string {
  const s = value.replace(/-/g, ' ');
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export const prLabels: Record<PrType, string> = {
  'estimated-1rm': 'Estimated 1RM',
  'max-weight': 'Heaviest set',
  'max-reps': 'Most reps',
  'max-volume': 'Session volume',
};

export const prShortLabels: Record<PrType, string> = {
  'estimated-1rm': 'e1RM',
  'max-weight': 'heaviest set',
  'max-reps': 'most reps',
  'max-volume': 'volume',
};

/** Which record leads when one exercise sets several in a session: the most tangible first. */
const prPriority: PrType[] = ['max-weight', 'estimated-1rm', 'max-reps', 'max-volume'];

export interface PrGroup<T extends PersonalRecord = PersonalRecord> {
  key: string;
  primary: T;
  others: T[];
}

/** One entry per exercise per session, keeping the input order (newest first from the API). */
export function groupPersonalRecords<T extends PersonalRecord>(prs: T[]): PrGroup<T>[] {
  const groups = new Map<string, T[]>();
  for (const pr of prs) {
    const key = `${pr.sessionId}|${pr.exerciseId}`;
    groups.set(key, [...(groups.get(key) ?? []), pr]);
  }
  return [...groups.entries()].map(([key, list]) => {
    const primary = list.reduce((best, p) => (prPriority.indexOf(p.type) < prPriority.indexOf(best.type) ? p : best));
    return { key, primary, others: list.filter((p) => p !== primary).sort((a, b) => prPriority.indexOf(a.type) - prPriority.indexOf(b.type)) };
  });
}

export function prGroupLabel(g: PrGroup): string {
  const also = g.others.map((o) => prShortLabels[o.type]).join(', ');
  return also ? `${prLabels[g.primary.type]} · also ${also}` : prLabels[g.primary.type];
}

/** Headline value of a PR in the user's units. */
export function formatPrValue(pr: Pick<PersonalRecord, 'type' | 'value'>, units: Units): string {
  switch (pr.type) {
    case 'max-reps':
      return `${pr.value} reps`;
    case 'max-volume':
      return formatVolume(pr.value, units);
    default:
      return formatWeight(pr.value, units);
  }
}

/** "+2.5 kg" style improvement over the previous best. */
export function formatPrDelta(pr: Pick<PersonalRecord, 'type' | 'value' | 'previousValue'>, units: Units): string {
  const diff = pr.value - pr.previousValue;
  if (pr.type === 'max-reps') return `+${diff} reps`;
  if (pr.type === 'max-volume') return `+${formatVolume(diff, units)}`;
  return `+${formatWeight(diff, units)}`;
}

export function formatSetLine(s: { weightKg: number | null; reps: number | null; durationSeconds?: number | null; distanceMeters?: number | null }, units: Units): string {
  const parts: string[] = [];
  if (s.weightKg !== null && s.weightKg > 0) parts.push(formatWeight(s.weightKg, units));
  if (s.reps !== null) parts.push(`${s.reps} reps`);
  if (s.durationSeconds != null) parts.push(formatDuration(s.durationSeconds));
  if (s.distanceMeters != null) parts.push(`${s.distanceMeters} m`);
  return parts.length ? parts.join(' × ') : '–';
}

export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    return 'UTC';
  }
}
