import { describe, expect, it } from 'vitest';
import { applyProblem, oneOf } from './form-errors';
import { ApiError } from './http';
import { describePath, formLevelMessage, validationMessages } from './problem-text';

const validation = (errors: Record<string, string[]>) =>
  new ApiError(400, { status: 400, title: 'Bad Request', detail: 'The request is invalid.', code: 'Validation', errors });

describe('applyProblem', () => {
  it('maps matching keys onto fields and reports no form error', () => {
    const seen: [string, string][] = [];
    const r = applyProblem(validation({ email: ['Bad email.'] }), { isField: oneOf(['email']), setFieldError: (p, m) => seen.push([p, m]) });
    expect(seen).toEqual([['email', 'Bad email.']]);
    expect(r).toEqual({ formError: null, fields: ['email'] });
  });

  it('shows a $ body error as the form-level message', () => {
    const r = applyProblem(validation({ $: ['The request body is not valid JSON.'] }), { isField: () => true, setFieldError: () => undefined });
    expect(r.formError).toBe('The request body is not valid JSON.');
    expect(r.fields).toEqual([]);
  });

  it('shows keys the form does not know as form-level messages with their path', () => {
    const seen: [string, string][] = [];
    const r = applyProblem(validation({ firstName: ['Required.'], timeZone: ["'Mars/Olympus Mons' is not a known time zone."] }), {
      isField: oneOf(['firstName', 'lastName']),
      setFieldError: (p, m) => seen.push([p, m]),
    });
    expect(seen).toEqual([['firstName', 'Required.']]);
    expect(r.fields).toEqual(['firstName']);
    expect(r.formError).toBe("Time zone: 'Mars/Olympus Mons' is not a known time zone.");
  });

  it('converts JSON paths and applies mapPath before matching', () => {
    const seen: string[] = [];
    applyProblem(validation({ 'exercises[0].goal.max': ['Too low.'] }), {
      isField: (p) => p === 'exercises.0.goalMax',
      setFieldError: (p) => seen.push(p),
      mapPath: (p) => p.replace(/\.goal\.max$/, '.goalMax'),
    });
    expect(seen).toEqual(['exercises.0.goalMax']);
  });

  it('falls back to the problem detail when there are no validation errors', () => {
    const r = applyProblem(new ApiError(422, { status: 422, detail: 'Archived clients cannot get new assignments.', code: 'Client.Archived' }), {
      isField: () => true,
      setFieldError: () => undefined,
    });
    expect(r.formError).toBe('Archived clients cannot get new assignments.');
  });
});

describe('problem-text', () => {
  it('describes JSON paths in words', () => {
    expect(describePath('exercises[0].restSeconds')).toBe('Exercise 1 rest seconds');
    expect(describePath('timeZone')).toBe('Time zone');
  });

  it('keeps $ messages standalone and labels the rest', () => {
    expect(formLevelMessage('$', 'Bad body.')).toBe('Bad body.');
    expect(validationMessages({ $: ['Bad body.'], weightKg: ['Too heavy.'] })).toEqual(['Bad body.', 'Weight kg: Too heavy.']);
  });
});
