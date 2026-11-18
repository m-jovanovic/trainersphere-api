import { createFileRoute } from '@tanstack/react-router';
import { useUser } from '@/lib/auth/hooks';
import { queries } from '@/lib/api/queries';
import { TrainerDashboard } from '@/features/dashboard/trainer-dashboard';
import { ClientToday } from '@/features/today/client-today';

export const Route = createFileRoute('/_authed/_shell/')({
  loader: ({ context }) =>
    context.user.role === 'trainer'
      ? context.queryClient.prefetchQuery(queries.trainerDashboard())
      : context.queryClient.prefetchQuery(queries.clientDashboard()),
  component: Home,
});

function Home() {
  const user = useUser();
  return user.role === 'trainer' ? <TrainerDashboard /> : <ClientToday />;
}
