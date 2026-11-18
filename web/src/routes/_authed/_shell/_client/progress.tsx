import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { ProgressPage } from '@/features/progress/progress-page';

export const Route = createFileRoute('/_authed/_shell/_client/progress')({
  validateSearch: z.object({ exercise: z.string().optional() }),
  head: () => ({ meta: [{ title: 'Progress · TrainerSphere' }] }),
  component: ProgressPage,
});
