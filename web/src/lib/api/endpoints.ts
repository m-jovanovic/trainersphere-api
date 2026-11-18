import { api } from './http';
import type * as T from './types';

const json = (method: 'POST' | 'PUT', body?: unknown) => ({ method, body }) as const;

export const authApi = {
  login: (body: T.LoginRequest) => api<T.AuthResponse>('/auth/login', { ...json('POST', body), auth: false }),
  signUp: (body: T.SignUpRequest) => api<T.AuthResponse>('/auth/signup', { ...json('POST', body), auth: false }),
  logout: () => api<undefined>('/auth/logout', { method: 'POST', auth: false }),
  me: () => api<T.User>('/auth/me'),
  updateMe: (body: T.UpdateMeRequest) => api<T.User>('/auth/me', json('PUT', body)),
  changePassword: (body: T.ChangePasswordRequest) => api<undefined>('/auth/change-password', json('POST', body)),
  forgotPassword: (email: string) => api<undefined>('/auth/forgot-password', { ...json('POST', { email }), auth: false }),
  resetPassword: (body: T.ResetPasswordRequest) =>
    api<undefined>('/auth/reset-password', { ...json('POST', body), auth: false }),
  previewInvitation: (token: string) =>
    api<T.InvitationPreview>(`/invitations/${encodeURIComponent(token)}`, { auth: false }),
  acceptInvitation: (token: string, body: T.AcceptInvitationRequest) =>
    api<T.AuthResponse>(`/invitations/${encodeURIComponent(token)}/accept`, { ...json('POST', body), auth: false }),
};

export const invitationsApi = {
  list: (q: { status?: T.InvitationStatus } & T.PageQuery = {}) =>
    api<T.Paged<T.Invitation>>('/invitations', { query: { ...q } }),
  create: (body: T.InviteClientRequest) => api<T.Invitation>('/invitations', json('POST', body)),
  resend: (id: string) => api<T.Invitation>(`/invitations/${id}/resend`, { method: 'POST' }),
  cancel: (id: string) => api<undefined>(`/invitations/${id}`, { method: 'DELETE' }),
};

export const exercisesApi = {
  list: (q: T.ExerciseQuery = {}, signal?: AbortSignal) =>
    api<T.Paged<T.Exercise>>('/exercises', { query: { ...q }, signal }),
  taxonomy: () => api<T.Taxonomy>('/exercises/taxonomy'),
  get: (id: string) => api<T.Exercise>(`/exercises/${id}`),
  create: (body: T.UpsertExerciseRequest) => api<T.Exercise>('/exercises', json('POST', body)),
  update: (id: string, body: T.UpsertExerciseRequest) => api<T.Exercise>(`/exercises/${id}`, json('PUT', body)),
  archive: (id: string) => api<undefined>(`/exercises/${id}`, { method: 'DELETE' }),
};

export const workoutsApi = {
  list: (q: { search?: string; includeArchived?: boolean; sort?: 'name' | '-updatedAt' } & T.PageQuery = {}) =>
    api<T.Paged<T.WorkoutSummary>>('/workouts', { query: { ...q } }),
  get: (id: string) => api<T.Workout>(`/workouts/${id}`),
  create: (body: T.UpsertWorkoutRequest) => api<T.Workout>('/workouts', json('POST', body)),
  update: (id: string, body: T.UpsertWorkoutRequest) => api<T.Workout>(`/workouts/${id}`, json('PUT', body)),
  duplicate: (id: string) => api<T.Workout>(`/workouts/${id}/duplicate`, { method: 'POST' }),
  archive: (id: string) => api<undefined>(`/workouts/${id}`, { method: 'DELETE' }),
};

export const programsApi = {
  list: (q: { search?: string; includeArchived?: boolean } & T.PageQuery = {}) =>
    api<T.Paged<T.ProgramSummary>>('/programs', { query: { ...q } }),
  get: (id: string) => api<T.Program>(`/programs/${id}`),
  create: (body: T.UpsertProgramRequest) => api<T.Program>('/programs', json('POST', body)),
  update: (id: string, body: T.UpsertProgramRequest) => api<T.Program>(`/programs/${id}`, json('PUT', body)),
  archive: (id: string) => api<undefined>(`/programs/${id}`, { method: 'DELETE' }),
  assign: (id: string, body: T.AssignProgramRequest) =>
    api<T.ProgramAssignment[]>(`/programs/${id}/assign`, json('POST', body)),
  clientAssignments: (clientId: string) => api<T.ProgramAssignment[]>(`/clients/${clientId}/program-assignments`),
  unassign: (programAssignmentId: string) =>
    api<undefined>(`/program-assignments/${programAssignmentId}`, { method: 'DELETE' }),
};

export const assignmentsApi = {
  list: (q: T.AssignmentQuery = {}) => api<T.Assignment[]>('/assignments', { query: { ...q } }),
  get: (id: string) => api<T.Assignment>(`/assignments/${id}`),
  create: (body: T.CreateAssignmentRequest) => api<T.Assignment>('/assignments', json('POST', body)),
  update: (id: string, body: T.UpdateAssignmentRequest) => api<T.Assignment>(`/assignments/${id}`, json('PUT', body)),
  remove: (id: string) => api<undefined>(`/assignments/${id}`, { method: 'DELETE' }),
  skip: (id: string, reason?: string) => api<T.Assignment>(`/assignments/${id}/skip`, json('POST', { reason })),
};

export const sessionsApi = {
  start: (body: T.StartSessionRequest) => api<T.Session>('/sessions', json('POST', body)),
  list: (q: T.SessionQuery = {}) => api<T.Paged<T.SessionSummary>>('/sessions', { query: { ...q } }),
  get: (id: string) => api<T.Session>(`/sessions/${id}`),
  update: (id: string, body: { workoutName: string; notes: string | null }) =>
    api<T.Session>(`/sessions/${id}`, json('PUT', body)),
  putSet: (sessionId: string, setId: string, body: T.PutSetRequest) =>
    api<T.SessionSet>(`/sessions/${sessionId}/sets/${setId}`, json('PUT', body)),
  deleteSet: (sessionId: string, setId: string) =>
    api<undefined>(`/sessions/${sessionId}/sets/${setId}`, { method: 'DELETE' }),
  complete: (id: string, body: T.CompleteSessionRequest = {}) =>
    api<T.Session>(`/sessions/${id}/complete`, json('POST', body)),
  discard: (id: string) => api<undefined>(`/sessions/${id}`, { method: 'DELETE' }),
};

export const clientsApi = {
  list: (q: T.ClientQuery = {}) => api<T.Paged<T.ClientSummary>>('/clients', { query: { ...q } }),
  get: (id: string) => api<T.ClientDetail>(`/clients/${id}`),
  update: (id: string, body: T.UpdateClientRequest) => api<T.ClientDetail>(`/clients/${id}`, json('PUT', body)),
  archive: (id: string) => api<T.ClientDetail>(`/clients/${id}/archive`, { method: 'POST' }),
  unarchive: (id: string) => api<T.ClientDetail>(`/clients/${id}/unarchive`, { method: 'POST' }),
  personalRecords: (id: string) => api<T.PersonalRecord[]>(`/clients/${id}/personal-records`),
  personalRecordHistory: (id: string, q: { exerciseId?: string } & T.PageQuery = {}) =>
    api<T.Paged<T.PersonalRecord>>(`/clients/${id}/personal-records/history`, { query: { ...q } }),
  progressExercises: (id: string) => api<T.ProgressExercise[]>(`/clients/${id}/progress/exercises`),
  exerciseProgress: (id: string, exerciseId: string, q: { from?: string; to?: string } = {}) =>
    api<T.ExerciseProgress>(`/clients/${id}/progress/exercises/${exerciseId}`, { query: q }),
  progressSummary: (id: string, weeks = 12) =>
    api<T.ProgressSummary>(`/clients/${id}/progress/summary`, { query: { weeks } }),
  bodyMetrics: (id: string, q: { from?: string; to?: string } = {}) =>
    api<T.BodyMetric[]>(`/clients/${id}/body-metrics`, { query: q }),
  putBodyMetric: (id: string, date: string, body: T.BodyMetricValues) =>
    api<T.BodyMetric>(`/clients/${id}/body-metrics/${date}`, json('PUT', body)),
  deleteBodyMetric: (id: string, date: string) =>
    api<undefined>(`/clients/${id}/body-metrics/${date}`, { method: 'DELETE' }),
};

export const dashboardApi = {
  trainer: () => api<T.TrainerDashboard>('/dashboard/trainer'),
  client: () => api<T.ClientDashboard>('/dashboard/client'),
};
