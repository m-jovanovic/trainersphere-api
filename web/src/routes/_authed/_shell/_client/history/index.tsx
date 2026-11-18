import { createFileRoute } from '@tanstack/react-router';
import { HistoryPage } from '@/features/history/history-page';

export const Route = createFileRoute('/_authed/_shell/_client/history/')({
  head: () => ({ meta: [{ title: 'History · TrainerSphere' }] }),
  component: HistoryPage,
});
