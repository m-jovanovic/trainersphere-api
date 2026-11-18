import { useInfiniteQuery } from '@tanstack/react-query';
import { format, parseISO } from 'date-fns';
import type { ReactNode } from 'react';
import { Link } from '@tanstack/react-router';
import { ChevronRight, History, Trophy } from 'lucide-react';
import { queries } from '@/lib/api/queries';
import type { SessionSummary } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatDateTime } from '@/lib/dates';
import { formatVolume } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Badge, Card, EmptyState, Skeleton } from '@/components/ui/misc';
import { QueryError } from '@/components/layout/page-states';

/** Paged list of sessions, newest first, grouped by month. */
export function SessionList({ clientId, emptyAction }: { clientId?: string; emptyAction?: ReactNode }) {
  const units = useUnits();
  const q = useInfiniteQuery(queries.sessionsInfinite({ clientId }));
  if (q.error && !q.data) return <Card><QueryError error={q.error} onRetry={() => void q.refetch()} /></Card>;
  if (!q.data) {
    return (
      <div className="flex flex-col gap-2" aria-busy="true" aria-label="Loading sessions">
        {[0, 1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-16" />
        ))}
      </div>
    );
  }
  const items = q.data.pages.flatMap((p) => p.items);
  if (items.length === 0) {
    return (
      <Card>
        <EmptyState icon={<History />} title="No sessions yet" description="Completed workouts show up here." action={emptyAction} />
      </Card>
    );
  }
  const groups = new Map<string, SessionSummary[]>();
  for (const s of items) {
    const key = format(parseISO(s.startedAt), 'MMMM yyyy');
    groups.set(key, [...(groups.get(key) ?? []), s]);
  }
  return (
    <div className="flex flex-col gap-5">
      {[...groups.entries()].map(([month, sessions]) => (
        <section key={month} aria-label={month}>
          <h3 className="mb-2 text-xs font-semibold tracking-wide text-muted uppercase">{month}</h3>
          <Card>
            <ul className="divide-y divide-border">
              {sessions.map((s) => (
                <li key={s.id}>
                  <Link to="/sessions/$sessionId" params={{ sessionId: s.id }} className="flex items-center gap-3 px-4 py-3 hover:bg-surface-2 sm:px-5">
                    <span className="flex w-11 shrink-0 flex-col items-center rounded-lg bg-surface-2 py-1 text-center leading-tight">
                      <span className="text-[10px] font-medium text-muted uppercase">{format(parseISO(s.startedAt), 'EEE')}</span>
                      <span className="tabular text-lg font-semibold">{format(parseISO(s.startedAt), 'd')}</span>
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-2">
                        <span className="truncate font-medium">{s.workoutName}</span>
                        {s.status === 'in-progress' && <Badge tone="primary">In progress</Badge>}
                      </span>
                      <span className="block truncate text-sm text-muted">
                        {formatDateTime(s.startedAt, 'HH:mm')} · {s.completedSets} sets · {formatVolume(s.totalVolumeKg, units)}
                      </span>
                    </span>
                    {s.personalRecordCount > 0 && (
                      <Badge tone="pr" aria-label={`${s.personalRecordCount} personal records`}>
                        <Trophy aria-hidden />
                        {s.personalRecordCount} PR{s.personalRecordCount === 1 ? '' : 's'}
                      </Badge>
                    )}
                    <ChevronRight className="size-4 text-muted" aria-hidden />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        </section>
      ))}
      {q.hasNextPage && (
        <Button variant="secondary" className="self-center" loading={q.isFetchingNextPage} onClick={() => void q.fetchNextPage()}>
          Load older sessions
        </Button>
      )}
    </div>
  );
}
