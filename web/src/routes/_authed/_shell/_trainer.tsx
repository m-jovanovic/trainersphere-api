import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

export const Route = createFileRoute('/_authed/_shell/_trainer')({
  beforeLoad: ({ context }) => {
    if (context.user.role !== 'trainer') throw redirect({ to: '/' });
  },
  component: Outlet,
});
