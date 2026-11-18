import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

export const Route = createFileRoute('/_authed/_shell/_client')({
  beforeLoad: ({ context }) => {
    if (context.user.role !== 'client') throw redirect({ to: '/' });
  },
  component: Outlet,
});
