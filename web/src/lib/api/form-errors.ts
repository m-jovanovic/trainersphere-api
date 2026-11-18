import { ApiError, errorMessage } from './http';
import { formLevelMessage, toFieldPath } from './problem-text';

export interface AppliedProblem {
  /** Message for the form banner, or null when every error landed on a field. */
  formError: string | null;
  /** Field paths (react-hook-form style) that received an error. */
  fields: string[];
}

/**
 * Spreads an API error over a form. Validation keys that name a field go to that field;
 * everything else, including `$` and keys the form does not know, becomes the form-level
 * message so it is never dropped. Other errors fall back to the problem's detail.
 */
export function applyProblem(
  err: unknown,
  {
    isField,
    setFieldError,
    mapPath = (p) => p,
  }: { isField: (path: string) => boolean; setFieldError: (path: string, message: string) => void; mapPath?: (path: string) => string },
): AppliedProblem {
  const errors = err instanceof ApiError ? err.problem.errors : undefined;
  if (!errors || Object.keys(errors).length === 0) return { formError: errorMessage(err), fields: [] };
  const fields: string[] = [];
  const unmatched: string[] = [];
  for (const [key, messages] of Object.entries(errors)) {
    const message = messages[0];
    if (!message) continue;
    const path = key === '$' ? null : mapPath(toFieldPath(key));
    if (path !== null && isField(path)) {
      setFieldError(path, message);
      fields.push(path);
    } else {
      unmatched.push(formLevelMessage(key, message));
    }
  }
  if (unmatched.length) return { formError: [...new Set(unmatched)].join(' '), fields };
  return { formError: fields.length ? null : errorMessage(err), fields };
}

/** `isField` for flat forms: the path is one of the listed field names. */
export const oneOf =
  (names: readonly string[]) =>
  (path: string): boolean =>
    names.includes(path);
