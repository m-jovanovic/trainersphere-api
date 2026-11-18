import { createFileRoute } from '@tanstack/react-router';
import { LiveSessionPage } from '@/features/session/live-session-page';

/** Full-screen live logging, outside the app shell so the phone screen is all workout. */
export const Route = createFileRoute('/_authed/log/$sessionId')({
  head: () => ({ meta: [{ title: 'Workout · TrainerSphere' }] }),
  component: LiveSessionPage,
});
