import { createRouter, type RouterHistory } from '@tanstack/react-router';
import type { QueryClient } from '@tanstack/react-query';
import { routeTree } from './routeTree.gen';
import { PageError, PageSpinner, NotFound } from '@/components/layout/page-states';

export function createAppRouter(queryClient: QueryClient, history?: RouterHistory) {
  return createRouter({
    routeTree,
    context: { queryClient },
    ...(history ? { history } : {}),
    defaultPreload: 'intent',
    // Loaders only prefetch into TanStack Query, which owns freshness.
    defaultPreloadStaleTime: 0,
    defaultPendingMs: 300,
    defaultPendingComponent: PageSpinner,
    defaultErrorComponent: PageError,
    defaultNotFoundComponent: NotFound,
    scrollRestoration: true,
  });
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
