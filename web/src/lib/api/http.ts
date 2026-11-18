import type { AuthResponse, ProblemDetails } from './types';
import { session } from '@/lib/auth/session';

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed (${status})`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  get code(): string | undefined {
    return this.problem.code;
  }

  /**
   * Validation messages keyed by react-hook-form field path: the API sends JSON paths
   * such as `exercises[0].sets`, RHF expects `exercises.0.sets`.
   */
  get fieldErrors(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const [key, messages] of Object.entries(this.problem.errors ?? {})) {
      const first = messages[0];
      if (first) out[key.replace(/\[(\d+)\]/g, '.$1')] = first;
    }
    return out;
  }
}

/** The request never reached the server (offline, DNS, proxy down). */
export class NetworkError extends Error {
  constructor(cause: unknown) {
    super('You appear to be offline.', { cause });
    this.name = 'NetworkError';
  }
}

export function isApiError(err: unknown, status?: number): err is ApiError {
  return err instanceof ApiError && (status === undefined || err.status === status);
}

export function errorMessage(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.status === 429) return 'Too many attempts. Wait a minute and try again.';
    if (err.status >= 500) return 'Something went wrong on our side. Please try again.';
    return err.problem.detail ?? err.message;
  }
  if (err instanceof NetworkError) return err.message;
  return 'Something went wrong. Please try again.';
}

export const API_BASE = '/api';

type QueryValue = string | number | boolean | null | undefined;
export type Query = Record<string, QueryValue | QueryValue[]>;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  query?: Query;
  signal?: AbortSignal;
  /** Send the bearer token and refresh on 401. Defaults to true. */
  auth?: boolean;
}

export function buildUrl(path: string, query?: Query): string {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(query ?? {})) {
    for (const item of Array.isArray(v) ? v : [v]) {
      if (item !== undefined && item !== null && item !== '') qs.append(k, String(item));
    }
  }
  const s = qs.toString();
  return `${API_BASE}${path}${s ? `?${s}` : ''}`;
}

async function send(path: string, opts: RequestOptions, token: string | null): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json, application/problem+json' };
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  try {
    return await fetch(buildUrl(path, opts.query), {
      method: opts.method ?? 'GET',
      headers,
      body: opts.body === undefined ? undefined : JSON.stringify(opts.body),
      credentials: 'include',
      signal: opts.signal ?? null,
    });
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') throw err;
    throw new NetworkError(err);
  }
}

async function parse<T>(res: Response): Promise<T> {
  if (res.status === 204 || res.status === 202) return undefined as T;
  const text = await res.text();
  if (!res.ok) {
    let problem: ProblemDetails = { status: res.status, title: res.statusText };
    if (text) {
      try {
        problem = JSON.parse(text) as ProblemDetails;
      } catch {
        problem = { status: res.status, title: res.statusText, detail: text };
      }
    }
    throw new ApiError(res.status, problem);
  }
  return (text ? JSON.parse(text) : undefined) as T;
}

let refreshInFlight: Promise<boolean> | null = null;

/**
 * Exchanges the httpOnly refresh cookie for a new access token. Concurrent callers share one
 * request: the API rotates the refresh token, and replaying a rotated token revokes the whole
 * token family, so parallel refreshes would log the user out.
 */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const res = await send('/auth/refresh', { method: 'POST' }, null);
      if (!res.ok) {
        session.clear();
        return false;
      }
      session.set(await parse<AuthResponse>(res));
      return true;
    } catch (err) {
      // Offline: keep the current user so cached screens and queued writes keep working.
      if (err instanceof NetworkError) {
        session.markReady();
        return session.get().user !== null;
      }
      session.clear();
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

const EXPIRY_SKEW_MS = 30_000;

export async function api<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  const useAuth = opts.auth ?? true;
  if (useAuth && session.get().user && session.isExpiring(EXPIRY_SKEW_MS)) {
    await refreshSession();
  }
  let res = await send(path, opts, useAuth ? session.get().accessToken : null);
  if (res.status === 401 && useAuth) {
    const ok = await refreshSession();
    if (!ok) return parse<T>(res);
    res = await send(path, opts, session.get().accessToken);
    if (res.status === 401) session.clear();
  }
  return parse<T>(res);
}
