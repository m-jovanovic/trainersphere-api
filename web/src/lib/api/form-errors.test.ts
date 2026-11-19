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
    const r = applyProblem(validation({ firstName: ['Must not be empty.'], timeZone: ['Must be a known IANA time zone, for example Europe/Belgrade.'] }), {
      isField: oneOf(['firstName', 'lastName']),
      setFieldError: (p, m) => seen.push([p, m]),
    });
    expect(seen).toEqual([['firstName', 'Must not be empty.']]);
    expect(r.fields).toEqual(['firstName']);
    expect(r.formError).toBe('Time zone: Must be a known IANA time zone, for example Europe/Belgrade.');
  });

  it('names the field for every field-free message (contract v1.8) that lands in the banner', () => {
    const r = applyProblem(
      validation({ to: ["Must not be before 'from'."], 'exercises[1].goal.max': ["Must be between 'min' and 100."], units: ['Must be one of: kg, lb.'], $: ['A request body is required.'] }),
      { isField: () => false, setFieldError: () => undefined },
    );
    expect(r.formError).toBe("To: Must not be before 'from'. Exercise 2 goal max: Must be between 'min' and 100. Units: Must be one of: kg, lb. A request body is required.");
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
