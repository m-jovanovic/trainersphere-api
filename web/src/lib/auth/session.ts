import type { AuthResponse, User } from '@/lib/api/types';

export interface SessionState {
  user: User | null;
  accessToken: string | null;
  expiresAt: number | null;
  /** True once the initial silent refresh has resolved. */
  ready: boolean;
}

type Listener = () => void;

const listeners = new Set<Listener>();
let state: SessionState = { user: null, accessToken: null, expiresAt: null, ready: false };

function emit(next: SessionState) {
  state = next;
  for (const l of listeners) l();
}

// The access token lives only in memory; the refresh token is an httpOnly cookie the
// browser sends to /api/auth/refresh, so XSS cannot exfiltrate a long-lived credential.
export const session = {
  get: (): SessionState => state,
  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  set(auth: AuthResponse) {
    emit({ user: auth.user, accessToken: auth.accessToken, expiresAt: Date.now() + auth.expiresIn * 1000, ready: true });
  },
  clear() {
    emit({ user: null, accessToken: null, expiresAt: null, ready: true });
  },
  markReady() {
    if (!state.ready) emit({ ...state, ready: true });
  },
  isExpiring(skewMs: number): boolean {
    return state.expiresAt !== null && state.expiresAt - skewMs < Date.now();
  },
  /** Test helper. */
  reset() {
    emit({ user: null, accessToken: null, expiresAt: null, ready: false });
  },
};
