import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { ExercisesPage } from '@/features/exercises/exercises-page';

export const Route = createFileRoute('/_authed/_shell/exercises')({
  validateSearch: z.object({
    q: z.string().optional(),
    muscle: z.array(z.string()).optional(),
    equipment: z.array(z.string()).optional(),
    pattern: z.array(z.string()).optional(),
    custom: z.boolean().optional(),
    id: z.string().optional(),
  }),
  head: () => ({ meta: [{ title: 'Exercises · TrainerSphere' }] }),
  component: ExercisesPage,
});
