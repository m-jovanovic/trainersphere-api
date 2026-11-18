import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryHistory } from '@tanstack/react-router';
import { App } from '@/app';
import { createAppRouter } from '@/router';
import { createQueryClient } from '@/lib/query-client';
import { registerMutationDefaults } from '@/features/session/offline';
import { authApi } from '@/lib/api/endpoints';
import { session } from '@/lib/auth/session';
import { DEMO_PASSWORD, TRAINER_EMAIL } from '@/mocks/db';

export const CLIENT_EMAIL = 'alex@trainersphere.dev';

/** Signs in against the mock API, as the browser would after a login. */
export async function signIn(email: string = TRAINER_EMAIL, password: string = DEMO_PASSWORD) {
  const res = await authApi.login({ email, password });
  session.set(res);
  return res.user;
}

/** Renders the whole app at `path` with a fresh query cache and router. */
export function renderApp(path = '/') {
  const queryClient = createQueryClient();
  queryClient.setDefaultOptions({
    ...queryClient.getDefaultOptions(),
    queries: { ...queryClient.getDefaultOptions().queries, retry: false, gcTime: Infinity },
  });
  registerMutationDefaults(queryClient);
  const history = createMemoryHistory({ initialEntries: [path] });
  const router = createAppRouter(queryClient, history);
  const user = userEvent.setup();
  const utils = render(<App queryClient={queryClient} router={router} persist={false} />);
  return { ...utils, user, router, queryClient, history };
}

export async function findHeading(name: string | RegExp) {
  return screen.findByRole('heading', { name, level: 1 }, { timeout: 5000 });
}
