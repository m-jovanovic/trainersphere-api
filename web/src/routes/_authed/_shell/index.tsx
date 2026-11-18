import { createFileRoute } from '@tanstack/react-router';
import { useUser } from '@/lib/auth/hooks';
import { queries } from '@/lib/api/queries';
import { TrainerDashboard } from '@/features/dashboard/trainer-dashboard';
import { ClientToday } from '@/features/today/client-today';

export const Route = createFileRoute('/_authed/_shell/')({
  loader: async ({ context }) => {
    const qc = context.queryClient;
    // Errors surface through the page's own query.
    const pending: Promise<unknown> = context.user.role === 'trainer' ? qc.query(queries.trainerDashboard()) : qc.query(queries.clientDashboard());
    await pending.catch(() => undefined);
  },
  component: Home,
});

function Home() {
  const user = useUser();
  return user.role === 'trainer' ? <TrainerDashboard /> : <ClientToday />;
}
