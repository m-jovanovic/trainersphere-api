import { CalendarClock, CircleCheck, CircleSlash, CircleX } from 'lucide-react';
import type { AssignmentStatus } from '@/lib/api/types';
import { formatPercent } from '@/lib/domain';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/misc';

const statusMeta = {
  scheduled: { tone: 'neutral', label: 'Scheduled', icon: CalendarClock },
  completed: { tone: 'primary', label: 'Completed', icon: CircleCheck },
  skipped: { tone: 'warning', label: 'Skipped', icon: CircleSlash },
  missed: { tone: 'danger', label: 'Missed', icon: CircleX },
} as const;

export function AssignmentStatusBadge({ status, inProgress }: { status: AssignmentStatus; inProgress?: boolean }) {
  if (inProgress && status === 'scheduled') {
    return (
      <Badge tone="primary">
        <span className="size-1.5 animate-pulse rounded-full bg-current" aria-hidden />
        In progress
      </Badge>
    );
  }
  const m = statusMeta[status];
  const Icon = m.icon;
  return (
    <Badge tone={m.tone}>
      <Icon aria-hidden />
      {m.label}
    </Badge>
  );
}

function adherenceTone(rate: number) {
  if (rate >= 0.8) return 'bg-primary';
  if (rate >= 0.5) return 'bg-warning';
  return 'bg-danger';
}

/** Horizontal meter for a 0..1 adherence rate; `null` means nothing was due. */
export function AdherenceMeter({ rate, label, className }: { rate: number | null; label: string; className?: string }) {
  return (
    <div className={cn('flex items-center gap-2', className)}>
      <div
        role="meter"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={rate === null ? undefined : Math.round(rate * 100)}
        aria-valuetext={rate === null ? 'Nothing due' : formatPercent(rate)}
        className="h-2 min-w-12 flex-1 overflow-hidden rounded-full bg-surface-3"
      >
        {rate !== null && <div className={cn('h-full rounded-full', adherenceTone(rate))} style={{ width: `${Math.max(4, rate * 100)}%` }} />}
      </div>
      <span className="tabular w-10 text-right text-sm font-medium">{formatPercent(rate)}</span>
    </div>
  );
}
