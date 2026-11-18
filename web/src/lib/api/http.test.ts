import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/node';
import { mockConfig } from '@/mocks/handlers';
import { api, ApiError, NetworkError, buildUrl, errorMessage, refreshSession } from '@/lib/api/http';
import { session } from '@/lib/auth/session';
import { CLIENT_EMAIL, signIn } from '@/test/utils';

describe('api client', () => {
  it('builds query strings, repeating array params and skipping empties', () => {
    expect(buildUrl('/exercises', { muscle: ['quads', 'glutes'], search: '', page: 2, custom: undefined })).toBe(
      '/api/exercises?muscle=quads&muscle=glutes&page=2',
    );
  });

  it('parses ProblemDetails into ApiError with field paths for react-hook-form', async () => {
    server.use(
      http.post('/api/x', () =>
        HttpResponse.json(
          { title: 'Bad', status: 400, detail: 'The request is invalid.', code: 'Validation', errors: { 'exercises[0].sets': ['Too many.'], email: ['Bad email.'] } },
          { status: 400 },
        ),
      ),
    );
    const err = await api('/x', { method: 'POST', body: {}, auth: false }).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    const apiErr = err as ApiError;
    expect(apiErr.code).toBe('Validation');
    expect(apiErr.fieldErrors).toEqual({ 'exercises.0.sets': 'Too many.', email: 'Bad email.' });
  });

  it('shows the domain detail for 422 and a generic message for 500', () => {
    expect(errorMessage(new ApiError(422, { status: 422, detail: 'Archived clients cannot get new assignments.', code: 'Client.Archived' }))).toBe(
      'Archived clients cannot get new assignments.',
    );
    expect(errorMessage(new ApiError(500, { status: 500, detail: 'NullReferenceException at ...' }))).toBe('Something went wrong on our side. Please try again.');
    expect(errorMessage(new ApiError(429, { status: 429 }))).toMatch(/Too many attempts/);
  });

  it('rejects a body that is not valid JSON with a $ error (contract v1.5)', async () => {
    const res = await fetch('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{not json' });
    expect(res.status).toBe(400);
    const problem = (await res.json()) as { code: string; type: string; detail: string; errors: Record<string, string[]> };
    expect(problem.code).toBe('Validation');
    expect(problem.type).toContain('rfc9110#section-15.5.1');
    expect(problem.errors.$).toEqual(['The request body is not valid JSON.']);
  });

  it('treats PUT as a full replace and enums as exact kebab-case (contract v1.5)', async () => {
    await signIn();
    const missing = (await api('/auth/me', { method: 'PUT', body: { firstName: 'Maya', lastName: 'Novak', timeZone: 'UTC' } }).catch((e: unknown) => e)) as ApiError;
    expect(missing).toBeInstanceOf(ApiError);
    expect(missing.problem.errors?.units).toEqual(['Required.']);

    const badEnum = (await api('/auth/me', { method: 'PUT', body: { firstName: 'Maya', lastName: 'Novak', units: 'KG', timeZone: 'UTC' } }).catch((e: unknown) => e)) as ApiError;
    expect(badEnum.problem.errors?.units).toEqual(['Must be one of: kg, lb.']);
    expect(errorMessage(badEnum)).toBe('Units: Must be one of: kg, lb.');
  });

  it('rejects PUT /sessions/{id} without a workoutName (full replace)', async () => {
    await signIn(CLIENT_EMAIL);
    const started = await api<{ id: string }>('/sessions', { method: 'POST', body: { workoutName: 'Ad hoc' } });
    const missing = (await api('/sessions/' + started.id, { method: 'PUT', body: { notes: null } }).catch((e: unknown) => e)) as ApiError;
    expect(missing.status).toBe(400);
    expect(missing.problem.errors?.workoutName).toEqual(['Required.']);
    const empty = (await api('/sessions/' + started.id, { method: 'PUT', body: { workoutName: '  ', notes: null } }).catch((e: unknown) => e)) as ApiError;
    expect(empty.problem.errors?.workoutName).toEqual(['Required.']);
  });

  it('rejects unknown enum values in query parameters', async () => {
    await signIn();
    const err = (await api('/clients', { query: { status: 'Active' } }).catch((e: unknown) => e)) as ApiError;
    expect(err.status).toBe(400);
    expect(err.problem.errors?.status).toEqual(['Must be one of: active, archived.']);
  });

  it('wraps fetch failures as NetworkError', async () => {
    server.use(http.get('/api/down', () => HttpResponse.error()));
    await expect(api('/down', { auth: false })).rejects.toBeInstanceOf(NetworkError);
  });

  it('refreshes once on 401 and retries the request with the new token', async () => {
    await signIn();
    const firstToken = session.get().accessToken;
    let calls = 0;
    server.use(
      http.get('/api/probe', ({ request }) => {
        calls += 1;
        return request.headers.get('Authorization') === `Bearer ${firstToken}`
          ? HttpResponse.json({ status: 401, code: 'Auth.Unauthorized' }, { status: 401 })
          : HttpResponse.json({ ok: true });
      }),
    );
    await expect(api('/probe')).resolves.toEqual({ ok: true });
    expect(calls).toBe(2);
    expect(session.get().accessToken).not.toBe(firstToken);
  });

  it('shares one refresh between concurrent 401s, so the rotated token is not replayed', async () => {
    await signIn();
    let refreshes = 0;
    server.events.on('request:start', ({ request }) => {
      if (new URL(request.url).pathname === '/api/auth/refresh') refreshes += 1;
    });
    session.set({ ...{ accessToken: 'mock.expired.0', tokenType: 'Bearer', expiresIn: 900 }, user: session.get().user! });
    const results = await Promise.all([api('/auth/me'), api('/auth/me'), api('/auth/me')]);
    expect(results).toHaveLength(3);
    expect(refreshes).toBe(1);
    server.events.removeAllListeners();
  });

  it('refreshes proactively when the access token is about to expire', async () => {
    mockConfig.accessTokenTtlSeconds = 10;
    await signIn();
    const before = session.get().accessToken;
    mockConfig.accessTokenTtlSeconds = 900;
    await api('/auth/me');
    expect(session.get().accessToken).not.toBe(before);
  });

  it('clears the session when the refresh token is rejected', async () => {
    await signIn();
    mockConfig.refreshCookie = 'revoked';
    await expect(refreshSession()).resolves.toBe(false);
    expect(session.get().user).toBeNull();
  });

  it('keeps the session when refresh fails only because the network is down', async () => {
    await signIn();
    server.use(http.post('/api/auth/refresh', () => HttpResponse.error()));
    await expect(refreshSession()).resolves.toBe(true);
    expect(session.get().user).not.toBeNull();
  });
});
