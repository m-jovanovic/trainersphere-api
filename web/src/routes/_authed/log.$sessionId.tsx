import { createFileRoute, redirect } from '@tanstack/react-router';
import { LiveSessionPage } from '@/features/session/live-session-page';

/** Full-screen live logging, outside the app shell so the phone screen is all workout. */
export const Route = createFileRoute('/_authed/log/$sessionId')({
  beforeLoad: ({ context, params }) => {
    // Only the client logs; trainers get the read-only view of the same session.
    if (context.user.role !== 'client') throw redirect({ to: '/sessions/$sessionId', params });
  },
  head: () => ({ meta: [{ title: 'Workout · TrainerSphere' }] }),
  component: LiveSessionPage,
});
