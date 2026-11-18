import type * as T from '@/lib/api/types';
import { addDaysIso, todayIso, weekStartIso } from '@/lib/dates';
import { estimateOneRepMax, round2 } from '@/lib/domain';
import { seedExercises } from './seed-exercises';

// In-memory model of the backend described in api/contract.md. Used by MSW in dev and tests.

export interface DbUser extends T.User {
  password: string;
}
export interface DbClient {
  id: string;
  trainerId: string;
  status: T.ClientStatus;
  joinedAt: string;
  goals: string | null;
  trainerNotes: string | null;
}
export interface DbInvitation extends T.Invitation {
  token: string;
  trainerId: string;
}
export interface DbExercise extends T.Exercise {
  ownerId: string | null;
}
export interface DbWorkoutExercise extends T.Prescription {
  id: string;
  exerciseId: string;
}
export interface DbWorkout {
  id: string;
  trainerId: string;
  name: string;
  description: string | null;
  archived: boolean;
  createdAt: string;
  updatedAt: string;
  exercises: DbWorkoutExercise[];
}
export interface DbProgram {
  id: string;
  trainerId: string;
  name: string;
  description: string | null;
  weeks: number;
  archived: boolean;
  createdAt: string;
  updatedAt: string;
  slots: { id: string; week: number; day: number; workoutId: string }[];
}
export interface DbAssignment {
  id: string;
  clientId: string;
  date: string;
  status: 'scheduled' | 'completed' | 'skipped';
  workoutId: string;
  snapshot: { name: string; description: string | null; exercises: T.WorkoutExercise[] };
  programAssignmentId: string | null;
  notes: string | null;
  sessionId: string | null;
  completedAt: string | null;
  createdAt: string;
}
export interface DbSession {
  id: string;
  clientId: string;
  assignmentId: string | null;
  workoutName: string;
  status: T.SessionStatus;
  startedAt: string;
  completedAt: string | null;
  notes: string | null;
  plan: { position: number; exerciseId: string; prescription: T.Prescription }[];
  sets: T.SessionSet[];
  personalRecords: T.PersonalRecord[];
}

export interface DbState {
  seededOn: string;
  users: DbUser[];
  clients: DbClient[];
  invitations: DbInvitation[];
  exercises: DbExercise[];
  workouts: DbWorkout[];
  programs: DbProgram[];
  programAssignments: T.ProgramAssignment[];
  assignments: DbAssignment[];
  sessions: DbSession[];
  personalRecords: T.PersonalRecord[];
  /** Best value per `${clientId}|${exerciseId}|${type}`; set by the first log (baseline). */
  bests: Record<string, number>;
  bodyMetrics: T.BodyMetric[];
  refreshTokens: Record<string, string>;
  resetTokens: Record<string, string>;
}

let counter = 0;
export function newId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  counter += 1;
  return `00000000-0000-7000-8000-${String(counter).padStart(12, '0')}`;
}

function seedId(prefix: string, n: number): string {
  return `0193f${prefix}-0000-7000-8000-${String(n).padStart(12, '0')}`;
}

export const DEMO_PASSWORD = 'Demo123!';
export const TRAINER_EMAIL = 'coach@trainersphere.dev';

export const db: { state: DbState } = { state: emptyState() };

function emptyState(): DbState {
  return {
    seededOn: todayIso(),
    users: [],
    clients: [],
    invitations: [],
    exercises: [],
    workouts: [],
    programs: [],
    programAssignments: [],
    assignments: [],
    sessions: [],
    personalRecords: [],
    bests: {},
    bodyMetrics: [],
    refreshTokens: {},
    resetTokens: {},
  };
}

// ---- Projections ------------------------------------------------------------

export const nowIso = () => new Date().toISOString().replace(/\.\d{3}Z$/, 'Z');

export function exerciseRef(id: string): T.ExerciseRef {
  const e = db.state.exercises.find((x) => x.id === id);
  if (!e) return { id, slug: 'unknown', name: 'Unknown exercise', illustration: null };
  return { id: e.id, slug: e.slug, name: e.name, illustration: e.illustration };
}

export function toExercise(e: DbExercise): T.Exercise {
  const { ownerId: _ownerId, ...rest } = e;
  return rest;
}

function toWorkoutExercises(list: DbWorkoutExercise[]): T.WorkoutExercise[] {
  return list.map((x, i) => ({ ...x, position: i + 1, exercise: exerciseRef(x.exerciseId) }));
}

export function toWorkout(w: DbWorkout): T.Workout {
  const { trainerId: _t, ...rest } = w;
  return { ...rest, exercises: toWorkoutExercises(w.exercises) };
}

export function toWorkoutSummary(w: DbWorkout): T.WorkoutSummary {
  return { id: w.id, name: w.name, description: w.description, exerciseCount: w.exercises.length, archived: w.archived, updatedAt: w.updatedAt };
}

export function toProgram(p: DbProgram): T.Program {
  const { trainerId: _t, ...rest } = p;
  const slots = [...p.slots]
    .sort((a, b) => a.week - b.week || a.day - b.day)
    .map((s) => ({ ...s, workout: { id: s.workoutId, name: db.state.workouts.find((w) => w.id === s.workoutId)?.name ?? 'Workout' } }));
  return { ...rest, slots };
}

export function toProgramSummary(p: DbProgram): T.ProgramSummary {
  return {
    id: p.id,
    name: p.name,
    description: p.description,
    weeks: p.weeks,
    workoutCount: p.slots.length,
    archived: p.archived,
    updatedAt: p.updatedAt,
  };
}

export function personRef(id: string): T.PersonRef {
  const u = db.state.users.find((x) => x.id === id);
  return { id, firstName: u?.firstName ?? '', lastName: u?.lastName ?? '' };
}

export function assignmentStatus(a: DbAssignment, today = todayIso()): T.AssignmentStatus {
  return a.status === 'scheduled' && a.date < today ? 'missed' : a.status;
}

export function toAssignment(a: DbAssignment): T.Assignment {
  const pa = a.programAssignmentId ? db.state.programAssignments.find((p) => p.id === a.programAssignmentId) : null;
  return {
    id: a.id,
    clientId: a.clientId,
    client: personRef(a.clientId),
    date: a.date,
    status: assignmentStatus(a),
    workoutId: a.workoutId,
    workout: a.snapshot,
    programAssignmentId: a.programAssignmentId,
    programName: pa?.programName ?? null,
    notes: a.notes,
    sessionId: a.sessionId,
    completedAt: a.completedAt,
    createdAt: a.createdAt,
  };
}

function completedSets(s: DbSession) {
  return s.sets.filter((x) => x.completed);
}

export function sessionTotals(s: DbSession): T.SessionTotals {
  const done = completedSets(s);
  return {
    totalVolumeKg: round2(done.reduce((sum, x) => sum + (x.weightKg ?? 0) * (x.reps ?? 0), 0)),
    completedSets: done.length,
    totalReps: done.reduce((sum, x) => sum + (x.reps ?? 0), 0),
    durationSeconds: s.completedAt ? Math.round((Date.parse(s.completedAt) - Date.parse(s.startedAt)) / 1000) : null,
    personalRecords: s.personalRecords,
  };
}

export function toSessionSummary(s: DbSession): T.SessionSummary {
  const t = sessionTotals(s);
  return {
    id: s.id,
    clientId: s.clientId,
    assignmentId: s.assignmentId,
    workoutName: s.workoutName,
    status: s.status,
    startedAt: s.startedAt,
    completedAt: s.completedAt,
    totalVolumeKg: t.totalVolumeKg,
    completedSets: t.completedSets,
    personalRecordCount: s.personalRecords.length,
  };
}

function lastPerformance(s: DbSession, exerciseId: string): T.LastPerformance | null {
  const prev = db.state.sessions
    .filter((x) => x.clientId === s.clientId && x.status === 'completed' && x.id !== s.id && x.startedAt < s.startedAt)
    .filter((x) => x.sets.some((y) => y.exerciseId === exerciseId && y.completed))
    .sort((a, b) => b.startedAt.localeCompare(a.startedAt))[0];
  if (!prev) return null;
  return {
    sessionId: prev.id,
    date: (prev.completedAt ?? prev.startedAt).slice(0, 10),
    sets: prev.sets
      .filter((y) => y.exerciseId === exerciseId && y.completed)
      .sort((a, b) => a.setNumber - b.setNumber)
      .map((y) => ({ setNumber: y.setNumber, weightKg: y.weightKg, reps: y.reps, rpe: y.rpe })),
  };
}

export function toSession(s: DbSession): T.Session {
  const positions = new Map<number, T.SessionExercise>();
  for (const p of s.plan) {
    positions.set(p.position, {
      position: p.position,
      exerciseId: p.exerciseId,
      exercise: exerciseRef(p.exerciseId),
      prescription: p.prescription,
      lastPerformance: lastPerformance(s, p.exerciseId),
      sets: [],
    });
  }
  for (const set of s.sets) {
    let ex = positions.get(set.position);
    if (!ex) {
      ex = {
        position: set.position,
        exerciseId: set.exerciseId,
        exercise: exerciseRef(set.exerciseId),
        prescription: null,
        lastPerformance: lastPerformance(s, set.exerciseId),
        sets: [],
      };
      positions.set(set.position, ex);
    }
    ex.sets.push(set);
  }
  const exercises = [...positions.values()].sort((a, b) => a.position - b.position);
  for (const e of exercises) e.sets.sort((a, b) => a.setNumber - b.setNumber);
  return {
    id: s.id,
    clientId: s.clientId,
    assignmentId: s.assignmentId,
    workoutName: s.workoutName,
    status: s.status,
    startedAt: s.startedAt,
    completedAt: s.completedAt,
    notes: s.notes,
    exercises,
    summary: sessionTotals(s),
  };
}

export function toInvitation(i: DbInvitation): T.Invitation {
  const { token: _token, trainerId: _t, ...rest } = i;
  const expired = rest.status === 'pending' && rest.expiresAt < nowIso();
  return { ...rest, status: expired ? 'expired' : rest.status };
}

// ---- Rules ------------------------------------------------------------------

export function adherence(clientId: string, days: number, today = todayIso()) {
  const from = addDaysIso(today, -(days - 1));
  const due = db.state.assignments.filter(
    (a) => a.clientId === clientId && a.date >= from && a.date <= today && !(a.date === today && a.status === 'scheduled'),
  );
  const completed = due.filter((a) => a.status === 'completed').length;
  return { due: due.length, completed, rate: due.length === 0 ? null : round2(completed / due.length) };
}

export function streakWeeks(clientId: string, today = todayIso()): number {
  const weeks = new Set(
    db.state.sessions
      .filter((s) => s.clientId === clientId && s.status === 'completed' && s.completedAt)
      .map((s) => weekStartIso((s.completedAt ?? s.startedAt).slice(0, 10))),
  );
  let week = weekStartIso(today);
  let streak = 0;
  if (!weeks.has(week)) week = addDaysIso(week, -7);
  while (weeks.has(week)) {
    streak += 1;
    week = addDaysIso(week, -7);
  }
  return streak;
}

/** Records PRs for a session being completed. The first log of an exercise only sets the baseline. */
export function computePersonalRecords(s: DbSession): T.PersonalRecord[] {
  const out: T.PersonalRecord[] = [];
  const byExercise = new Map<string, T.SessionSet[]>();
  for (const set of completedSets(s)) {
    const list = byExercise.get(set.exerciseId) ?? [];
    list.push(set);
    byExercise.set(set.exerciseId, list);
  }
  const achievedAt = s.completedAt ?? nowIso();
  for (const [exerciseId, sets] of byExercise) {
    const candidates: { type: T.PrType; value: number; set: T.SessionSet | null }[] = [];
    let bestE1rm: { value: number; set: T.SessionSet } | null = null;
    let heaviest: T.SessionSet | null = null;
    for (const set of sets) {
      const w = set.weightKg ?? 0;
      const e = estimateOneRepMax(w, set.reps ?? 0);
      if (e !== null && (!bestE1rm || e > bestE1rm.value)) bestE1rm = { value: e, set };
      if (w > 0 && (!heaviest || w > (heaviest.weightKg ?? 0))) heaviest = set;
    }
    if (bestE1rm) candidates.push({ type: 'estimated-1rm', value: bestE1rm.value, set: bestE1rm.set });
    if (heaviest) candidates.push({ type: 'max-weight', value: heaviest.weightKg ?? 0, set: heaviest });
    const mostReps = [...sets].sort((a, b) => (b.reps ?? 0) - (a.reps ?? 0))[0];
    if (mostReps && (mostReps.reps ?? 0) > 0) candidates.push({ type: 'max-reps', value: mostReps.reps ?? 0, set: mostReps });
    const volume = round2(sets.reduce((sum, x) => sum + (x.weightKg ?? 0) * (x.reps ?? 0), 0));
    if (volume > 0) candidates.push({ type: 'max-volume', value: volume, set: null });

    for (const c of candidates) {
      const key = `${s.clientId}|${exerciseId}|${c.type}`;
      const prev = db.state.bests[key];
      if (prev === undefined) {
        db.state.bests[key] = c.value;
        continue;
      }
      if (c.value <= prev) continue;
      db.state.bests[key] = c.value;
      out.push({
        id: newId(),
        clientId: s.clientId,
        exerciseId,
        exercise: exerciseRef(exerciseId),
        type: c.type,
        value: c.value,
        previousValue: prev,
        weightKg: c.set?.weightKg ?? null,
        reps: c.set?.reps ?? null,
        sessionId: s.id,
        achievedAt,
      });
    }
  }
  return out;
}

export function completeSession(s: DbSession, completedAt: string, notes?: string | null) {
  if (s.status === 'completed') return;
  s.status = 'completed';
  s.completedAt = completedAt;
  if (notes !== undefined) s.notes = notes;
  s.personalRecords = computePersonalRecords(s);
  db.state.personalRecords.push(...s.personalRecords);
  if (s.assignmentId) {
    const a = db.state.assignments.find((x) => x.id === s.assignmentId);
    if (a) {
      a.status = 'completed';
      a.completedAt = completedAt;
    }
  }
}

export function snapshotWorkout(w: DbWorkout): DbAssignment['snapshot'] {
  return { name: w.name, description: w.description, exercises: toWorkoutExercises(w.exercises) };
}

export function planFromSnapshot(snap: DbAssignment['snapshot']): DbSession['plan'] {
  return snap.exercises.map((e) => ({
    position: e.position,
    exerciseId: e.exerciseId,
    prescription: {
      sets: e.sets,
      goal: e.goal,
      load: e.load,
      restSeconds: e.restSeconds,
      tempo: e.tempo,
      supersetGroup: e.supersetGroup,
      notes: e.notes,
    },
  }));
}

// ---- Seed -------------------------------------------------------------------

function rng(seed: number) {
  let a = seed;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const plate = (kg: number) => Math.round(kg / 2.5) * 2.5;

function rx(sets: number, min: number, max: number, load: T.Load, rest: number, extra: Partial<T.Prescription> = {}): T.Prescription {
  return { sets, goal: { type: 'reps', min, max }, load, restSeconds: rest, tempo: null, supersetGroup: null, notes: null, ...extra };
}

export function seed(today = todayIso()): DbState {
  const state = emptyState();
  state.seededOn = today;
  db.state = state;
  const created = `${addDaysIso(today, -60)}T09:00:00Z`;

  state.exercises = seedExercises.map((e, i) => ({
    ...e,
    id: seedId('100', i + 1),
    isCustom: false,
    archived: false,
    ownerId: null,
  }));
  const ex = (slug: string) => {
    const found = state.exercises.find((e) => e.slug === slug);
    if (!found) throw new Error(`seed exercise ${slug} missing`);
    return found.id;
  };

  const trainer: DbUser = {
    id: seedId('1a2', 1),
    email: TRAINER_EMAIL,
    password: DEMO_PASSWORD,
    firstName: 'Maya',
    lastName: 'Novak',
    role: 'trainer',
    trainerId: null,
    units: 'kg',
    timeZone: 'Europe/Belgrade',
    createdAt: created,
  };
  state.users.push(trainer);

  // Custom exercise owned by the trainer, no art.
  state.exercises.push({
    id: seedId('100', 900),
    slug: 'landmine-press',
    name: 'Landmine Press',
    category: 'strength',
    movementPattern: 'vertical-push',
    primaryMuscles: ['front-delts'],
    secondaryMuscles: ['chest', 'triceps'],
    equipment: ['barbell'],
    unilateral: true,
    cue: 'Stagger stance, press up and across, finish with the arm by the ear.',
    illustration: null,
    isCustom: true,
    archived: false,
    ownerId: trainer.id,
  });

  const kg = (v: number): T.Load => ({ type: 'weight', value: v });
  const bw: T.Load = { type: 'bodyweight', value: null };
  const workoutDefs: { name: string; description: string; items: [string, T.Prescription][] }[] = [
    {
      name: 'Lower A',
      description: 'Squat focus, moderate volume.',
      items: [
        ['barbell-back-squat', rx(4, 6, 8, kg(80), 150, { tempo: '3-1-1-0', notes: 'Belt on top sets.' })],
        ['barbell-romanian-deadlift', rx(3, 8, 10, kg(70), 120)],
        ['dumbbell-lunge', rx(3, 10, 12, kg(16), 90)],
        ['standing-calf-raise', rx(3, 12, 15, kg(60), 60)],
        ['plank', { sets: 3, goal: { type: 'time', min: 45, max: 45 }, load: bw, restSeconds: 60, tempo: null, supersetGroup: null, notes: null }],
      ],
    },
    {
      name: 'Upper A',
      description: 'Horizontal push and pull.',
      items: [
        ['barbell-bench-press', rx(4, 5, 7, kg(60), 150)],
        ['barbell-bent-over-row', rx(4, 8, 10, kg(55), 120)],
        ['incline-dumbbell-press', rx(3, 10, 12, kg(20), 90, { supersetGroup: 'A' })],
        ['cable-face-pull', rx(3, 12, 15, kg(15), 60, { supersetGroup: 'A' })],
        ['cable-triceps-pushdown', rx(2, 12, 15, kg(20), 60)],
      ],
    },
    {
      name: 'Lower B',
      description: 'Hinge focus.',
      items: [
        ['barbell-deadlift', rx(3, 4, 6, kg(100), 180)],
        ['bulgarian-split-squat', rx(3, 8, 10, kg(14), 90)],
        ['lying-leg-curl', rx(3, 10, 12, kg(35), 60)],
        ['hanging-leg-raise', rx(3, 10, 15, bw, 60)],
      ],
    },
    {
      name: 'Upper B',
      description: 'Vertical push and pull.',
      items: [
        ['barbell-overhead-press', rx(4, 5, 7, kg(40), 150)],
        ['pull-up', rx(4, 5, 8, bw, 120)],
        ['seated-dumbbell-shoulder-press', rx(3, 8, 10, kg(16), 90)],
        ['lat-pulldown', rx(3, 10, 12, kg(50), 90)],
        ['dumbbell-hammer-curl', rx(2, 10, 12, kg(12), 60)],
      ],
    },
    {
      name: 'Full Body Conditioning',
      description: 'Circuit-style, keep rest short.',
      items: [
        ['kettlebell-swing', rx(4, 15, 15, kg(20), 45)],
        ['goblet-squat', rx(3, 12, 12, kg(20), 45)],
        ['push-up', rx(3, 10, 15, bw, 45)],
        ['farmers-carry', { sets: 3, goal: { type: 'distance', min: 40, max: 40 }, load: kg(28), restSeconds: 60, tempo: null, supersetGroup: null, notes: null }],
        ['rowing-machine', { sets: 1, goal: { type: 'time', min: 600, max: 600 }, load: { type: 'none', value: null }, restSeconds: 0, tempo: null, supersetGroup: null, notes: 'Steady, conversational pace.' }],
      ],
    },
  ];
  state.workouts = workoutDefs.map((w, i) => ({
    id: seedId('200', i + 1),
    trainerId: trainer.id,
    name: w.name,
    description: w.description,
    archived: false,
    createdAt: created,
    updatedAt: `${addDaysIso(today, -10 + i)}T10:00:00Z`,
    exercises: w.items.map(([slug, p], j) => ({ id: seedId('200', (i + 1) * 100 + j), exerciseId: ex(slug), ...p })),
  }));
  const [lowerA, upperA, lowerB, upperB, fullBody] = state.workouts as [DbWorkout, DbWorkout, DbWorkout, DbWorkout, DbWorkout];

  const program: DbProgram = {
    id: seedId('300', 1),
    trainerId: trainer.id,
    name: 'Strength Base (4 weeks)',
    description: '3 full-body sessions per week.',
    weeks: 4,
    archived: false,
    createdAt: created,
    updatedAt: created,
    slots: [],
  };
  for (let w = 1; w <= 4; w++) {
    const rotation = w % 2 === 1 ? [lowerA, upperA, lowerB] : [upperB, lowerA, upperA];
    [1, 3, 5].forEach((day, i) => {
      program.slots.push({ id: seedId('300', w * 10 + day), week: w, day, workoutId: (rotation[i] as DbWorkout).id });
    });
  }
  state.programs.push(program, {
    id: seedId('300', 2),
    trainerId: trainer.id,
    name: 'Conditioning Block',
    description: 'Two conditioning days a week for general fitness.',
    weeks: 2,
    archived: false,
    createdAt: created,
    updatedAt: created,
    slots: [
      { id: seedId('300', 201), week: 1, day: 2, workoutId: fullBody.id },
      { id: seedId('300', 202), week: 1, day: 5, workoutId: fullBody.id },
      { id: seedId('300', 203), week: 2, day: 2, workoutId: fullBody.id },
      { id: seedId('300', 204), week: 2, day: 5, workoutId: fullBody.id },
    ],
  });

  const clientDefs = [
    { first: 'Alex', last: 'Rivera', email: 'alex@trainersphere.dev', rate: 0.92, strength: 1.0, weight: 82, goals: 'Squat 120 kg by March.', notes: 'Left knee: avoid deep lunges.' },
    { first: 'Sam', last: 'Lee', email: 'sam@trainersphere.dev', rate: 0.5, strength: 0.75, weight: 68, goals: 'Build consistency, 3 sessions a week.', notes: 'Travels for work most weeks.' },
    { first: 'Jordan', last: 'Patel', email: 'jordan@trainersphere.dev', rate: 0.78, strength: 1.15, weight: 91, goals: 'Drop to 85 kg while keeping strength.', notes: null },
  ];
  const r = rng(42);
  const rotation = [lowerA, upperA, lowerB, upperB, fullBody];
  const programStart = addDaysIso(weekStartIso(today), -7);

  clientDefs.forEach((c, ci) => {
    const id = seedId('1a2', 10 + ci);
    state.users.push({
      id,
      email: c.email,
      password: DEMO_PASSWORD,
      firstName: c.first,
      lastName: c.last,
      role: 'client',
      trainerId: trainer.id,
      units: ci === 1 ? 'lb' : 'kg',
      timeZone: 'Europe/Belgrade',
      createdAt: `${addDaysIso(today, -50)}T12:00:00Z`,
    });
    state.clients.push({ id, trainerId: trainer.id, status: 'active', joinedAt: `${addDaysIso(today, -50)}T12:00:00Z`, goals: c.goals, trainerNotes: c.notes });
    state.invitations.push({
      id: seedId('1b0', 10 + ci),
      email: c.email,
      firstName: c.first,
      lastName: c.last,
      message: null,
      status: 'accepted',
      createdAt: `${addDaysIso(today, -51)}T12:00:00Z`,
      expiresAt: `${addDaysIso(today, -44)}T12:00:00Z`,
      acceptedAt: `${addDaysIso(today, -50)}T12:00:00Z`,
      clientId: id,
      token: `seed-accepted-${ci}`,
      trainerId: trainer.id,
    });

    const pa: T.ProgramAssignment = {
      id: seedId('310', ci + 1),
      programId: program.id,
      programName: program.name,
      clientId: id,
      startDate: programStart,
      endDate: addDaysIso(programStart, 27),
      assignmentCount: program.slots.length,
      createdAt: `${addDaysIso(programStart, -2)}T10:00:00Z`,
    };
    state.programAssignments.push(pa);

    const dates: { date: string; workout: DbWorkout; pa: string | null }[] = [];
    let k = ci;
    for (let d = -42; d < -7 - 0; d++) {
      const date = addDaysIso(today, d);
      if (date >= programStart) break;
      const dow = new Date(`${date}T12:00:00`).getDay();
      if ([1, 3, 5].includes(dow)) dates.push({ date, workout: rotation[k++ % rotation.length] as DbWorkout, pa: null });
    }
    for (const slot of program.slots) {
      const date = addDaysIso(programStart, (slot.week - 1) * 7 + slot.day - 1);
      dates.push({ date, workout: state.workouts.find((w) => w.id === slot.workoutId) as DbWorkout, pa: pa.id });
    }
    // Everyone trains today so both dashboards have something to show.
    if (!dates.some((d) => d.date === today)) dates.push({ date: today, workout: ci === 1 ? fullBody : upperA, pa: null });
    dates.sort((a, b) => a.date.localeCompare(b.date));

    let sessionIndex = 0;
    for (const d of dates) {
      const a: DbAssignment = {
        id: newId(),
        clientId: id,
        date: d.date,
        status: 'scheduled',
        workoutId: d.workout.id,
        snapshot: snapshotWorkout(d.workout),
        programAssignmentId: d.pa,
        notes: null,
        sessionId: null,
        completedAt: null,
        createdAt: `${addDaysIso(d.date, -3)}T10:00:00Z`,
      };
      state.assignments.push(a);
      const past = d.date < today;
      const isTodayDone = d.date === today && ci === 2;
      if (!(past || isTodayDone)) continue;
      if (past && r() > c.rate) {
        if (r() < 0.4) a.status = 'skipped';
        continue;
      }
      const start = `${d.date}T${String(16 + ci).padStart(2, '0')}:0${ci}:00Z`;
      const s: DbSession = {
        id: newId(),
        clientId: id,
        assignmentId: a.id,
        workoutName: a.snapshot.name,
        status: 'in-progress',
        startedAt: start,
        completedAt: null,
        notes: null,
        plan: planFromSnapshot(a.snapshot),
        sets: [],
        personalRecords: [],
      };
      const progress = 1 + sessionIndex * 0.012;
      for (const p of s.plan) {
        const target = p.prescription;
        for (let n = 1; n <= target.sets; n++) {
          const baseLoad = target.load.type === 'weight' && target.load.value !== null ? target.load.value * c.strength * progress : null;
          const reps = target.goal.type === 'reps' ? target.goal.min + Math.floor(r() * (target.goal.max - target.goal.min + 1)) : null;
          s.sets.push({
            id: newId(),
            exerciseId: p.exerciseId,
            position: p.position,
            setNumber: n,
            weightKg: baseLoad === null ? (target.load.type === 'bodyweight' ? 0 : null) : plate(baseLoad),
            reps,
            durationSeconds: target.goal.type === 'time' ? target.goal.min : null,
            distanceMeters: target.goal.type === 'distance' ? target.goal.min : null,
            rpe: reps === null ? null : Math.round((7 + r() * 2) * 2) / 2,
            completed: true,
            notes: null,
            updatedAt: start,
          });
        }
      }
      a.sessionId = s.id;
      state.sessions.push(s);
      const minutes = 45 + Math.floor(r() * 25);
      completeSession(s, new Date(Date.parse(start) + minutes * 60_000).toISOString().replace(/\.\d{3}Z$/, 'Z'));
      sessionIndex += 1;
    }

    for (let wk = 6; wk >= 0; wk--) {
      const date = addDaysIso(today, -wk * 7 - (ci % 3));
      if (date > today) continue;
      const trend = c.weight - (6 - wk) * (ci === 2 ? 0.6 : 0.15) + (r() - 0.5) * 0.6;
      state.bodyMetrics.push({
        clientId: id,
        date,
        weightKg: round2(trend),
        bodyFatPercent: wk % 2 === 0 ? round2(18 + ci * 2 - (6 - wk) * 0.2) : null,
        waistCm: wk % 2 === 0 ? round2(84 + ci * 4 - (6 - wk) * 0.3) : null,
        chestCm: null,
        hipsCm: null,
        armCm: null,
        thighCm: null,
        restingHeartRate: 60 - ci * 2 + Math.round(r() * 4),
        notes: null,
        recordedBy: wk % 3 === 0 ? 'trainer' : 'client',
        updatedAt: `${date}T07:00:00Z`,
      });
    }
  });

  state.invitations.push({
    id: seedId('1b0', 1),
    email: 'riley@trainersphere.dev',
    firstName: 'Riley',
    lastName: 'Morgan',
    message: 'Excited to work with you!',
    status: 'pending',
    createdAt: `${addDaysIso(today, -1)}T09:00:00Z`,
    expiresAt: `${addDaysIso(today, 6)}T09:00:00Z`,
    acceptedAt: null,
    clientId: null,
    token: 'demo-invite-riley-0000000000000000000000000',
    trainerId: trainer.id,
  });

  return state;
}
