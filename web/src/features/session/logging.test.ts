import { describe, expect, it } from 'vitest';
import type { Prescription, Session, SessionExercise, SessionSet } from '@/lib/api/types';
import { afterSetCompleted, firstIncompleteIndex, lastTimeHint, localTotals, metricsFor, plannedRows, prefill, restText, sessionProgress, targetText } from './logging';

function prescription(patch: Partial<Prescription> = {}): Prescription {
  return {
    sets: 3,
    goal: { type: 'reps', min: 8, max: 10 },
    load: { type: 'weight', value: 60 },
    restSeconds: 90,
    tempo: null,
    supersetGroup: null,
    notes: null,
    ...patch,
  };
}

function sessionSet(patch: Partial<SessionSet> = {}): SessionSet {
  return {
    id: 'set-1',
    exerciseId: 'exercise-1',
    position: 1,
    setNumber: 1,
    weightKg: 60,
    reps: 10,
    durationSeconds: null,
    distanceMeters: null,
    rpe: null,
    completed: true,
    notes: null,
    updatedAt: '2026-01-01T00:00:00Z',
    ...patch,
  };
}

function exercise(patch: Partial<SessionExercise> = {}): SessionExercise {
  return {
    position: 1,
    exerciseId: 'exercise-1',
    exercise: { id: 'exercise-1', slug: 'bench-press', name: 'Bench Press', illustration: null },
    prescription: prescription(),
    lastPerformance: null,
    sets: [],
    ...patch,
  };
}

function session(exercises: SessionExercise[]): Session {
  return {
    id: 'session-1',
    clientId: 'client-1',
    assignmentId: null,
    workoutName: 'Upper A',
    status: 'in-progress',
    startedAt: '2026-01-01T00:00:00Z',
    completedAt: null,
    notes: null,
    exercises,
    summary: { totalVolumeKg: 0, completedSets: 0, totalReps: 0, durationSeconds: null, personalRecords: [] },
  };
}

describe('session logging helpers', () => {
  it('chooses inputs for each goal and weighted load', () => {
    expect(metricsFor(null)).toEqual(['weight', 'reps']);
    expect(metricsFor(prescription())).toEqual(['weight', 'reps']);
    expect(metricsFor(prescription({ goal: { type: 'time', min: 30, max: 45 }, load: { type: 'bodyweight', value: null } }))).toEqual(['duration']);
    expect(metricsFor(prescription({ goal: { type: 'time', min: 30, max: 45 }, load: { type: 'bodyweight', value: 10 } }))).toEqual(['weight', 'duration']);
    expect(metricsFor(prescription({ goal: { type: 'distance', min: 40, max: 40 }, load: { type: 'none', value: null } }))).toEqual(['distance']);
    expect(metricsFor(prescription({ load: { type: 'percent-1rm', value: 80 } }))).toEqual(['weight', 'reps']);
    expect(metricsFor(prescription({ load: { type: 'rpe', value: 8 } }))).toEqual(['weight', 'reps']);
  });

  it('prefills from an earlier set and aims at the top of the goal range', () => {
    const e = exercise({ sets: [sessionSet({ setNumber: 1, weightKg: 62.5, reps: 8 })] });
    expect(prefill(e, 2)).toEqual({ weightKg: 62.5, reps: 10, durationSeconds: null, distanceMeters: null, rpe: null });
  });

  it('falls back to prescribed weight and last performance, but not for an unweighted goal', () => {
    const lastPerformance = { sessionId: 'old', date: '2025-12-01', sets: [{ setNumber: 1, weightKg: 55, reps: 9, rpe: 8 }] };
    expect(prefill(exercise({ lastPerformance }), 1)).toMatchObject({ weightKg: 60, reps: 10 });
    const unweighted = exercise({
      prescription: prescription({ goal: { type: 'distance', min: 40, max: 50 }, load: { type: 'none', value: null } }),
      lastPerformance,
    });
    expect(prefill(unweighted, 1)).toEqual({ weightKg: null, reps: null, durationSeconds: null, distanceMeters: 50, rpe: null });
  });

  it('derives row counts, targets, rest and last-time hints', () => {
    const e = exercise({
      sets: [sessionSet({ setNumber: 5 })],
      lastPerformance: { sessionId: 'old', date: '2025-12-01', sets: [{ setNumber: 2, weightKg: 55, reps: 9, rpe: 8 }] },
    });
    expect(plannedRows(e)).toBe(5);
    expect(plannedRows(exercise({ prescription: null, sets: [] }))).toBe(1);
    expect(targetText(e.prescription, 'kg')).toBe('3 × 8–10 reps @ 60 kg');
    expect(targetText(null, 'kg')).toBeNull();
    expect(restText(e.prescription)).toBe('Rest 1:30');
    expect(restText(prescription({ restSeconds: 0 }))).toBeNull();
    expect(lastTimeHint(e, 2, 'kg')).toBe('55 kg × 9 reps');
    expect(lastTimeHint(e, 1, 'kg')).toBeNull();
  });

  it('counts progress and local totals only from completed sets', () => {
    const first = exercise({ prescription: prescription({ sets: 2 }), sets: [sessionSet({ weightKg: 60, reps: 10 }), sessionSet({ id: 'set-2', setNumber: 2, completed: false, weightKg: 100, reps: 20 })] });
    const second = exercise({
      position: 2,
      exerciseId: 'exercise-2',
      prescription: prescription({ sets: 2 }),
      sets: [sessionSet({ id: 'set-3', position: 2, exerciseId: 'exercise-2', weightKg: 50, reps: 5 }), sessionSet({ id: 'set-4', position: 2, exerciseId: 'exercise-2', setNumber: 3, completed: false })],
    });
    const s = session([first, second]);
    expect(sessionProgress(s)).toEqual({ done: 2, planned: 5 });
    expect(localTotals(s)).toEqual({ totalVolumeKg: 850, completedSets: 2, totalReps: 15 });
  });

  it('moves through a superset before resting and returning to its first exercise', () => {
    const first = exercise({ prescription: prescription({ supersetGroup: 'A', sets: 2 }) });
    const second = exercise({ position: 2, exerciseId: 'exercise-2', prescription: prescription({ supersetGroup: 'A', sets: 2 }) });
    const third = exercise({ position: 3, exerciseId: 'exercise-3' });
    const s = session([first, second, third]);
    expect(afterSetCompleted(s, 0)).toEqual({ rest: false, nextIndex: 1 });
    expect(afterSetCompleted(s, 1)).toEqual({ rest: true, nextIndex: 0 });
    expect(afterSetCompleted(s, 2)).toEqual({ rest: true, nextIndex: 2 });
  });

  it('stays on the last superset exercise after every planned set is complete', () => {
    const done = [sessionSet({ setNumber: 1 }), sessionSet({ id: 'set-2', setNumber: 2 })];
    const first = exercise({ prescription: prescription({ supersetGroup: 'A', sets: 2 }), sets: done });
    const second = exercise({ position: 2, exerciseId: 'exercise-2', prescription: prescription({ supersetGroup: 'A', sets: 2 }), sets: done.map((x) => ({ ...x, position: 2, exerciseId: 'exercise-2' })) });
    expect(afterSetCompleted(session([first, second]), 1)).toEqual({ rest: true, nextIndex: 1 });
  });

  it('finds the first incomplete exercise and defaults to the first when all are done', () => {
    const complete = exercise({ prescription: prescription({ sets: 1 }), sets: [sessionSet()] });
    const incomplete = exercise({ position: 2, exerciseId: 'exercise-2', prescription: prescription({ sets: 1 }) });
    expect(firstIncompleteIndex(session([complete, incomplete]))).toBe(1);
    expect(firstIncompleteIndex(session([complete]))).toBe(0);
  });
});
