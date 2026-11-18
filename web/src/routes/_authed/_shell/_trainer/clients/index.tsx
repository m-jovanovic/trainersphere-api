import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { ClientsPage } from '@/features/clients/clients-page';

export const Route = createFileRoute('/_authed/_shell/_trainer/clients/')({
  validateSearch: z.object({
    q: z.string().optional(),
    status: z.enum(['active', 'archived']).optional(),
    sort: z.enum(['name', '-lastSessionAt', 'adherence7d']).optional(),
    invite: z.boolean().optional(),
  }),
  head: () => ({ meta: [{ title: 'Clients · TrainerSphere' }] }),
  component: ClientsPage,
});
