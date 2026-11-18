import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';
import { session } from '@/lib/auth/session';
import { AuthLayout } from '@/features/auth/auth-layout';

export const Route = createFileRoute('/_public')({
  beforeLoad: () => {
    if (session.get().user) throw redirect({ to: '/' });
  },
  component: () => (
    <AuthLayout>
      <Outlet />
    </AuthLayout>
  ),
});
