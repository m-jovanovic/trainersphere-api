import type { Prescription, Session, SessionExercise, SessionSet, Units } from '@/lib/api/types';
import { formatDuration, formatGoal, formatLoad, formatSetLine } from '@/lib/domain';

export type Metric = 'weight' | 'reps' | 'duration' | 'distance';

export interface SetDraft {
  weightKg: number | null;
  reps: number | null;
  durationSeconds: number | null;
  distanceMeters: number | null;
  rpe: number | null;
}

/** The inputs a set row shows: the goal's measure, plus weight when the load is an actual weight. */
export function metricsFor(p: Prescription | null): Metric[] {
  if (!p) return ['weight', 'reps'];
  const main: Metric = p.goal.type === 'reps' ? 'reps' : p.goal.type === 'time' ? 'duration' : 'distance';
  const weighted =
    p.load.type === 'weight' ||
    (p.load.type === 'bodyweight' && (p.load.value ?? 0) > 0) ||
    (p.goal.type === 'reps' && (p.load.type === 'percent-1rm' || p.load.type === 'rpe'));
  return weighted ? ['weight', main] : [main];
}

export function primaryMetric(p: Prescription | null): Exclude<Metric, 'weight'> {
  const m = metricsFor(p);
  return m[m.length - 1] as Exclude<Metric, 'weight'>;
}

const fieldOf = { weight: 'weightKg', reps: 'reps', duration: 'durationSeconds', distance: 'distanceMeters' } as const;

export function draftValue(d: SetDraft, m: Metric): number | null {
  return d[fieldOf[m]];
}

export function withValue(d: SetDraft, m: Metric, v: number | null): SetDraft {
  return { ...d, [fieldOf[m]]: v };
}

export function draftOf(s: SessionSet): SetDraft {
  return { weightKg: s.weightKg, reps: s.reps, durationSeconds: s.durationSeconds, distanceMeters: s.distanceMeters, rpe: s.rpe };
}

/**
 * Starting values for a set that has not been logged: the weight carries over from the previous
 * set of this session, else the trainer's prescribed weight, else last time's; the goal measure
 * aims at the top of the prescribed range.
 */
export function prefill(e: SessionExercise, setNumber: number): SetDraft {
  const p = e.prescription;
  const earlier = [...e.sets].filter((s) => s.setNumber < setNumber).sort((a, b) => b.setNumber - a.setNumber)[0];
  const last = e.lastPerformance?.sets.find((s) => s.setNumber === setNumber) ?? e.lastPerformance?.sets.at(-1);
  const metrics = metricsFor(p);

  let weightKg: number | null = null;
  if (metrics.includes('weight')) {
    const prescribed = p && (p.load.type === 'weight' || p.load.type === 'bodyweight') ? p.load.value : null;
    weightKg = earlier?.weightKg ?? prescribed ?? last?.weightKg ?? null;
  }
  const goalTop = p ? p.goal.max : null;
  const main = primaryMetric(p);
  return {
    weightKg,
    reps: main === 'reps' ? (goalTop ?? earlier?.reps ?? last?.reps ?? null) : null,
    durationSeconds: main === 'duration' ? (goalTop ?? earlier?.durationSeconds ?? null) : null,
    distanceMeters: main === 'distance' ? (goalTop ?? earlier?.distanceMeters ?? null) : null,
    rpe: null,
  };
}

/** Number of set rows: the plan, or more if more were logged; at least one. */
export function plannedRows(e: SessionExercise): number {
  const logged = e.sets.reduce((m, s) => Math.max(m, s.setNumber), 0);
  return Math.max(e.prescription?.sets ?? 1, logged, 1);
}

export function targetText(p: Prescription | null, units: Units): string | null {
  if (!p) return null;
  const load = formatLoad(p.load, units);
  return `${p.sets} × ${formatGoal(p.goal)}${load ? ` @ ${load}` : ''}`;
}

export function restText(p: Prescription | null): string | null {
  if (!p || p.restSeconds <= 0) return null;
  return `Rest ${formatDuration(p.restSeconds)}`;
}

export function lastTimeHint(e: SessionExercise, setNumber: number, units: Units): string | null {
  const s = e.lastPerformance?.sets.find((x) => x.setNumber === setNumber);
  return s ? formatSetLine(s, units) : null;
}

export function completedCount(e: SessionExercise): number {
  return e.sets.filter((s) => s.completed).length;
}

export function sessionProgress(s: Session): { done: number; planned: number } {
  let done = 0;
  let planned = 0;
  for (const e of s.exercises) {
    done += completedCount(e);
    planned += Math.max(plannedRows(e), completedCount(e));
  }
  return { done, planned };
}

/** Volume, sets and reps from what is on this device; the server's summary replaces it once synced. */
export function localTotals(s: Session): { totalVolumeKg: number; completedSets: number; totalReps: number } {
  let totalVolumeKg = 0;
  let completedSets = 0;
  let totalReps = 0;
  for (const e of s.exercises) {
    for (const x of e.sets) {
      if (!x.completed) continue;
      completedSets += 1;
      totalReps += x.reps ?? 0;
      totalVolumeKg += (x.weightKg ?? 0) * (x.reps ?? 0);
    }
  }
  return { totalVolumeKg: Math.round(totalVolumeKg * 100) / 100, completedSets, totalReps };
}

/**
 * Where to go after ticking a set. Inside a superset the next exercise of the group follows
 * without rest; after the group's last exercise the rest starts and the group begins again
 * while it has sets left.
 */
export function afterSetCompleted(s: Session, index: number): { rest: boolean; nextIndex: number } {
  const list = s.exercises;
  const e = list[index];
  const group = e?.prescription?.supersetGroup ?? null;
  if (!e || !group) return { rest: true, nextIndex: index };
  const members = list.map((x, i) => ({ x, i })).filter(({ x }) => x.prescription?.supersetGroup === group);
  const pos = members.findIndex((m) => m.i === index);
  const next = members[pos + 1];
  if (next) return { rest: false, nextIndex: next.i };
  const first = members[0];
  const groupHasSetsLeft = members.some(({ x }) => completedCount(x) < plannedRows(x));
  return { rest: true, nextIndex: first && groupHasSetsLeft ? first.i : index };
}

export function firstIncompleteIndex(s: Session): number {
  const i = s.exercises.findIndex((e) => completedCount(e) < plannedRows(e));
  return i < 0 ? 0 : i;
}
