import { http, HttpResponse, delay, type HttpResponseResolver } from 'msw';
import type * as T from '@/lib/api/types';
import { addDaysIso, todayIso } from '@/lib/dates';
import {
  adherence,
  assignmentStatus,
  completeSession,
  db,
  newId,
  nowIso,
  personRef,
  planFromSnapshot,
  snapshotWorkout,
  streakWeeks,
  toAssignment,
  toExercise,
  toInvitation,
  toProgram,
  toProgramSummary,
  toSession,
  toSessionSummary,
  toWorkout,
  toWorkoutSummary,
  type DbAssignment,
  type DbClient,
  type DbSession,
  type DbUser,
  type DbWorkoutExercise,
} from './db';
import { seedTaxonomy } from './seed-exercises';

export const mockConfig = {
  delayMs: 0,
  accessTokenTtlSeconds: 900,
  onMutate: (): void => undefined,
  /**
   * Stand-in for the browser cookie jar: MSW cannot set httpOnly cookies, so the mock keeps the
   * refresh token here and treats it as the `ts_refresh` cookie the real API would set.
   */
  refreshCookie: null as string | null,
  onRefreshCookie: (_token: string | null): void => undefined,
};

const API = '/api';

// ---- helpers ----------------------------------------------------------------

function problem(status: number, code: string, detail: string, errors?: Record<string, string[]>, extra: Record<string, unknown> = {}) {
  const titles: Record<number, string> = {
    400: 'One or more validation errors occurred.',
    401: 'Unauthorized',
    403: 'Forbidden',
    404: 'Not Found',
    409: 'Conflict',
    422: 'Unprocessable Entity',
  };
  return HttpResponse.json(
    { type: 'about:blank', title: titles[status] ?? 'Error', status, detail, code, traceId: '00-mock', ...(errors ? { errors } : {}), ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

const validation = (errors: Record<string, string[]>) => problem(400, 'Validation', 'The request is invalid.', errors);
const notFound = (what: string) => problem(404, `${what}.NotFound`, `${what} was not found.`);
const noContent = () => new HttpResponse(null, { status: 204 });

function setRefresh(token: string | null) {
  mockConfig.refreshCookie = token;
  mockConfig.onRefreshCookie(token);
}

function issueAuth(user: DbUser, status = 200) {
  const token = `rt_${newId()}`;
  db.state.refreshTokens[token] = user.id;
  setRefresh(token);
  const accessToken = `mock.${user.id}.${Date.now() + mockConfig.accessTokenTtlSeconds * 1000}`;
  const { password: _p, ...publicUser } = user;
  return HttpResponse.json(
    { accessToken, tokenType: 'Bearer', expiresIn: mockConfig.accessTokenTtlSeconds, user: publicUser } satisfies T.AuthResponse,
    { status, headers: { 'Set-Cookie': `ts_refresh=${token}; Path=/api/auth; HttpOnly; SameSite=Strict` } },
  );
}

function publicUser(u: DbUser): T.User {
  const { password: _p, ...rest } = u;
  return rest;
}

function currentUser(request: Request): DbUser | null {
  const header = request.headers.get('Authorization') ?? '';
  const match = /^Bearer mock\.([^.]+)\.(\d+)$/.exec(header);
  if (!match) return null;
  if (Number(match[2]) < Date.now()) return null;
  return db.state.users.find((u) => u.id === match[1]) ?? null;
}

type Ctx = { request: Request; params: Record<string, string | readonly string[] | undefined>; user: DbUser; url: URL };
type Handler = (ctx: Ctx) => Response | Promise<Response>;

function authed(role: T.Role | 'any', handler: Handler): HttpResponseResolver {
  return async ({ request, params }) => {
    if (mockConfig.delayMs) await delay(mockConfig.delayMs);
    const user = currentUser(request);
    if (!user) return problem(401, 'Auth.Unauthorized', 'Authentication is required.');
    if (role !== 'any' && user.role !== role) return problem(403, 'Auth.Forbidden', 'You do not have access to this resource.');
    const res = await handler({ request, params: params as Ctx['params'], user, url: new URL(request.url) });
    if (request.method !== 'GET') mockConfig.onMutate();
    return res;
  };
}

function open(handler: (ctx: { request: Request; params: Record<string, string | readonly string[] | undefined> }) => Response | Promise<Response>): HttpResponseResolver {
  return async ({ request, params }) => {
    if (mockConfig.delayMs) await delay(mockConfig.delayMs);
    const res = await handler({ request, params: params as Ctx['params'] });
    if (request.method !== 'GET') mockConfig.onMutate();
    return res;
  };
}

const param = (ctx: { params: Ctx['params'] }, name: string) => String(ctx.params[name] ?? '');

async function body<B>(request: Request): Promise<B> {
  try {
    return (await request.json()) as B;
  } catch {
    return {} as B;
  }
}

function paged<X>(items: X[], url: URL, maxPageSize = 100): T.Paged<X> {
  const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
  const pageSize = Math.min(maxPageSize, Math.max(1, Number(url.searchParams.get('pageSize') ?? 20)));
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  return { items: items.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: items.length, totalPages };
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function checkPassword(password: unknown, field: string, errors: Record<string, string[]>) {
  if (typeof password !== 'string' || password.length < 8) errors[field] = ['Password must be at least 8 characters long.'];
  else if (password.length > 128) errors[field] = ['Password must be at most 128 characters long.'];
}

function checkName(value: unknown, field: string, errors: Record<string, string[]>) {
  if (typeof value !== 'string' || value.trim().length === 0) errors[field] = ['Required.'];
  else if (value.length > 100) errors[field] = ['Must be at most 100 characters.'];
}

/** Trainer id that owns data visible to this user. */
const trainerOf = (u: DbUser) => (u.role === 'trainer' ? u.id : (u.trainerId ?? ''));

function clientFor(user: DbUser, clientId: string): DbClient | null {
  const c = db.state.clients.find((x) => x.id === clientId);
  if (!c) return null;
  if (user.role === 'trainer' && c.trainerId === user.id) return c;
  if (user.role === 'client' && user.id === clientId) return c;
  return null;
}

function clientSummary(c: DbClient): T.ClientSummary {
  const u = db.state.users.find((x) => x.id === c.id) as DbUser;
  const today = todayIso();
  const last = db.state.sessions
    .filter((s) => s.clientId === c.id && s.status === 'completed')
    .sort((a, b) => (b.completedAt ?? '').localeCompare(a.completedAt ?? ''))[0];
  const next = db.state.assignments
    .filter((a) => a.clientId === c.id && a.status === 'scheduled' && a.date >= today)
    .sort((a, b) => a.date.localeCompare(b.date))[0];
  return {
    id: c.id,
    firstName: u.firstName,
    lastName: u.lastName,
    email: u.email,
    status: c.status,
    joinedAt: c.joinedAt,
    lastSessionAt: last?.completedAt ?? null,
    adherence7d: adherence(c.id, 7).rate,
    adherence28d: adherence(c.id, 28).rate,
    nextAssignment: next ? { id: next.id, date: next.date, workoutName: next.snapshot.name } : null,
  };
}

function clientDetail(c: DbClient, viewer: DbUser): T.ClientDetail {
  const inProgress = db.state.sessions.find((s) => s.clientId === c.id && s.status === 'in-progress');
  return {
    ...clientSummary(c),
    goals: c.goals,
    trainerNotes: viewer.role === 'trainer' ? c.trainerNotes : null,
    inProgressSessionId: inProgress?.id ?? null,
  };
}

function visibleExercises(user: DbUser) {
  const owner = trainerOf(user);
  return db.state.exercises.filter((e) => e.ownerId === null || e.ownerId === owner);
}

function validateExercise(b: Partial<T.UpsertExerciseRequest>) {
  const errors: Record<string, string[]> = {};
  checkName(b.name, 'name', errors);
  if (!b.category) errors.category = ['Required.'];
  if (!b.movementPattern) errors.movementPattern = ['Required.'];
  if (!b.primaryMuscles?.length) errors.primaryMuscles = ['Pick at least one primary muscle.'];
  if (b.cue && b.cue.length > 1000) errors.cue = ['Must be at most 1000 characters.'];
  const t = seedTaxonomy;
  if (b.category && !t.categories.includes(b.category)) errors.category = [`'${b.category}' is not a known category.`];
  if (b.movementPattern && !t.movementPatterns.includes(b.movementPattern)) errors.movementPattern = [`'${b.movementPattern}' is not a known movement pattern.`];
  for (const field of ['primaryMuscles', 'secondaryMuscles'] as const) {
    const bad = (b[field] ?? []).find((m) => !t.muscles.includes(m));
    if (bad) errors[field] = [`'${bad}' is not a known muscle.`];
  }
  const badEquipment = (b.equipment ?? []).find((x) => !t.equipment.includes(x));
  if (badEquipment) errors.equipment = [`'${badEquipment}' is not known equipment.`];
  return errors;
}

function validateWorkout(b: Partial<T.UpsertWorkoutRequest>, user: DbUser) {
  const errors: Record<string, string[]> = {};
  checkName(b.name, 'name', errors);
  const list = b.exercises ?? [];
  if (list.length < 1) errors.exercises = ['Add at least one exercise.'];
  if (list.length > 30) errors.exercises = ['At most 30 exercises.'];
  const visible = new Set(visibleExercises(user).map((e) => e.id));
  list.forEach((e, i) => {
    if (!visible.has(e.exerciseId)) errors[`exercises[${i}].exerciseId`] = ['Exercise was not found.'];
    if (!(e.sets >= 1 && e.sets <= 20)) errors[`exercises[${i}].sets`] = ['Sets must be between 1 and 20.'];
    if (e.goal.max < e.goal.min) errors[`exercises[${i}].goal.max`] = ['Max must be greater than or equal to min.'];
    if (!(e.restSeconds >= 0 && e.restSeconds <= 1800)) errors[`exercises[${i}].restSeconds`] = ['Rest must be between 0 and 1800 seconds.'];
    if (e.tempo && !/^[0-9X]-[0-9X]-[0-9X]-[0-9X]$/.test(e.tempo)) errors[`exercises[${i}].tempo`] = ['Tempo must look like 3-1-X-0.'];
  });
  return errors;
}

function toDbWorkoutExercises(list: T.UpsertWorkoutRequest['exercises']): DbWorkoutExercise[] {
  return list.map((e) => ({
    id: newId(),
    exerciseId: e.exerciseId,
    sets: e.sets,
    goal: e.goal,
    load: e.load,
    restSeconds: e.restSeconds,
    tempo: e.tempo ?? null,
    supersetGroup: e.supersetGroup ?? null,
    notes: e.notes ?? null,
  }));
}

function visibleAssignment(user: DbUser, id: string): DbAssignment | null {
  const a = db.state.assignments.find((x) => x.id === id);
  if (!a) return null;
  return clientFor(user, a.clientId) ? a : null;
}

function visibleSession(user: DbUser, id: string): DbSession | null {
  const s = db.state.sessions.find((x) => x.id === id);
  if (!s) return null;
  return clientFor(user, s.clientId) ? s : null;
}

function prsWithClient(list: T.PersonalRecord[]): T.PersonalRecordWithClient[] {
  return list.map((p) => ({ ...p, client: personRef(p.clientId) }));
}

function currentBests(clientId: string): T.PersonalRecord[] {
  const best = new Map<string, T.PersonalRecord>();
  for (const p of db.state.personalRecords.filter((x) => x.clientId === clientId)) {
    const key = `${p.exerciseId}|${p.type}`;
    const cur = best.get(key);
    if (!cur || p.value > cur.value) best.set(key, p);
  }
  return [...best.values()].sort((a, b) => b.achievedAt.localeCompare(a.achievedAt));
}

// ---- handlers ---------------------------------------------------------------

export const handlers = [
  // Auth
  http.post(`${API}/auth/login`, open(async ({ request }) => {
    const b = await body<T.LoginRequest>(request);
    const user = db.state.users.find((u) => u.email === (b.email ?? '').trim().toLowerCase());
    if (user?.password !== b.password || !user) return problem(401, 'Auth.InvalidCredentials', 'Email or password is incorrect.');
    return issueAuth(user);
  })),

  http.post(`${API}/auth/signup`, open(async ({ request }) => {
    const b = await body<T.SignUpRequest>(request);
    const errors: Record<string, string[]> = {};
    const email = (b.email ?? '').trim().toLowerCase();
    if (!EMAIL.test(email)) errors.email = ['Email is not a valid email address.'];
    checkPassword(b.password, 'password', errors);
    checkName(b.firstName, 'firstName', errors);
    checkName(b.lastName, 'lastName', errors);
    if (Object.keys(errors).length) return validation(errors);
    if (db.state.users.some((u) => u.email === email)) return problem(409, 'User.EmailInUse', `The email '${email}' is already in use.`);
    const user: DbUser = {
      id: newId(),
      email,
      password: b.password,
      firstName: b.firstName.trim(),
      lastName: b.lastName.trim(),
      role: 'trainer',
      trainerId: null,
      units: 'kg',
      timeZone: b.timeZone ?? 'UTC',
      createdAt: nowIso(),
    };
    db.state.users.push(user);
    return issueAuth(user, 201);
  })),

  http.post(`${API}/auth/refresh`, open(({ request }) => {
    const cookie = /(?:^|;\s*)ts_refresh=([^;]+)/.exec(request.headers.get('cookie') ?? '')?.[1];
    const token = cookie ?? mockConfig.refreshCookie;
    const userId = token ? db.state.refreshTokens[token] : undefined;
    const user = userId ? db.state.users.find((u) => u.id === userId) : undefined;
    if (!token || !user) {
      setRefresh(null);
      return problem(401, 'Auth.InvalidRefreshToken', 'The refresh token is invalid or expired.');
    }
    delete db.state.refreshTokens[token];
    return issueAuth(user);
  })),

  http.post(`${API}/auth/logout`, open(() => {
    if (mockConfig.refreshCookie) delete db.state.refreshTokens[mockConfig.refreshCookie];
    setRefresh(null);
    return noContent();
  })),

  http.get(`${API}/auth/me`, authed('any', ({ user }) => HttpResponse.json(publicUser(user)))),

  http.put(`${API}/auth/me`, authed('any', async ({ request, user }) => {
    const b = await body<T.UpdateMeRequest>(request);
    const errors: Record<string, string[]> = {};
    checkName(b.firstName, 'firstName', errors);
    checkName(b.lastName, 'lastName', errors);
    if (b.units !== 'kg' && b.units !== 'lb') errors.units = ['Units must be kg or lb.'];
    if (Object.keys(errors).length) return validation(errors);
    Object.assign(user, { firstName: b.firstName.trim(), lastName: b.lastName.trim(), units: b.units, timeZone: b.timeZone || user.timeZone });
    return HttpResponse.json(publicUser(user));
  })),

  http.post(`${API}/auth/change-password`, authed('any', async ({ request, user }) => {
    const b = await body<T.ChangePasswordRequest>(request);
    const errors: Record<string, string[]> = {};
    if (b.currentPassword !== user.password) errors.currentPassword = ['Current password is incorrect.'];
    checkPassword(b.newPassword, 'newPassword', errors);
    if (Object.keys(errors).length) return validation(errors);
    user.password = b.newPassword;
    return noContent();
  })),

  http.post(`${API}/auth/forgot-password`, open(async ({ request }) => {
    const b = await body<{ email: string }>(request);
    const user = db.state.users.find((u) => u.email === (b.email ?? '').trim().toLowerCase());
    if (user) db.state.resetTokens[`reset-${user.id}`] = user.id;
    return new HttpResponse(null, { status: 202 });
  })),

  http.post(`${API}/auth/reset-password`, open(async ({ request }) => {
    const b = await body<T.ResetPasswordRequest>(request);
    const errors: Record<string, string[]> = {};
    checkPassword(b.newPassword, 'newPassword', errors);
    const userId = db.state.resetTokens[b.token];
    const user = db.state.users.find((u) => u.id === userId);
    if (!user) errors.token = ['This reset link is invalid, already used or expired.'];
    if (Object.keys(errors).length || !user) return validation(errors);
    user.password = b.newPassword;
    delete db.state.resetTokens[b.token];
    return noContent();
  })),

  // Invitations (public)
  http.get(`${API}/invitations/:token`, open(({ params }) => {
    const token = String(params.token);
    const inv = db.state.invitations.find((i) => i.token === token);
    if (!inv) return problem(404, 'Invitation.NotFound', 'This invitation link is not valid.');
    const view = toInvitation(inv);
    const trainer = db.state.users.find((u) => u.id === inv.trainerId);
    return HttpResponse.json({
      email: inv.email,
      firstName: inv.firstName,
      lastName: inv.lastName,
      trainerName: trainer ? `${trainer.firstName} ${trainer.lastName}` : 'Your trainer',
      message: inv.message,
      status: view.status,
      expiresAt: inv.expiresAt,
    } satisfies T.InvitationPreview);
  })),

  http.post(`${API}/invitations/:token/accept`, open(async ({ request, params }) => {
    const token = String(params.token);
    const inv = db.state.invitations.find((i) => i.token === token);
    if (!inv) return problem(404, 'Invitation.NotFound', 'This invitation link is not valid.');
    if (toInvitation(inv).status !== 'pending') return problem(409, 'Invitation.NotPending', 'This invitation can no longer be accepted.');
    const b = await body<T.AcceptInvitationRequest>(request);
    const errors: Record<string, string[]> = {};
    checkName(b.firstName, 'firstName', errors);
    checkName(b.lastName, 'lastName', errors);
    checkPassword(b.password, 'password', errors);
    if (Object.keys(errors).length) return validation(errors);
    if (db.state.users.some((u) => u.email === inv.email)) return problem(409, 'User.EmailInUse', 'An account with this email already exists.');
    const user: DbUser = {
      id: newId(),
      email: inv.email,
      password: b.password,
      firstName: b.firstName.trim(),
      lastName: b.lastName.trim(),
      role: 'client',
      trainerId: inv.trainerId,
      units: 'kg',
      timeZone: b.timeZone ?? 'UTC',
      createdAt: nowIso(),
    };
    db.state.users.push(user);
    db.state.clients.push({ id: user.id, trainerId: inv.trainerId, status: 'active', joinedAt: nowIso(), goals: null, trainerNotes: null });
    inv.status = 'accepted';
    inv.acceptedAt = nowIso();
    inv.clientId = user.id;
    return issueAuth(user, 201);
  })),

  // Invitations (trainer). The `:token` GET above only matches non-UUID paths in practice;
  // list uses the bare collection path.
  http.get(`${API}/invitations`, authed('trainer', ({ user, url }) => {
    const status = url.searchParams.get('status');
    const list = db.state.invitations
      .filter((i) => i.trainerId === user.id)
      .map(toInvitation)
      .filter((i) => !status || i.status === status)
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return HttpResponse.json(paged(list, url));
  })),

  http.post(`${API}/invitations`, authed('trainer', async ({ request, user }) => {
    const b = await body<T.InviteClientRequest>(request);
    const errors: Record<string, string[]> = {};
    const email = (b.email ?? '').trim().toLowerCase();
    if (!EMAIL.test(email)) errors.email = ['Email is not a valid email address.'];
    checkName(b.firstName, 'firstName', errors);
    checkName(b.lastName, 'lastName', errors);
    if (b.message && b.message.length > 500) errors.message = ['Message must be at most 500 characters.'];
    if (Object.keys(errors).length) return validation(errors);
    if (db.state.users.some((u) => u.email === email)) return problem(409, 'User.EmailInUse', `'${email}' already has an account.`);
    if (db.state.invitations.some((i) => i.trainerId === user.id && i.email === email && toInvitation(i).status === 'pending'))
      return problem(409, 'Invitation.PendingExists', `There is already a pending invitation for '${email}'.`);
    const inv = {
      id: newId(),
      email,
      firstName: b.firstName.trim(),
      lastName: b.lastName.trim(),
      message: b.message?.trim() || null,
      status: 'pending' as const,
      createdAt: nowIso(),
      expiresAt: new Date(Date.now() + 7 * 86400_000).toISOString(),
      acceptedAt: null,
      clientId: null,
      token: `invite-${newId()}`,
      trainerId: user.id,
    };
    db.state.invitations.push(inv);
    return HttpResponse.json(toInvitation(inv), { status: 201 });
  })),

  http.post(`${API}/invitations/:id/resend`, authed('trainer', (ctx) => {
    const inv = db.state.invitations.find((i) => i.id === param(ctx, 'id') && i.trainerId === ctx.user.id);
    if (!inv) return notFound('Invitation');
    if (inv.status !== 'pending') return problem(409, 'Invitation.NotPending', 'Only pending invitations can be resent.');
    inv.expiresAt = new Date(Date.now() + 7 * 86400_000).toISOString();
    inv.token = `invite-${newId()}`;
    return HttpResponse.json(toInvitation(inv));
  })),

  http.delete(`${API}/invitations/:id`, authed('trainer', (ctx) => {
    const inv = db.state.invitations.find((i) => i.id === param(ctx, 'id') && i.trainerId === ctx.user.id);
    if (!inv) return notFound('Invitation');
    if (inv.status === 'accepted') return problem(409, 'Invitation.NotPending', 'This invitation was already accepted.');
    inv.status = 'cancelled';
    return noContent();
  })),

  // Exercises
  http.get(`${API}/exercises/taxonomy`, authed('any', () => HttpResponse.json(seedTaxonomy))),

  http.get(`${API}/exercises`, authed('any', ({ user, url }) => {
    const p = url.searchParams;
    const search = (p.get('search') ?? '').toLowerCase();
    const any = (name: string, values: string[]) => {
      const wanted = p.getAll(name);
      return wanted.length === 0 || values.some((v) => wanted.includes(v));
    };
    const custom = p.get('custom');
    const list = visibleExercises(user)
      .filter((e) => p.get('includeArchived') === 'true' || !e.archived)
      .filter((e) => !search || e.name.toLowerCase().includes(search))
      .filter((e) => any('category', [e.category]) && any('pattern', [e.movementPattern]))
      .filter((e) => any('muscle', [...e.primaryMuscles, ...e.secondaryMuscles]) && any('equipment', e.equipment))
      .filter((e) => custom === null || String(e.isCustom) === custom)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(toExercise);
    return HttpResponse.json(paged(list, url, 200));
  })),

  http.get(`${API}/exercises/:id`, authed('any', (ctx) => {
    const e = visibleExercises(ctx.user).find((x) => x.id === param(ctx, 'id'));
    return e ? HttpResponse.json(toExercise(e)) : notFound('Exercise');
  })),

  http.post(`${API}/exercises`, authed('trainer', async ({ request, user }) => {
    const b = await body<T.UpsertExerciseRequest>(request);
    const errors = validateExercise(b);
    if (Object.keys(errors).length) return validation(errors);
    if (db.state.exercises.some((e) => e.ownerId === user.id && !e.archived && e.name.toLowerCase() === b.name.trim().toLowerCase()))
      return problem(409, 'Exercise.NameInUse', `You already have an exercise named '${b.name.trim()}'.`);
    const e = {
      ...b,
      name: b.name.trim(),
      id: newId(),
      slug: b.name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-'),
      illustration: null,
      isCustom: true,
      archived: false,
      ownerId: user.id,
    };
    db.state.exercises.push(e);
    return HttpResponse.json(toExercise(e), { status: 201 });
  })),

  http.put(`${API}/exercises/:id`, authed('trainer', async (ctx) => {
    const e = visibleExercises(ctx.user).find((x) => x.id === param(ctx, 'id'));
    if (!e) return notFound('Exercise');
    if (e.ownerId !== ctx.user.id) return problem(403, 'Exercise.NotEditable', 'Library exercises cannot be edited.');
    const b = await body<T.UpsertExerciseRequest>(ctx.request);
    const errors = validateExercise(b);
    if (Object.keys(errors).length) return validation(errors);
    Object.assign(e, b, { name: b.name.trim() });
    return HttpResponse.json(toExercise(e));
  })),

  http.delete(`${API}/exercises/:id`, authed('trainer', (ctx) => {
    const e = visibleExercises(ctx.user).find((x) => x.id === param(ctx, 'id'));
    if (!e) return notFound('Exercise');
    if (e.ownerId !== ctx.user.id) return problem(403, 'Exercise.NotEditable', 'Library exercises cannot be deleted.');
    e.archived = true;
    return noContent();
  })),

  // Workouts
  http.get(`${API}/workouts`, authed('trainer', ({ user, url }) => {
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const sort = url.searchParams.get('sort') ?? 'name';
    const list = db.state.workouts
      .filter((w) => w.trainerId === user.id && (url.searchParams.get('includeArchived') === 'true' || !w.archived))
      .filter((w) => !search || w.name.toLowerCase().includes(search))
      .sort((a, b) => (sort === '-updatedAt' ? b.updatedAt.localeCompare(a.updatedAt) : a.name.localeCompare(b.name)))
      .map(toWorkoutSummary);
    return HttpResponse.json(paged(list, url));
  })),

  http.get(`${API}/workouts/:id`, authed('trainer', (ctx) => {
    const w = db.state.workouts.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    return w ? HttpResponse.json(toWorkout(w)) : notFound('Workout');
  })),

  http.post(`${API}/workouts`, authed('trainer', async ({ request, user }) => {
    const b = await body<T.UpsertWorkoutRequest>(request);
    const errors = validateWorkout(b, user);
    if (Object.keys(errors).length) return validation(errors);
    const w = {
      id: newId(),
      trainerId: user.id,
      name: b.name.trim(),
      description: b.description ?? null,
      archived: false,
      createdAt: nowIso(),
      updatedAt: nowIso(),
      exercises: toDbWorkoutExercises(b.exercises),
    };
    db.state.workouts.push(w);
    return HttpResponse.json(toWorkout(w), { status: 201 });
  })),

  http.put(`${API}/workouts/:id`, authed('trainer', async (ctx) => {
    const w = db.state.workouts.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    if (!w) return notFound('Workout');
    const b = await body<T.UpsertWorkoutRequest>(ctx.request);
    const errors = validateWorkout(b, ctx.user);
    if (Object.keys(errors).length) return validation(errors);
    Object.assign(w, { name: b.name.trim(), description: b.description ?? null, exercises: toDbWorkoutExercises(b.exercises), updatedAt: nowIso() });
    return HttpResponse.json(toWorkout(w));
  })),

  http.post(`${API}/workouts/:id/duplicate`, authed('trainer', (ctx) => {
    const w = db.state.workouts.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    if (!w) return notFound('Workout');
    const copy = { ...structuredClone(w), id: newId(), name: `${w.name} (copy)`, createdAt: nowIso(), updatedAt: nowIso() };
    copy.exercises = copy.exercises.map((e) => ({ ...e, id: newId() }));
    db.state.workouts.push(copy);
    return HttpResponse.json(toWorkout(copy), { status: 201 });
  })),

  http.delete(`${API}/workouts/:id`, authed('trainer', (ctx) => {
    const w = db.state.workouts.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    if (!w) return notFound('Workout');
    w.archived = true;
    return noContent();
  })),

  // Programs
  http.get(`${API}/programs`, authed('trainer', ({ user, url }) => {
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const list = db.state.programs
      .filter((p) => p.trainerId === user.id && (url.searchParams.get('includeArchived') === 'true' || !p.archived))
      .filter((p) => !search || p.name.toLowerCase().includes(search))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(toProgramSummary);
    return HttpResponse.json(paged(list, url));
  })),

  http.get(`${API}/programs/:id`, authed('trainer', (ctx) => {
    const p = db.state.programs.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    return p ? HttpResponse.json(toProgram(p)) : notFound('Program');
  })),

  ...(['post', 'put'] as const).map((method) =>
    http[method](method === 'post' ? `${API}/programs` : `${API}/programs/:id`, authed('trainer', async (ctx) => {
      const b = await body<T.UpsertProgramRequest>(ctx.request);
      const errors: Record<string, string[]> = {};
      checkName(b.name, 'name', errors);
      if (!(b.weeks >= 1 && b.weeks <= 52)) errors.weeks = ['Weeks must be between 1 and 52.'];
      if (Object.keys(errors).length) return validation(errors);
      if (b.slots.some((s) => s.week < 1 || s.week > b.weeks || s.day < 1 || s.day > 7))
        return problem(422, 'Program.SlotOutOfRange', 'A slot falls outside the program weeks.');
      const owned = new Set(db.state.workouts.filter((w) => w.trainerId === ctx.user.id).map((w) => w.id));
      const missing = b.slots.findIndex((s) => !owned.has(s.workoutId));
      if (missing >= 0) return validation({ [`slots[${missing}].workoutId`]: ['Workout was not found.'] });
      const slots = b.slots.map((s) => ({ ...s, id: newId() }));
      if (method === 'post') {
        const p = { id: newId(), trainerId: ctx.user.id, name: b.name.trim(), description: b.description ?? null, weeks: b.weeks, archived: false, createdAt: nowIso(), updatedAt: nowIso(), slots };
        db.state.programs.push(p);
        return HttpResponse.json(toProgram(p), { status: 201 });
      }
      const p = db.state.programs.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
      if (!p) return notFound('Program');
      Object.assign(p, { name: b.name.trim(), description: b.description ?? null, weeks: b.weeks, slots, updatedAt: nowIso() });
      return HttpResponse.json(toProgram(p));
    })),
  ),

  http.delete(`${API}/programs/:id`, authed('trainer', (ctx) => {
    const p = db.state.programs.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    if (!p) return notFound('Program');
    p.archived = true;
    return noContent();
  })),

  http.post(`${API}/programs/:id/assign`, authed('trainer', async (ctx) => {
    const p = db.state.programs.find((x) => x.id === param(ctx, 'id') && x.trainerId === ctx.user.id);
    if (!p) return notFound('Program');
    const b = await body<T.AssignProgramRequest>(ctx.request);
    const errors: Record<string, string[]> = {};
    if (!b.clientIds?.length) errors.clientIds = ['Pick at least one client.'];
    if (!/^\d{4}-\d{2}-\d{2}$/.test(b.startDate ?? '')) errors.startDate = ['Start date is required.'];
    if (Object.keys(errors).length) return validation(errors);
    const out: T.ProgramAssignment[] = [];
    for (const clientId of b.clientIds) {
      const c = clientFor(ctx.user, clientId);
      if (!c) return notFound('Client');
      if (c.status === 'archived') return problem(422, 'Client.Archived', 'Archived clients cannot get new assignments.');
    }
    for (const clientId of b.clientIds) {
      const pa: T.ProgramAssignment = {
        id: newId(),
        programId: p.id,
        programName: p.name,
        clientId,
        startDate: b.startDate,
        endDate: addDaysIso(b.startDate, p.weeks * 7 - 1),
        assignmentCount: p.slots.length,
        createdAt: nowIso(),
      };
      db.state.programAssignments.push(pa);
      for (const slot of p.slots) {
        const w = db.state.workouts.find((x) => x.id === slot.workoutId);
        if (!w) continue;
        db.state.assignments.push({
          id: newId(),
          clientId,
          date: addDaysIso(b.startDate, (slot.week - 1) * 7 + slot.day - 1),
          status: 'scheduled',
          workoutId: w.id,
          snapshot: snapshotWorkout(w),
          programAssignmentId: pa.id,
          notes: null,
          sessionId: null,
          completedAt: null,
          createdAt: nowIso(),
        });
      }
      out.push(pa);
    }
    return HttpResponse.json(out, { status: 201 });
  })),

  http.get(`${API}/clients/:clientId/program-assignments`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'clientId'));
    if (!c) return notFound('Client');
    return HttpResponse.json(db.state.programAssignments.filter((p) => p.clientId === c.id).sort((a, b) => b.startDate.localeCompare(a.startDate)));
  })),

  http.delete(`${API}/program-assignments/:id`, authed('trainer', (ctx) => {
    const pa = db.state.programAssignments.find((p) => p.id === param(ctx, 'id'));
    if (!pa || !clientFor(ctx.user, pa.clientId)) return notFound('ProgramAssignment');
    const today = todayIso();
    db.state.assignments = db.state.assignments.filter(
      (a) => !(a.programAssignmentId === pa.id && a.status === 'scheduled' && !a.sessionId && a.date >= today),
    );
    db.state.programAssignments = db.state.programAssignments.filter((p) => p.id !== pa.id);
    return noContent();
  })),

  // Assignments
  http.get(`${API}/assignments`, authed('any', ({ user, url }) => {
    const today = todayIso();
    const from = url.searchParams.get('from') ?? addDaysIso(today, -7);
    const to = url.searchParams.get('to') ?? addDaysIso(today, 28);
    const clientId = user.role === 'client' ? user.id : url.searchParams.get('clientId');
    const status = url.searchParams.get('status');
    const visibleClients = new Set(db.state.clients.filter((c) => clientFor(user, c.id)).map((c) => c.id));
    const list = db.state.assignments
      .filter((a) => visibleClients.has(a.clientId) && (!clientId || a.clientId === clientId))
      .filter((a) => a.date >= from && a.date <= to)
      .filter((a) => !status || assignmentStatus(a) === status)
      .sort((a, b) => a.date.localeCompare(b.date))
      .map(toAssignment);
    return HttpResponse.json(list);
  })),

  http.get(`${API}/assignments/:id`, authed('any', (ctx) => {
    const a = visibleAssignment(ctx.user, param(ctx, 'id'));
    return a ? HttpResponse.json(toAssignment(a)) : notFound('Assignment');
  })),

  http.post(`${API}/assignments`, authed('trainer', async ({ request, user }) => {
    const b = await body<T.CreateAssignmentRequest>(request);
    const c = clientFor(user, b.clientId);
    if (!c) return notFound('Client');
    if (c.status === 'archived') return problem(422, 'Client.Archived', 'Archived clients cannot get new assignments.');
    const w = db.state.workouts.find((x) => x.id === b.workoutId && x.trainerId === user.id);
    if (!w) return notFound('Workout');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(b.date)) return validation({ date: ['Date is required.'] });
    const a: DbAssignment = {
      id: newId(),
      clientId: c.id,
      date: b.date,
      status: 'scheduled',
      workoutId: w.id,
      snapshot: snapshotWorkout(w),
      programAssignmentId: null,
      notes: b.notes ?? null,
      sessionId: null,
      completedAt: null,
      createdAt: nowIso(),
    };
    db.state.assignments.push(a);
    return HttpResponse.json(toAssignment(a), { status: 201 });
  })),

  http.put(`${API}/assignments/:id`, authed('trainer', async (ctx) => {
    const a = visibleAssignment(ctx.user, param(ctx, 'id'));
    if (!a) return notFound('Assignment');
    if (a.status !== 'scheduled' || a.sessionId) return problem(409, 'Assignment.NotScheduled', 'Only scheduled workouts can be changed.');
    const b = await body<T.UpdateAssignmentRequest>(ctx.request);
    a.date = b.date;
    a.notes = b.notes;
    return HttpResponse.json(toAssignment(a));
  })),

  http.delete(`${API}/assignments/:id`, authed('trainer', (ctx) => {
    const a = visibleAssignment(ctx.user, param(ctx, 'id'));
    if (!a) return notFound('Assignment');
    if (a.status !== 'scheduled' || a.sessionId) return problem(409, 'Assignment.NotScheduled', 'Only scheduled workouts can be removed.');
    db.state.assignments = db.state.assignments.filter((x) => x.id !== a.id);
    return noContent();
  })),

  http.post(`${API}/assignments/:id/skip`, authed('any', (ctx) => {
    const a = visibleAssignment(ctx.user, param(ctx, 'id'));
    if (!a) return notFound('Assignment');
    if (a.status !== 'scheduled' || a.sessionId) return problem(409, 'Assignment.NotScheduled', 'Only scheduled workouts can be skipped.');
    a.status = 'skipped';
    return HttpResponse.json(toAssignment(a));
  })),

  // Sessions
  http.post(`${API}/sessions`, authed('client', async ({ request, user }) => {
    const b = await body<T.StartSessionRequest>(request);
    if (b.id) {
      const existing = db.state.sessions.find((s) => s.id === b.id && s.clientId === user.id);
      if (existing) return HttpResponse.json(toSession(existing));
    }
    let assignment: DbAssignment | null = null;
    if (b.assignmentId) {
      assignment = visibleAssignment(user, b.assignmentId);
      if (!assignment) return notFound('Assignment');
      if (assignment.sessionId)
        return problem(409, 'Session.AlreadyStartedForAssignment', 'A session was already started for this workout.', undefined, { sessionId: assignment.sessionId });
      if (assignment.status !== 'scheduled') return problem(409, 'Assignment.NotScheduled', 'This workout is not scheduled anymore.');
    } else if (!b.workoutName?.trim()) {
      return validation({ workoutName: ['Name the workout or start it from an assignment.'] });
    }
    const s: DbSession = {
      id: b.id ?? newId(),
      clientId: user.id,
      assignmentId: assignment?.id ?? null,
      workoutName: assignment?.snapshot.name ?? (b.workoutName ?? '').trim(),
      status: 'in-progress',
      startedAt: b.startedAt ?? nowIso(),
      completedAt: null,
      notes: null,
      plan: assignment ? planFromSnapshot(assignment.snapshot) : [],
      sets: [],
      personalRecords: [],
    };
    if (assignment) assignment.sessionId = s.id;
    db.state.sessions.push(s);
    return HttpResponse.json(toSession(s), { status: 201 });
  })),

  http.get(`${API}/sessions`, authed('any', ({ user, url }) => {
    const clientId = user.role === 'client' ? user.id : url.searchParams.get('clientId');
    const status = url.searchParams.get('status');
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const list = db.state.sessions
      .filter((s) => clientFor(user, s.clientId) && (!clientId || s.clientId === clientId))
      .filter((s) => !status || s.status === status)
      .filter((s) => (!from || s.startedAt.slice(0, 10) >= from) && (!to || s.startedAt.slice(0, 10) <= to))
      .sort((a, b) => b.startedAt.localeCompare(a.startedAt))
      .map(toSessionSummary);
    return HttpResponse.json(paged(list, url));
  })),

  http.get(`${API}/sessions/:id`, authed('any', (ctx) => {
    const s = visibleSession(ctx.user, param(ctx, 'id'));
    return s ? HttpResponse.json(toSession(s)) : notFound('Session');
  })),

  http.put(`${API}/sessions/:id`, authed('client', async (ctx) => {
    const s = visibleSession(ctx.user, param(ctx, 'id'));
    if (!s) return notFound('Session');
    const b = await body<{ workoutName: string; notes: string | null }>(ctx.request);
    s.workoutName = b.workoutName || s.workoutName;
    s.notes = b.notes;
    return HttpResponse.json(toSession(s));
  })),

  http.put(`${API}/sessions/:id/sets/:setId`, authed('client', async (ctx) => {
    const s = visibleSession(ctx.user, param(ctx, 'id'));
    if (!s) return notFound('Session');
    if (s.status === 'completed') return problem(409, 'Session.Completed', 'This session is already completed.');
    const b = await body<T.PutSetRequest>(ctx.request);
    const errors: Record<string, string[]> = {};
    if (!(b.setNumber >= 1 && b.setNumber <= 50)) errors.setNumber = ['Set number must be between 1 and 50.'];
    if (b.weightKg !== null && !(b.weightKg >= 0 && b.weightKg <= 1000)) errors.weightKg = ['Weight must be between 0 and 1000 kg.'];
    if (b.reps !== null && !(b.reps >= 0 && b.reps <= 1000)) errors.reps = ['Reps must be between 0 and 1000.'];
    if (b.rpe !== null && !(b.rpe >= 1 && b.rpe <= 10)) errors.rpe = ['RPE must be between 1 and 10.'];
    if (Object.keys(errors).length) return validation(errors);
    const setId = param(ctx, 'setId');
    const set: T.SessionSet = { id: setId, ...b, updatedAt: nowIso() };
    const i = s.sets.findIndex((x) => x.id === setId);
    if (i >= 0) s.sets[i] = set;
    else s.sets.push(set);
    return HttpResponse.json(set);
  })),

  http.delete(`${API}/sessions/:id/sets/:setId`, authed('client', (ctx) => {
    const s = visibleSession(ctx.user, param(ctx, 'id'));
    if (!s) return notFound('Session');
    if (s.status === 'completed') return problem(409, 'Session.Completed', 'This session is already completed.');
    s.sets = s.sets.filter((x) => x.id !== param(ctx, 'setId'));
    return noContent();
  })),

  http.post(`${API}/sessions/:id/complete`, authed('client', async (ctx) => {
    const s = visibleSession(ctx.user, param(ctx, 'id'));
    if (!s) return notFound('Session');
    const b = await body<T.CompleteSessionRequest>(ctx.request);
    completeSession(s, b.completedAt ?? nowIso(), b.notes);
    return HttpResponse.json(toSession(s));
  })),

  http.delete(`${API}/sessions/:id`, authed('client', (ctx) => {
    const s = visibleSession(ctx.user, param(ctx, 'id'));
    if (!s) return noContent();
    if (s.status === 'completed') return problem(409, 'Session.Completed', 'Completed sessions cannot be discarded.');
    const a = s.assignmentId ? db.state.assignments.find((x) => x.id === s.assignmentId) : null;
    if (a) a.sessionId = null;
    db.state.sessions = db.state.sessions.filter((x) => x.id !== s.id);
    return noContent();
  })),

  // Clients
  http.get(`${API}/clients`, authed('trainer', ({ user, url }) => {
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const status = url.searchParams.get('status');
    const sort = url.searchParams.get('sort') ?? 'name';
    const list = db.state.clients
      .filter((c) => c.trainerId === user.id && (!status || c.status === status))
      .map(clientSummary)
      .filter((c) => !search || `${c.firstName} ${c.lastName} ${c.email}`.toLowerCase().includes(search));
    list.sort((a, b) => {
      if (sort === '-lastSessionAt') return (b.lastSessionAt ?? '').localeCompare(a.lastSessionAt ?? '');
      if (sort === 'adherence7d') return (a.adherence7d ?? 2) - (b.adherence7d ?? 2);
      return `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`);
    });
    return HttpResponse.json(paged(list, url));
  })),

  http.get(`${API}/clients/:id`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    return c ? HttpResponse.json(clientDetail(c, ctx.user)) : notFound('Client');
  })),

  http.put(`${API}/clients/:id`, authed('trainer', async (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    const b = await body<T.UpdateClientRequest>(ctx.request);
    c.goals = b.goals;
    c.trainerNotes = b.trainerNotes;
    return HttpResponse.json(clientDetail(c, ctx.user));
  })),

  ...(['archive', 'unarchive'] as const).map((action) =>
    http.post(`${API}/clients/:id/${action}`, authed('trainer', (ctx) => {
      const c = clientFor(ctx.user, param(ctx, 'id'));
      if (!c) return notFound('Client');
      c.status = action === 'archive' ? 'archived' : 'active';
      if (action === 'archive') {
        const today = todayIso();
        db.state.assignments = db.state.assignments.filter((a) => !(a.clientId === c.id && a.status === 'scheduled' && a.date >= today && !a.sessionId));
      }
      return HttpResponse.json(clientDetail(c, ctx.user));
    })),
  ),

  http.get(`${API}/clients/:id/personal-records`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    return c ? HttpResponse.json(currentBests(c.id)) : notFound('Client');
  })),

  http.get(`${API}/clients/:id/personal-records/history`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    const exerciseId = ctx.url.searchParams.get('exerciseId');
    const list = db.state.personalRecords
      .filter((p) => p.clientId === c.id && (!exerciseId || p.exerciseId === exerciseId))
      .sort((a, b) => b.achievedAt.localeCompare(a.achievedAt));
    return HttpResponse.json(paged(list, ctx.url));
  })),

  http.get(`${API}/clients/:id/progress/exercises`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    const map = new Map<string, T.ProgressExercise>();
    for (const s of db.state.sessions.filter((x) => x.clientId === c.id && x.status === 'completed')) {
      for (const exId of new Set(s.sets.filter((x) => x.completed).map((x) => x.exerciseId))) {
        const cur = map.get(exId);
        const at = s.completedAt ?? s.startedAt;
        if (cur) {
          cur.sessionCount += 1;
          if (at > cur.lastPerformedAt) cur.lastPerformedAt = at;
        } else {
          map.set(exId, { exerciseId: exId, exercise: { ...db.state.exercises.find((e) => e.id === exId)! }, sessionCount: 1, lastPerformedAt: at });
        }
      }
    }
    const list = [...map.values()]
      .map((p) => ({ ...p, exercise: { id: p.exercise.id, slug: p.exercise.slug, name: p.exercise.name, illustration: p.exercise.illustration } }))
      .sort((a, b) => b.sessionCount - a.sessionCount || a.exercise.name.localeCompare(b.exercise.name));
    return HttpResponse.json(list);
  })),

  http.get(`${API}/clients/:id/progress/exercises/:exerciseId`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    const exId = param(ctx, 'exerciseId');
    const e = db.state.exercises.find((x) => x.id === exId);
    if (!e) return notFound('Exercise');
    const points: T.ProgressPoint[] = db.state.sessions
      .filter((s) => s.clientId === c.id && s.status === 'completed')
      .sort((a, b) => a.startedAt.localeCompare(b.startedAt))
      .flatMap((s) => {
        const sets = s.sets.filter((x) => x.completed && x.exerciseId === exId);
        if (!sets.length) return [];
        let e1rm: number | null = null;
        let top: T.SessionSet | null = null;
        for (const x of sets) {
          const w = x.weightKg ?? 0;
          const r = x.reps ?? 0;
          const est = r >= 1 && r <= 12 && w > 0 ? (r === 1 ? w : Math.round(w * (1 + r / 30) * 100) / 100) : null;
          if (est !== null && (e1rm === null || est > e1rm)) e1rm = est;
          if (!top || w > (top.weightKg ?? 0)) top = x;
        }
        return [{
          date: (s.completedAt ?? s.startedAt).slice(0, 10),
          sessionId: s.id,
          estimatedOneRepMaxKg: e1rm,
          topSetWeightKg: top?.weightKg ?? null,
          topSetReps: top?.reps ?? null,
          volumeKg: Math.round(sets.reduce((sum, x) => sum + (x.weightKg ?? 0) * (x.reps ?? 0), 0) * 100) / 100,
          totalReps: sets.reduce((sum, x) => sum + (x.reps ?? 0), 0),
          completedSets: sets.length,
        }];
      });
    return HttpResponse.json({ exercise: { id: e.id, slug: e.slug, name: e.name, illustration: e.illustration }, points } satisfies T.ExerciseProgress);
  })),

  http.get(`${API}/clients/:id/progress/summary`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    const weeksCount = Number(ctx.url.searchParams.get('weeks') ?? 12);
    const today = todayIso();
    const sessions = db.state.sessions.filter((s) => s.clientId === c.id && s.status === 'completed');
    const thisWeek = addDaysIso(today, -((new Date(`${today}T12:00:00`).getDay() + 6) % 7));
    const weeks = Array.from({ length: weeksCount }, (_, i) => addDaysIso(thisWeek, -(weeksCount - 1 - i) * 7)).map((weekStart) => {
      const inWeek = sessions.filter((s) => {
        const d = (s.completedAt ?? s.startedAt).slice(0, 10);
        return d >= weekStart && d <= addDaysIso(weekStart, 6);
      });
      return {
        weekStart,
        sessions: inWeek.length,
        volumeKg: Math.round(inWeek.reduce((sum, s) => sum + toSessionSummary(s).totalVolumeKg, 0)),
        completedSets: inWeek.reduce((sum, s) => sum + toSessionSummary(s).completedSets, 0),
      };
    });
    return HttpResponse.json({
      weeks,
      totalSessions: sessions.length,
      adherence7d: adherence(c.id, 7).rate,
      adherence28d: adherence(c.id, 28).rate,
      streakWeeks: streakWeeks(c.id),
    } satisfies T.ProgressSummary);
  })),

  http.get(`${API}/clients/:id/body-metrics`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    return HttpResponse.json(db.state.bodyMetrics.filter((m) => m.clientId === c.id).sort((a, b) => a.date.localeCompare(b.date)));
  })),

  http.put(`${API}/clients/:id/body-metrics/:date`, authed('any', async (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    const date = param(ctx, 'date');
    const b = await body<T.BodyMetricValues>(ctx.request);
    const errors: Record<string, string[]> = {};
    if (date > todayIso()) errors.date = ['Date cannot be in the future.'];
    const measures = ['weightKg', 'bodyFatPercent', 'waistCm', 'chestCm', 'hipsCm', 'armCm', 'thighCm', 'restingHeartRate'] as const;
    if (!measures.some((k) => b[k] !== null && b[k] !== undefined)) errors.weightKg = ['Enter at least one measurement.'];
    if (b.weightKg != null && (b.weightKg < 20 || b.weightKg > 400)) errors.weightKg = ['Weight must be between 20 and 400 kg.'];
    if (b.bodyFatPercent != null && (b.bodyFatPercent < 2 || b.bodyFatPercent > 75)) errors.bodyFatPercent = ['Body fat must be between 2 and 75%.'];
    if (b.restingHeartRate != null && (b.restingHeartRate < 25 || b.restingHeartRate > 250)) errors.restingHeartRate = ['Resting heart rate must be between 25 and 250.'];
    for (const k of ['waistCm', 'chestCm', 'hipsCm', 'armCm', 'thighCm'] as const) {
      const v = b[k];
      if (v != null && (v < 10 || v > 300)) errors[k] = ['Must be between 10 and 300 cm.'];
    }
    if (Object.keys(errors).length) return validation(errors);
    const metric: T.BodyMetric = {
      clientId: c.id,
      date,
      weightKg: b.weightKg ?? null,
      bodyFatPercent: b.bodyFatPercent ?? null,
      waistCm: b.waistCm ?? null,
      chestCm: b.chestCm ?? null,
      hipsCm: b.hipsCm ?? null,
      armCm: b.armCm ?? null,
      thighCm: b.thighCm ?? null,
      restingHeartRate: b.restingHeartRate ?? null,
      notes: b.notes ?? null,
      recordedBy: ctx.user.role,
      updatedAt: nowIso(),
    };
    db.state.bodyMetrics = [...db.state.bodyMetrics.filter((m) => !(m.clientId === c.id && m.date === date)), metric];
    return HttpResponse.json(metric);
  })),

  http.delete(`${API}/clients/:id/body-metrics/:date`, authed('any', (ctx) => {
    const c = clientFor(ctx.user, param(ctx, 'id'));
    if (!c) return notFound('Client');
    db.state.bodyMetrics = db.state.bodyMetrics.filter((m) => !(m.clientId === c.id && m.date === param(ctx, 'date')));
    return noContent();
  })),

  // Dashboards
  http.get(`${API}/dashboard/trainer`, authed('trainer', ({ user }) => {
    const today = todayIso();
    const active = db.state.clients.filter((c) => c.trainerId === user.id && c.status === 'active');
    const activeIds = new Set(active.map((c) => c.id));
    const todayAssignments = db.state.assignments
      .filter((a) => activeIds.has(a.clientId) && a.date === today)
      .map((a) => ({
        assignmentId: a.id,
        client: personRef(a.clientId),
        workoutName: a.snapshot.name,
        status: assignmentStatus(a),
        sessionId: a.sessionId,
        completedAt: a.completedAt,
      }));
    const adherenceRows = active
      .map((c) => {
        const a7 = adherence(c.id, 7);
        const a28 = adherence(c.id, 28);
        return {
          client: personRef(c.id),
          adherence7d: a7.rate,
          adherence28d: a28.rate,
          due7d: a7.due,
          completed7d: a7.completed,
          due28d: a28.due,
          completed28d: a28.completed,
          lastSessionAt: clientSummary(c).lastSessionAt,
        };
      })
      .sort((a, b) => (a.adherence7d ?? 2) - (b.adherence7d ?? 2) || (a.adherence28d ?? 2) - (b.adherence28d ?? 2));
    const since = addDaysIso(today, -14);
    const prs = db.state.personalRecords
      .filter((p) => activeIds.has(p.clientId) && p.achievedAt.slice(0, 10) >= since)
      .sort((a, b) => b.achievedAt.localeCompare(a.achievedAt))
      .slice(0, 20);
    const pending = db.state.invitations
      .filter((i) => i.trainerId === user.id)
      .map(toInvitation)
      .filter((i) => i.status === 'pending')
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    const recentSessions = db.state.sessions
      .filter((s) => activeIds.has(s.clientId))
      .sort((a, b) => b.startedAt.localeCompare(a.startedAt))
      .slice(0, 10)
      .map((s) => ({ ...toSessionSummary(s), client: personRef(s.clientId) }));
    return HttpResponse.json({
      date: today,
      activeClients: active.length,
      today: todayAssignments,
      adherence: adherenceRows,
      recentPersonalRecords: prsWithClient(prs),
      pendingInvitations: { count: pending.length, items: pending.slice(0, 10) },
      recentSessions,
    } satisfies T.TrainerDashboard);
  })),

  http.get(`${API}/dashboard/client`, authed('client', ({ user }) => {
    const today = todayIso();
    const mine = db.state.assignments.filter((a) => a.clientId === user.id);
    const next = mine.filter((a) => a.status === 'scheduled' && a.date > today).sort((a, b) => a.date.localeCompare(b.date))[0];
    const inProgress = db.state.sessions.find((s) => s.clientId === user.id && s.status === 'in-progress');
    const weekStart = addDaysIso(today, -((new Date(`${today}T12:00:00`).getDay() + 6) % 7));
    const weekDue = mine.filter((a) => a.date >= weekStart && a.date <= today && !(a.date === today && a.status === 'scheduled'));
    const weekCompleted = weekDue.filter((a) => a.status === 'completed').length;
    const since = addDaysIso(today, -14);
    const metrics = db.state.bodyMetrics.filter((m) => m.clientId === user.id).sort((a, b) => b.date.localeCompare(a.date));
    return HttpResponse.json({
      date: today,
      today: mine.filter((a) => a.date === today).map(toAssignment),
      nextAssignment: next ? toAssignment(next) : null,
      inProgressSession: inProgress ? toSessionSummary(inProgress) : null,
      thisWeek: { due: weekDue.length, completed: weekCompleted, rate: weekDue.length ? Math.round((weekCompleted / weekDue.length) * 100) / 100 : null },
      adherence7d: adherence(user.id, 7).rate,
      adherence28d: adherence(user.id, 28).rate,
      streakWeeks: streakWeeks(user.id),
      recentPersonalRecords: db.state.personalRecords
        .filter((p) => p.clientId === user.id && p.achievedAt.slice(0, 10) >= since)
        .sort((a, b) => b.achievedAt.localeCompare(a.achievedAt)),
      recentSessions: db.state.sessions
        .filter((s) => s.clientId === user.id)
        .sort((a, b) => b.startedAt.localeCompare(a.startedAt))
        .slice(0, 5)
        .map(toSessionSummary),
      latestBodyMetric: metrics[0] ?? null,
    } satisfies T.ClientDashboard);
  })),
];
