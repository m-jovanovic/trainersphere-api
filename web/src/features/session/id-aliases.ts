import { useSyncExternalStore } from 'react';

/**
 * Client-made ids that the server refused because another session already owns them, mapped to
 * the fresh id that replaced them. Queued writes and open pages still hold the original id, so
 * they resolve it here. Persisted because queued writes survive a reload.
 */
const STORAGE_KEY = 'ts-id-aliases';
const listeners = new Set<() => void>();

function load(): Record<string, string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Record<string, string>) : {};
  } catch {
    return {};
  }
}

let aliases = load();

export function resolveId(id: string): string {
  return aliases[id] ?? id;
}

/** The id a write was first queued under, so every write for one session shares a scope. */
export function originalId(id: string): string {
  return Object.keys(aliases).find((k) => aliases[k] === id) ?? id;
}

export function aliasId(from: string, to: string): void {
  aliases = { ...aliases, [from]: to };
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(aliases));
  } catch {
    // Storage full or blocked: the alias still holds for this page load.
  }
  listeners.forEach((l) => l());
}

export function clearIdAliases(): void {
  aliases = {};
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore: nothing persisted.
  }
  listeners.forEach((l) => l());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useResolvedId(id: string): string {
  return useSyncExternalStore(subscribe, () => resolveId(id), () => id);
}
