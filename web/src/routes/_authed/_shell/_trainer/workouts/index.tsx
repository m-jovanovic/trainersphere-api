import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { WorkoutsPage } from '@/features/workouts/workouts-page';

export const Route = createFileRoute('/_authed/_shell/_trainer/workouts/')({
  validateSearch: z.object({ q: z.string().optional() }),
  head: () => ({ meta: [{ title: 'Workouts · TrainerSphere' }] }),
  component: WorkoutsPage,
});
