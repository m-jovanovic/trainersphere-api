import { useCallback, useSyncExternalStore } from 'react';

/** Device-local preferences (not synced to the account). */
export const prefKeys = {
  restSound: 'ts-rest-sound',
  restVibrate: 'ts-rest-vibrate',
} as const;

const listeners = new Set<() => void>();

function readBool(key: string, fallback: boolean): boolean {
  try {
    const v = localStorage.getItem(key);
    return v === null ? fallback : v === 'true';
  } catch {
    return fallback;
  }
}

export function setBoolPref(key: string, value: boolean) {
  try {
    localStorage.setItem(key, String(value));
  } catch {
    // Storage unavailable (private mode): the choice lasts for this page only.
  }
  for (const l of listeners) l();
}

export function useBoolPref(key: string, fallback: boolean): [boolean, (v: boolean) => void] {
  const value = useSyncExternalStore(
    (l) => {
      listeners.add(l);
      return () => listeners.delete(l);
    },
    () => readBool(key, fallback),
    () => fallback,
  );
  const set = useCallback((v: boolean) => setBoolPref(key, v), [key]);
  return [value, set];
}
