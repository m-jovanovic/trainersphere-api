import { useQuery } from '@tanstack/react-query';
import { getRouteApi } from '@tanstack/react-router';
import { addMonths, eachDayOfInterval, endOfISOWeek, endOfMonth, format, isSameMonth, parseISO, startOfISOWeek, startOfMonth } from 'date-fns';
import { CalendarPlus, ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { useState } from 'react';
import { queries } from '@/lib/api/queries';
import type { Assignment, AssignmentStatus } from '@/lib/api/types';
import { relativeDay, toIsoDate, todayIso } from '@/lib/dates';
import { fullName, initials } from '@/lib/domain';
import { useMediaQuery } from '@/lib/use-media-query';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { NativeSelect } from '@/components/ui/input';
import { Card, EmptyState, PageHeader, Skeleton } from '@/components/ui/misc';
import { AssignmentStatusBadge } from '@/components/status';
import { QueryError } from '@/components/layout/page-states';
import { AssignmentDialog, ScheduleWorkoutDialog } from './assignment-dialog';

const route = getRouteApi('/_authed/_shell/_trainer/schedule');
const statusDotClass: Record<AssignmentStatus, string> = {
  scheduled: 'bg-muted',
  completed: 'bg-primary',
  skipped: 'bg-muted',
  missed: 'bg-danger',
};

const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

export function SchedulePage() {
  const search = route.useSearch();
  const navigate = route.useNavigate();
  const today = todayIso();
  const anchor = parseISO(search.month ?? today);
  const monthStart = startOfMonth(anchor);
  const gridStart = startOfISOWeek(monthStart);
  const gridEnd = endOfISOWeek(endOfMonth(monthStart));
  const from = toIsoDate(gridStart);
  const to = toIsoDate(gridEnd);
  const clients = useQuery(queries.clients({ status: 'active', sort: 'name' }));
  const assignments = useQuery(queries.assignments({ from, to, clientId: search.client }));
  const wide = useMediaQuery('(min-width: 768px)');
  const [open, setOpen] = useState<Assignment | null>(null);
  const [scheduleDate, setScheduleDate] = useState<string | null>(null);

  const setMonth = (d: Date | null) => void navigate({ search: (s) => ({ ...s, month: d ? toIsoDate(d) : undefined }), replace: true });
  const byDate = new Map<string, Assignment[]>();
  for (const a of assignments.data ?? []) byDate.set(a.date, [...(byDate.get(a.date) ?? []), a]);
  const days = eachDayOfInterval({ start: gridStart, end: gridEnd });

  return (
    <>
      <PageHeader
        title="Schedule"
        actions={
          <Button onClick={() => setScheduleDate(today)}>
            <CalendarPlus aria-hidden />
            Schedule workout
          </Button>
        }
      />
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-1">
          <Button variant="secondary" size="icon" aria-label="Previous month" onClick={() => setMonth(addMonths(monthStart, -1))}>
            <ChevronLeft />
          </Button>
          <Button variant="secondary" size="icon" aria-label="Next month" onClick={() => setMonth(addMonths(monthStart, 1))}>
            <ChevronRight />
          </Button>
        </div>
        <h2 className="min-w-40 text-lg font-semibold" aria-live="polite">
          {format(monthStart, 'MMMM yyyy')}
        </h2>
        {!isSameMonth(monthStart, parseISO(today)) && (
          <Button variant="ghost" size="sm" onClick={() => setMonth(null)}>
            Today
          </Button>
        )}
        <NativeSelect aria-label="Filter by client" value={search.client ?? ''} onChange={(e) => void navigate({ search: (s) => ({ ...s, client: e.target.value || undefined }), replace: true })} className="ml-auto w-auto min-w-44">
          <option value="">All clients</option>
          {clients.data?.items.map((c) => (
            <option key={c.id} value={c.id}>
              {fullName(c)}
            </option>
          ))}
        </NativeSelect>
      </div>

      {assignments.error && !assignments.data ? (
        <QueryError error={assignments.error} onRetry={() => void assignments.refetch()} />
      ) : !assignments.data ? (
        <Skeleton className="h-[32rem]" />
      ) : wide ? (
        <Card className="overflow-hidden">
          <div role="grid" aria-label={`${format(monthStart, 'MMMM yyyy')} schedule`} className={cn(assignments.isPlaceholderData && 'opacity-60')}>
            <div role="row" className="grid grid-cols-7 border-b border-border bg-surface-2">
              {WEEKDAYS.map((d) => (
                <div role="columnheader" key={d} className="px-2 py-2 text-xs font-medium text-muted">
                  {d}
                </div>
              ))}
            </div>
            {Array.from({ length: days.length / 7 }, (_, w) => (
              <div role="row" key={w} className="grid grid-cols-7 border-b border-border last:border-b-0">
                {days.slice(w * 7, w * 7 + 7).map((day) => {
                  const iso = toIsoDate(day);
                  const list = byDate.get(iso) ?? [];
                  const inMonth = isSameMonth(day, monthStart);
                  const isToday = iso === today;
                  return (
                    <div role="gridcell" key={iso} aria-label={format(day, 'EEEE d MMMM')} className={cn('group flex min-h-28 flex-col gap-1 border-r border-border p-1.5 last:border-r-0', !inMonth && 'bg-surface-2/50')}>
                      <div className="flex items-center justify-between">
                        <span className={cn('tabular flex size-6 items-center justify-center rounded-full text-xs', isToday ? 'bg-primary font-semibold text-primary-fg' : inMonth ? 'text-fg' : 'text-muted')}>{format(day, 'd')}</span>
                        <button
                          type="button"
                          aria-label={`Schedule on ${format(day, 'EEEE d MMMM')}`}
                          onClick={() => setScheduleDate(iso)}
                          className="flex size-6 items-center justify-center rounded text-muted opacity-0 group-hover:opacity-100 hover:bg-surface-2 hover:text-fg focus-visible:opacity-100"
                        >
                          <Plus className="size-3.5" aria-hidden />
                        </button>
                      </div>
                      {list.map((a) => (
                        <button
                          type="button"
                          key={a.id}
                          onClick={() => setOpen(a)}
                          className="flex w-full items-center gap-1.5 rounded bg-surface-2 px-1.5 py-1 text-left text-xs hover:bg-surface-3"
                          title={`${fullName(a.client)} · ${a.workout.name} · ${a.status}`}
                        >
                          <span className={cn('size-2 shrink-0 rounded-full', statusDotClass[a.status])} aria-hidden />
                          {!search.client && <span className="shrink-0 font-semibold">{initials(a.client)}</span>}
                          <span className="truncate">{a.workout.name}</span>
                          <span className="sr-only">
                            , {fullName(a.client)}, {a.status}
                          </span>
                        </button>
                      ))}
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        </Card>
      ) : (
        <Agenda byDate={byDate} monthStart={monthStart} onOpen={setOpen} />
      )}
      <div className="mt-3 flex flex-wrap gap-3 text-xs text-muted" aria-hidden>
        {(['scheduled', 'completed', 'skipped', 'missed'] as const).map((s) => (
          <span key={s} className="flex items-center gap-1.5 capitalize">
            <span className={cn('size-2 rounded-full', statusDotClass[s])} />
            {s}
          </span>
        ))}
      </div>

      {open && <AssignmentDialog assignment={open} onClose={() => setOpen(null)} />}
      <ScheduleWorkoutDialog open={scheduleDate !== null} onOpenChange={(o) => !o && setScheduleDate(null)} date={scheduleDate ?? undefined} clientId={search.client} />
    </>
  );
}

function Agenda({ byDate, monthStart, onOpen }: { byDate: Map<string, Assignment[]>; monthStart: Date; onOpen: (a: Assignment) => void }) {
  const dates = [...byDate.keys()].filter((d) => isSameMonth(parseISO(d), monthStart)).sort();
  if (dates.length === 0) {
    return (
      <Card>
        <EmptyState title="Nothing scheduled this month" />
      </Card>
    );
  }
  return (
    <div className="flex flex-col gap-4">
      {dates.map((d) => (
        <section key={d} aria-label={format(parseISO(d), 'EEEE d MMMM')}>
          <h3 className="mb-1.5 text-sm font-semibold">
            {relativeDay(d)} <span className="font-normal text-muted">{format(parseISO(d), 'd MMM')}</span>
          </h3>
          <Card>
            <ul className="divide-y divide-border">
              {(byDate.get(d) ?? []).map((a) => (
                <li key={a.id}>
                  <button type="button" onClick={() => onOpen(a)} className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-surface-2">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">{a.workout.name}</span>
                      <span className="block truncate text-xs text-muted">{fullName(a.client)}</span>
                    </span>
                    <AssignmentStatusBadge status={a.status} inProgress={a.sessionId !== null} />
                  </button>
                </li>
              ))}
            </ul>
          </Card>
        </section>
      ))}
    </div>
  );
}
