import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';
import { session } from '@/lib/auth/session';

export const Route = createFileRoute('/_authed')({
  beforeLoad: ({ location }) => {
    const { user } = session.get();
    if (!user) throw redirect({ to: '/login', search: { redirect: location.href } });
    return { user };
  },
  component: Outlet,
});
