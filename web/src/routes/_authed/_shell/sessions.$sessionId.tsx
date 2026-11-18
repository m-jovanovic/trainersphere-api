import { createFileRoute } from '@tanstack/react-router';
import { queries } from '@/lib/api/queries';
import { SessionDetailPage } from '@/features/history/session-detail-page';

/** Read-only view of a logged session, for trainers and clients. */
export const Route = createFileRoute('/_authed/_shell/sessions/$sessionId')({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.session(params.sessionId)),
  head: () => ({ meta: [{ title: 'Session · TrainerSphere' }] }),
  component: SessionDetailPage,
});
