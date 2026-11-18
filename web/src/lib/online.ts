import { onlineManager, useIsMutating, useMutationState } from '@tanstack/react-query';
import { useSyncExternalStore } from 'react';

export function useOnline(): boolean {
  return useSyncExternalStore(
    (cb) => onlineManager.subscribe(cb),
    () => onlineManager.isOnline(),
    () => true,
  );
}

/** Number of writes waiting for the network (paused) plus ones in flight. */
export function usePendingWrites(): { paused: number; inFlight: number } {
  const paused = useMutationState({ filters: { predicate: (m) => m.state.isPaused } }).length;
  const inFlight = useIsMutating();
  return { paused, inFlight };
}
