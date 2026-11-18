import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { queries } from '@/lib/api/queries';
import { ClientDetailPage } from '@/features/clients/client-detail-page';

export const Route = createFileRoute('/_authed/_shell/_trainer/clients/$clientId')({
  validateSearch: z.object({
    tab: z.enum(['overview', 'schedule', 'history', 'progress', 'body']).optional(),
    exercise: z.string().optional(),
  }),
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.client(params.clientId)),
  head: () => ({ meta: [{ title: 'Client · TrainerSphere' }] }),
  component: ClientDetailPage,
});
