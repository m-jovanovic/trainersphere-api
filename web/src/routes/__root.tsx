import { Outlet, createRootRouteWithContext } from '@tanstack/react-router';
import type { QueryClient } from '@tanstack/react-query';
import { bootstrapSession } from '@/lib/auth/hooks';

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  beforeLoad: () => bootstrapSession(),
  component: Outlet,
});
