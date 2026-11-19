import { useEffect } from 'react';

/** Keeps the phone screen on while logging; the lock is dropped when the tab hides, so re-request. */
export function useWakeLock(enabled = true) {
  useEffect(() => {
    if (!enabled || !('wakeLock' in navigator)) return;
    let lock: WakeLockSentinel | null = null;
    let cancelled = false;
    const request = async () => {
      try {
        const next = await navigator.wakeLock.request('screen');
        if (cancelled) await next.release();
        else lock = next;
      } catch {
        // Denied (battery saver, unsupported context): logging works without it.
      }
    };
    const onVisible = () => {
      if (document.visibilityState === 'visible') void request();
    };
    void request();
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      cancelled = true;
      document.removeEventListener('visibilitychange', onVisible);
      void lock?.release();
    };
  }, [enabled]);
}
