import { useCallback, useState } from 'react';

export interface RestState {
  /** Epoch ms; wall-clock based so the countdown survives the tab sleeping. */
  endAt: number;
  total: number;
}

export function useRestTimer() {
  const [state, setState] = useState<RestState | null>(null);
  const start = useCallback((seconds: number) => setState({ endAt: Date.now() + seconds * 1000, total: seconds }), []);
  const add = useCallback(
    (delta: number) => setState((s) => (s ? { endAt: Math.max(Date.now(), s.endAt + delta * 1000), total: Math.max(1, s.total + delta) } : s)),
    [],
  );
  const stop = useCallback(() => setState(null), []);
  return { state, start, add, stop };
}
