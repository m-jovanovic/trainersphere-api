import { setupWorker } from 'msw/browser';
import { todayIso } from '@/lib/dates';
import { db, seed, type DbState } from './db';
import { handlers, mockConfig } from './handlers';

const DB_KEY = 'ts-mock-db-v1';
const COOKIE_KEY = 'ts-mock-refresh';

function load(): void {
  try {
    const raw = localStorage.getItem(DB_KEY);
    const saved = raw ? (JSON.parse(raw) as DbState) : null;
    // Reseed daily so the demo always has workouts scheduled for "today".
    if (saved && saved.seededOn === todayIso()) {
      db.state = saved;
      return;
    }
  } catch {
    // fall through to a fresh seed
  }
  seed();
  localStorage.removeItem(COOKIE_KEY);
}

export async function startMockWorker(): Promise<void> {
  load();
  mockConfig.delayMs = 250;
  mockConfig.refreshCookie = localStorage.getItem(COOKIE_KEY);
  mockConfig.onRefreshCookie = (token) => {
    if (token) localStorage.setItem(COOKIE_KEY, token);
    else localStorage.removeItem(COOKIE_KEY);
  };
  let timer: ReturnType<typeof setTimeout> | undefined;
  mockConfig.onMutate = () => {
    clearTimeout(timer);
    timer = setTimeout(() => localStorage.setItem(DB_KEY, JSON.stringify(db.state)), 100);
  };
  const worker = setupWorker(...handlers);
  await worker.start({ onUnhandledFrame: 'bypass', quiet: true });
  Object.assign(window, {
    __mock: {
      reset() {
        localStorage.removeItem(DB_KEY);
        localStorage.removeItem(COOKIE_KEY);
        location.reload();
      },
      setOffline(offline: boolean) {
        if (offline) worker.stop();
        else void worker.start({ onUnhandledFrame: 'bypass', quiet: true });
      },
    },
  });
}
