import { createFileRoute } from '@tanstack/react-router';
import { queries } from '@/lib/api/queries';
import { WorkoutBuilderPage } from '@/features/workouts/workout-builder-page';

/** `new` opens an empty builder. */
export const Route = createFileRoute('/_authed/_shell/_trainer/workouts/$workoutId')({
  loader: ({ context, params }) =>
    params.workoutId === 'new' ? undefined : context.queryClient.ensureQueryData(queries.workout(params.workoutId)),
  head: () => ({ meta: [{ title: 'Workout builder · TrainerSphere' }] }),
  component: WorkoutBuilderPage,
});
