// Types mirror api/contract.md (v1, 2026-11-18). Keep in sync when its "Last changed" moves.

export type Uuid = string;
/** ISO 8601 UTC timestamp. */
export type Iso = string;
/** Calendar date `YYYY-MM-DD` in the user's time zone. */
export type IsoDate = string;

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
}

// ---- Auth -------------------------------------------------------------------

export type Role = 'trainer' | 'client';
export type Units = 'kg' | 'lb';

export interface User {
  id: Uuid;
  email: string;
  firstName: string;
  lastName: string;
  role: Role;
  /** Set for clients. */
  trainerId: Uuid | null;
  units: Units;
  timeZone: string;
  createdAt: Iso;
}

export interface AuthResponse {
  accessToken: string;
  tokenType: 'Bearer';
  /** Seconds. */
  expiresIn: number;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface SignUpRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  timeZone?: string;
}

export interface UpdateMeRequest {
  firstName: string;
  lastName: string;
  units: Units;
  timeZone: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface InvitationPreview {
  email: string;
  firstName: string;
  lastName: string;
  trainerName: string;
  message: string | null;
  /** Only `pending` can be accepted; the others explain why the link no longer works. */
  status: InvitationStatus;
  expiresAt: Iso;
}

export interface AcceptInvitationRequest {
  firstName: string;
  lastName: string;
  password: string;
  timeZone?: string;
}

// ---- Invitations ------------------------------------------------------------

export type InvitationStatus = 'pending' | 'accepted' | 'cancelled' | 'expired';

export interface Invitation {
  id: Uuid;
  email: string;
  firstName: string;
  lastName: string;
  message: string | null;
  status: InvitationStatus;
  createdAt: Iso;
  expiresAt: Iso;
  acceptedAt: Iso | null;
  clientId: Uuid | null;
}

export interface InviteClientRequest {
  email: string;
  firstName: string;
  lastName: string;
  message?: string;
}

// ---- Exercises --------------------------------------------------------------

export type ExerciseCategory = string;
export type MovementPattern = string;
export type Muscle = string;
export type Equipment = string;

export interface Taxonomy {
  categories: ExerciseCategory[];
  movementPatterns: MovementPattern[];
  muscles: Muscle[];
  equipment: Equipment[];
}

export interface ExerciseRef {
  id: Uuid;
  slug: string;
  name: string;
  illustration: string | null;
}

export interface Exercise extends ExerciseRef {
  category: ExerciseCategory;
  movementPattern: MovementPattern;
  primaryMuscles: Muscle[];
  secondaryMuscles: Muscle[];
  equipment: Equipment[];
  unilateral: boolean;
  cue: string | null;
  isCustom: boolean;
  archived: boolean;
}

export interface ExerciseQuery extends PageQuery {
  search?: string;
  category?: ExerciseCategory[];
  pattern?: MovementPattern[];
  muscle?: Muscle[];
  equipment?: Equipment[];
  custom?: boolean;
  includeArchived?: boolean;
}

export interface UpsertExerciseRequest {
  name: string;
  category: ExerciseCategory;
  movementPattern: MovementPattern;
  primaryMuscles: Muscle[];
  secondaryMuscles: Muscle[];
  equipment: Equipment[];
  unilateral: boolean;
  cue: string | null;
}

// ---- Workouts ---------------------------------------------------------------

export type GoalType = 'reps' | 'time' | 'distance';
export interface Goal {
  type: GoalType;
  min: number;
  max: number;
}

export type LoadType = 'none' | 'bodyweight' | 'weight' | 'percent-1rm' | 'rpe';
export interface Load {
  type: LoadType;
  value: number | null;
}

export interface Prescription {
  sets: number;
  goal: Goal;
  load: Load;
  restSeconds: number;
  tempo: string | null;
  supersetGroup: string | null;
  notes: string | null;
}

export interface WorkoutExercise extends Prescription {
  id: Uuid;
  position: number;
  exerciseId: Uuid;
  exercise: ExerciseRef;
}

export interface Workout {
  id: Uuid;
  name: string;
  description: string | null;
  archived: boolean;
  createdAt: Iso;
  updatedAt: Iso;
  exercises: WorkoutExercise[];
}

export interface WorkoutSummary {
  id: Uuid;
  name: string;
  description: string | null;
  exerciseCount: number;
  archived: boolean;
  updatedAt: Iso;
}

export interface UpsertWorkoutRequest {
  name: string;
  description: string | null;
  exercises: (Prescription & { exerciseId: Uuid })[];
}

// ---- Programs ---------------------------------------------------------------

export interface ProgramSlot {
  id: Uuid;
  week: number;
  /** 1-7, day index within the program week; day 1 is the start date's weekday. */
  day: number;
  workoutId: Uuid;
  workout: { id: Uuid; name: string };
}

export interface Program {
  id: Uuid;
  name: string;
  description: string | null;
  weeks: number;
  archived: boolean;
  createdAt: Iso;
  updatedAt: Iso;
  slots: ProgramSlot[];
}

export interface ProgramSummary {
  id: Uuid;
  name: string;
  description: string | null;
  weeks: number;
  workoutCount: number;
  archived: boolean;
  updatedAt: Iso;
}

export interface UpsertProgramRequest {
  name: string;
  description: string | null;
  weeks: number;
  slots: { week: number; day: number; workoutId: Uuid }[];
}

export interface AssignProgramRequest {
  clientIds: Uuid[];
  startDate: IsoDate;
}

export interface ProgramAssignment {
  id: Uuid;
  programId: Uuid;
  programName: string;
  clientId: Uuid;
  startDate: IsoDate;
  endDate: IsoDate;
  assignmentCount: number;
  createdAt: Iso;
}

// ---- Assignments ------------------------------------------------------------

export interface PersonRef {
  id: Uuid;
  firstName: string;
  lastName: string;
}

export type AssignmentStatus = 'scheduled' | 'completed' | 'skipped' | 'missed';

export interface Assignment {
  id: Uuid;
  clientId: Uuid;
  client: PersonRef;
  date: IsoDate;
  status: AssignmentStatus;
  workoutId: Uuid;
  workout: { name: string; description: string | null; exercises: WorkoutExercise[] };
  programAssignmentId: Uuid | null;
  programName: string | null;
  notes: string | null;
  sessionId: Uuid | null;
  completedAt: Iso | null;
  createdAt: Iso;
}

export interface AssignmentQuery {
  clientId?: Uuid;
  from?: IsoDate;
  to?: IsoDate;
  status?: AssignmentStatus;
}

export interface CreateAssignmentRequest {
  clientId: Uuid;
  workoutId: Uuid;
  date: IsoDate;
  notes?: string | null;
}

export interface UpdateAssignmentRequest {
  date: IsoDate;
  notes: string | null;
}

// ---- Sessions ---------------------------------------------------------------

export type SessionStatus = 'in-progress' | 'completed';

export interface SessionSet {
  id: Uuid;
  exerciseId: Uuid;
  position: number;
  setNumber: number;
  weightKg: number | null;
  reps: number | null;
  durationSeconds: number | null;
  distanceMeters: number | null;
  rpe: number | null;
  completed: boolean;
  notes: string | null;
  updatedAt: Iso;
}

export interface LastPerformance {
  sessionId: Uuid;
  date: IsoDate;
  sets: { setNumber: number; weightKg: number | null; reps: number | null; rpe: number | null }[];
}

export interface SessionExercise {
  position: number;
  exerciseId: Uuid;
  exercise: ExerciseRef;
  prescription: Prescription | null;
  lastPerformance: LastPerformance | null;
  sets: SessionSet[];
}

export interface SessionTotals {
  totalVolumeKg: number;
  completedSets: number;
  totalReps: number;
  durationSeconds: number | null;
  personalRecords: PersonalRecord[];
}

export interface Session {
  id: Uuid;
  clientId: Uuid;
  assignmentId: Uuid | null;
  workoutName: string;
  status: SessionStatus;
  startedAt: Iso;
  completedAt: Iso | null;
  notes: string | null;
  exercises: SessionExercise[];
  summary: SessionTotals;
}

export interface SessionSummary {
  id: Uuid;
  clientId: Uuid;
  assignmentId: Uuid | null;
  workoutName: string;
  status: SessionStatus;
  startedAt: Iso;
  completedAt: Iso | null;
  totalVolumeKg: number;
  completedSets: number;
  personalRecordCount: number;
}

export interface StartSessionRequest {
  id?: Uuid;
  assignmentId?: Uuid;
  workoutName?: string;
  startedAt?: Iso;
}

export interface SessionQuery extends PageQuery {
  clientId?: Uuid;
  status?: SessionStatus;
  from?: IsoDate;
  to?: IsoDate;
}

export interface PutSetRequest {
  exerciseId: Uuid;
  position: number;
  setNumber: number;
  weightKg: number | null;
  reps: number | null;
  durationSeconds: number | null;
  distanceMeters: number | null;
  rpe: number | null;
  completed: boolean;
  notes: string | null;
}

export interface CompleteSessionRequest {
  completedAt?: Iso;
  notes?: string | null;
}

// ---- Personal records and progress -----------------------------------------

export type PrType = 'estimated-1rm' | 'max-weight' | 'max-reps' | 'max-volume';

export interface PersonalRecord {
  id: Uuid;
  clientId: Uuid;
  exerciseId: Uuid;
  exercise: ExerciseRef;
  type: PrType;
  value: number;
  previousValue: number;
  weightKg: number | null;
  reps: number | null;
  sessionId: Uuid;
  achievedAt: Iso;
}

export interface PersonalRecordWithClient extends PersonalRecord {
  client: PersonRef;
}

export interface ProgressExercise {
  exerciseId: Uuid;
  exercise: ExerciseRef;
  sessionCount: number;
  lastPerformedAt: Iso;
}

export interface ProgressPoint {
  date: IsoDate;
  sessionId: Uuid;
  estimatedOneRepMaxKg: number | null;
  topSetWeightKg: number | null;
  topSetReps: number | null;
  volumeKg: number;
  totalReps: number;
  completedSets: number;
}

export interface ExerciseProgress {
  exercise: ExerciseRef;
  points: ProgressPoint[];
}

export interface ProgressSummary {
  weeks: { weekStart: IsoDate; sessions: number; volumeKg: number; completedSets: number }[];
  totalSessions: number;
  adherence7d: number | null;
  adherence28d: number | null;
  streakWeeks: number;
}

// ---- Body metrics -----------------------------------------------------------

export interface BodyMetricValues {
  weightKg: number | null;
  bodyFatPercent: number | null;
  waistCm: number | null;
  chestCm: number | null;
  hipsCm: number | null;
  armCm: number | null;
  thighCm: number | null;
  restingHeartRate: number | null;
  notes: string | null;
}

export interface BodyMetric extends BodyMetricValues {
  clientId: Uuid;
  date: IsoDate;
  recordedBy: Role;
  updatedAt: Iso;
}

// ---- Clients ----------------------------------------------------------------

export type ClientStatus = 'active' | 'archived';

export interface ClientSummary {
  id: Uuid;
  firstName: string;
  lastName: string;
  email: string;
  status: ClientStatus;
  joinedAt: Iso;
  lastSessionAt: Iso | null;
  adherence7d: number | null;
  adherence28d: number | null;
  nextAssignment: { id: Uuid; date: IsoDate; workoutName: string } | null;
}

export interface ClientDetail extends ClientSummary {
  goals: string | null;
  trainerNotes: string | null;
  inProgressSessionId: Uuid | null;
}

export type ClientSort = 'name' | '-lastSessionAt' | 'adherence7d';

export interface ClientQuery extends PageQuery {
  search?: string;
  status?: ClientStatus;
  sort?: ClientSort;
}

export interface UpdateClientRequest {
  goals: string | null;
  trainerNotes: string | null;
}

// ---- Dashboards -------------------------------------------------------------

export interface TrainerDashboard {
  date: IsoDate;
  activeClients: number;
  today: {
    assignmentId: Uuid | null;
    client: PersonRef;
    workoutName: string;
    status: AssignmentStatus;
    sessionId: Uuid | null;
    completedAt: Iso | null;
  }[];
  adherence: {
    client: PersonRef;
    adherence7d: number | null;
    adherence28d: number | null;
    due7d: number;
    completed7d: number;
    due28d: number;
    completed28d: number;
    lastSessionAt: Iso | null;
  }[];
  recentPersonalRecords: PersonalRecordWithClient[];
  pendingInvitations: { count: number; items: Invitation[] };
  recentSessions: (SessionSummary & { client: PersonRef })[];
}

export interface ClientDashboard {
  date: IsoDate;
  today: Assignment[];
  nextAssignment: Assignment | null;
  inProgressSession: SessionSummary | null;
  thisWeek: { due: number; completed: number; rate: number | null };
  adherence7d: number | null;
  adherence28d: number | null;
  streakWeeks: number;
  recentPersonalRecords: PersonalRecord[];
  recentSessions: SessionSummary[];
  latestBodyMetric: BodyMetric | null;
}

// ---- Errors -----------------------------------------------------------------

/** RFC 9457 problem details. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}
