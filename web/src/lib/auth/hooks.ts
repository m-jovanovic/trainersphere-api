import { useSyncExternalStore } from 'react';
import type { User } from '@/lib/api/types';
import { refreshSession } from '@/lib/api/http';
import { session, type SessionState } from './session';

export function useSession(): SessionState {
  return useSyncExternalStore(session.subscribe, session.get, session.get);
}

let lastUser: User | null = null;

/**
 * The signed-in user. Only use below the authenticated route. After logout the previous user is
 * returned for the one render that happens before the route guard redirects to /login.
 */
export function useUser(): User {
  const { user } = useSession();
  if (user) lastUser = user;
  if (!lastUser) throw new Error('useUser called outside an authenticated route');
  return user ?? lastUser;
}

let bootstrap: Promise<void> | null = null;

/** Restores the session from the refresh cookie once per page load. */
export function bootstrapSession(): Promise<void> {
  bootstrap ??= (async () => {
    if (session.get().ready) return;
    await refreshSession();
    session.markReady();
  })();
  return bootstrap;
}

export function resetBootstrap() {
  bootstrap = null;
}
