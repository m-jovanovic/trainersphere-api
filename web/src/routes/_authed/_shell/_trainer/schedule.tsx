import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { SchedulePage } from '@/features/schedule/schedule-page';

export const Route = createFileRoute('/_authed/_shell/_trainer/schedule')({
  validateSearch: z.object({
    /** Any date inside the visible month, YYYY-MM-DD. */
    month: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional(),
    client: z.string().optional(),
  }),
  head: () => ({ meta: [{ title: 'Schedule · TrainerSphere' }] }),
  component: SchedulePage,
});
