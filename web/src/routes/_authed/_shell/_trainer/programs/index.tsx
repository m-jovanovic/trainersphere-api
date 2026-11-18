import { createFileRoute } from '@tanstack/react-router';
import { ProgramsPage } from '@/features/programs/programs-page';

export const Route = createFileRoute('/_authed/_shell/_trainer/programs/')({
  head: () => ({ meta: [{ title: 'Programs · TrainerSphere' }] }),
  component: ProgramsPage,
});
