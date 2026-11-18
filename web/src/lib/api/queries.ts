import { keepPreviousData, queryOptions } from '@tanstack/react-query';
import {
  assignmentsApi,
  clientsApi,
  dashboardApi,
  exercisesApi,
  invitationsApi,
  programsApi,
  sessionsApi,
  workoutsApi,
} from './endpoints';
import type { AssignmentQuery, ClientQuery, ExerciseQuery, SessionQuery } from './types';

/** Query keys are hierarchical so `invalidateQueries({ queryKey: keys.clients })` hits every client query. */
export const keys = {
  me: ['me'] as const,
  dashboard: ['dashboard'] as const,
  invitations: ['invitations'] as const,
  exercises: ['exercises'] as const,
  taxonomy: ['exercises', 'taxonomy'] as const,
  workouts: ['workouts'] as const,
  programs: ['programs'] as const,
  assignments: ['assignments'] as const,
  sessions: ['sessions'] as const,
  clients: ['clients'] as const,
};

export const queries = {
  trainerDashboard: () => queryOptions({ queryKey: [...keys.dashboard, 'trainer'], queryFn: dashboardApi.trainer }),
  clientDashboard: () => queryOptions({ queryKey: [...keys.dashboard, 'client'], queryFn: dashboardApi.client }),

  invitations: (status: 'pending' | undefined = 'pending') =>
    queryOptions({
      queryKey: [...keys.invitations, { status }],
      queryFn: () => invitationsApi.list({ status, pageSize: 100 }),
    }),

  exercises: (q: ExerciseQuery) =>
    queryOptions({
      queryKey: [...keys.exercises, 'list', q],
      queryFn: ({ signal }) => exercisesApi.list({ pageSize: 200, ...q }, signal),
      placeholderData: keepPreviousData,
    }),
  exercise: (id: string) => queryOptions({ queryKey: [...keys.exercises, 'detail', id], queryFn: () => exercisesApi.get(id) }),
  taxonomy: () => queryOptions({ queryKey: keys.taxonomy, queryFn: exercisesApi.taxonomy, staleTime: Infinity }),

  workouts: (search = '') =>
    queryOptions({
      queryKey: [...keys.workouts, 'list', search],
      queryFn: () => workoutsApi.list({ search, sort: '-updatedAt', pageSize: 100 }),
      placeholderData: keepPreviousData,
    }),
  workout: (id: string) => queryOptions({ queryKey: [...keys.workouts, 'detail', id], queryFn: () => workoutsApi.get(id) }),

  programs: (search = '') =>
    queryOptions({
      queryKey: [...keys.programs, 'list', search],
      queryFn: () => programsApi.list({ search, pageSize: 100 }),
      placeholderData: keepPreviousData,
    }),
  program: (id: string) => queryOptions({ queryKey: [...keys.programs, 'detail', id], queryFn: () => programsApi.get(id) }),

  assignments: (q: AssignmentQuery) =>
    queryOptions({ queryKey: [...keys.assignments, q], queryFn: () => assignmentsApi.list(q), placeholderData: keepPreviousData }),
  assignment: (id: string) =>
    queryOptions({ queryKey: [...keys.assignments, 'detail', id], queryFn: () => assignmentsApi.get(id) }),

  sessions: (q: SessionQuery) =>
    queryOptions({ queryKey: [...keys.sessions, 'list', q], queryFn: () => sessionsApi.list(q), placeholderData: keepPreviousData }),
  session: (id: string) => queryOptions({ queryKey: [...keys.sessions, 'detail', id], queryFn: () => sessionsApi.get(id) }),

  clients: (q: ClientQuery) =>
    queryOptions({
      queryKey: [...keys.clients, 'list', q],
      queryFn: () => clientsApi.list({ pageSize: 100, ...q }),
      placeholderData: keepPreviousData,
    }),
  client: (id: string) => queryOptions({ queryKey: [...keys.clients, id, 'detail'], queryFn: () => clientsApi.get(id) }),
  clientProgramAssignments: (id: string) =>
    queryOptions({ queryKey: [...keys.clients, id, 'program-assignments'], queryFn: () => programsApi.clientAssignments(id) }),
  personalRecords: (id: string) =>
    queryOptions({ queryKey: [...keys.clients, id, 'prs'], queryFn: () => clientsApi.personalRecords(id) }),
  progressExercises: (id: string) =>
    queryOptions({ queryKey: [...keys.clients, id, 'progress', 'exercises'], queryFn: () => clientsApi.progressExercises(id) }),
  exerciseProgress: (id: string, exerciseId: string) =>
    queryOptions({
      queryKey: [...keys.clients, id, 'progress', 'exercise', exerciseId],
      queryFn: () => clientsApi.exerciseProgress(id, exerciseId),
    }),
  progressSummary: (id: string, weeks = 12) =>
    queryOptions({ queryKey: [...keys.clients, id, 'progress', 'summary', weeks], queryFn: () => clientsApi.progressSummary(id, weeks) }),
  bodyMetrics: (id: string) =>
    queryOptions({ queryKey: [...keys.clients, id, 'body-metrics'], queryFn: () => clientsApi.bodyMetrics(id) }),
};
