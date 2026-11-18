import { CloudOff, RefreshCw } from 'lucide-react';
import { useOnline, usePendingWrites } from '@/lib/online';
import { cn } from '@/lib/utils';

/** Shows offline state and queued writes; hidden when everything is synced. */
export function SyncStatus({ compact }: { compact?: boolean }) {
  const online = useOnline();
  const { paused } = usePendingWrites();
  if (online && paused === 0) return null;
  const text = !online
    ? paused > 0
      ? `Offline · ${paused} change${paused === 1 ? '' : 's'} pending`
      : 'Offline'
    : `Syncing ${paused} change${paused === 1 ? '' : 's'}`;
  return (
    <div
      role="status"
      className={cn(
        'flex items-center gap-1.5 rounded-full border border-border bg-surface-2 text-xs font-medium',
        compact ? 'px-2 py-1' : 'px-3 py-1.5',
        !online && 'text-warning',
      )}
    >
      {online ? <RefreshCw className="size-3.5 animate-spin" aria-hidden /> : <CloudOff className="size-3.5" aria-hidden />}
      <span className={cn(compact && 'max-sm:sr-only')}>{text}</span>
    </div>
  );
}
