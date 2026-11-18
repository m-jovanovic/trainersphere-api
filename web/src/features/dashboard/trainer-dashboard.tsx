import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { CalendarPlus, ChevronRight, Dumbbell, Trophy, UserPlus } from 'lucide-react';
import { useState } from 'react';
import { queries } from '@/lib/api/queries';
import type { TrainerDashboard as Dashboard } from '@/lib/api/types';
import { useUnits, useUser } from '@/lib/auth/hooks';
import { formatAgo, formatDay } from '@/lib/dates';
import { formatPercent, formatPrDelta, formatPrValue, formatVolume, fullName, prLabels } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Avatar, Badge, Card, CardBody, CardHeader, EmptyState, PageHeader, Skeleton, Stat } from '@/components/ui/misc';
import { AdherenceMeter, AssignmentStatusBadge } from '@/components/status';
import { QueryError } from '@/components/layout/page-states';
import { InviteClientDialog, PendingInvitationList } from '@/features/clients/invitations';

function greeting(date = new Date()) {
  const h = date.getHours();
  if (h < 12) return 'Good morning';
  if (h < 18) return 'Good afternoon';
  return 'Good evening';
}

export function TrainerDashboard() {
  const user = useUser();
  const { data, error, refetch } = useQuery(queries.trainerDashboard());
  const [inviteOpen, setInviteOpen] = useState(false);

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={`${greeting()}, ${user.firstName}. ${data ? formatDay(data.date, 'EEEE d MMMM') : ''}`}
        actions={
          <>
            <Button variant="secondary" asChild>
              <Link to="/schedule">
                <CalendarPlus aria-hidden />
                Schedule
              </Link>
            </Button>
            <Button onClick={() => setInviteOpen(true)}>
              <UserPlus aria-hidden />
              Invite client
            </Button>
          </>
        }
      />
      {error && !data ? <QueryError error={error} onRetry={() => void refetch()} /> : data ? <DashboardBody data={data} onInvite={() => setInviteOpen(true)} /> : <DashboardSkeleton />}
      <InviteClientDialog open={inviteOpen} onOpenChange={setInviteOpen} />
    </>
  );
}

function DashboardSkeleton() {
  return (
    <div className="flex flex-col gap-4" aria-busy="true" aria-label="Loading dashboard">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-24" />
        ))}
      </div>
      <Skeleton className="h-64" />
    </div>
  );
}

function DashboardBody({ data, onInvite }: { data: Dashboard; onInvite: () => void }) {
  const units = useUnits();
  const doneToday = data.today.filter((t) => t.status === 'completed').length;
  const rates = data.adherence.map((a) => a.adherence7d).filter((r): r is number => r !== null);
  const avg = rates.length ? rates.reduce((a, b) => a + b, 0) / rates.length : null;

  return (
    <div className="flex flex-col gap-4 sm:gap-6">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Card className="p-4">
          <Stat label="Active clients" value={data.activeClients} />
        </Card>
        <Card className="p-4">
          <Stat label="Done today" value={`${doneToday}/${data.today.length}`} hint="Workouts completed" />
        </Card>
        <Card className="p-4">
          <Stat label="Adherence 7d" value={formatPercent(avg)} hint="Average across clients" />
        </Card>
        <Card className="p-4">
          <Stat label="Pending invites" value={data.pendingInvitations.count} />
        </Card>
      </div>

      <div className="grid gap-4 sm:gap-6 lg:grid-cols-3">
        <div className="flex flex-col gap-4 sm:gap-6 lg:col-span-2">
          <Card>
            <CardHeader title="Today" description={`${data.today.length} workout${data.today.length === 1 ? '' : 's'} on the schedule`} />
            <CardBody>
              {data.today.length === 0 ? (
                <EmptyState
                  className="py-6"
                  icon={<Dumbbell />}
                  title="Nothing scheduled today"
                  action={
                    <Button variant="secondary" size="sm" asChild>
                      <Link to="/schedule">Open schedule</Link>
                    </Button>
                  }
                />
              ) : (
                <ul className="divide-y divide-border">
                  {data.today.map((t) => (
                    <li key={t.assignmentId ?? t.sessionId} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                      <Avatar name={t.client} />
                      <div className="min-w-0 flex-1">
                        <Link to="/clients/$clientId" params={{ clientId: t.client.id }} className="block truncate text-sm font-medium hover:underline">
                          {fullName(t.client)}
                        </Link>
                        <p className="truncate text-sm text-muted">{t.workoutName}</p>
                      </div>
                      <AssignmentStatusBadge status={t.status} inProgress={t.sessionId !== null && t.status === 'scheduled'} />
                      {t.sessionId && (
                        <Button variant="ghost" size="icon-sm" asChild>
                          <Link to="/sessions/$sessionId" params={{ sessionId: t.sessionId }} aria-label={`View ${fullName(t.client)}'s session`}>
                            <ChevronRight />
                          </Link>
                        </Button>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </CardBody>
          </Card>

          <Card>
            <CardHeader
              title="Adherence"
              description="Completed vs due, lowest first"
              action={
                <Button variant="link" size="sm" asChild>
                  <Link to="/clients" search={{ sort: 'adherence7d' }}>
                    All clients
                  </Link>
                </Button>
              }
            />
            <CardBody>
              {data.adherence.length === 0 ? (
                <EmptyState className="py-6" title="No active clients yet" action={<Button size="sm" onClick={onInvite}>Invite your first client</Button>} />
              ) : (
                <ul className="flex flex-col gap-4">
                  {data.adherence.map((a) => (
                    <li key={a.client.id} className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-1 sm:grid-cols-[12rem_1fr_auto]">
                      <Link to="/clients/$clientId" params={{ clientId: a.client.id }} className="flex min-w-0 items-center gap-2 text-sm font-medium hover:underline">
                        <Avatar name={a.client} className="size-7 text-[10px]" />
                        <span className="truncate">{fullName(a.client)}</span>
                      </Link>
                      <span className="text-right text-xs text-muted sm:order-last">Last session {formatAgo(a.lastSessionAt)}</span>
                      <div className="col-span-2 sm:col-span-1">
                        <AdherenceMeter rate={a.adherence7d} label={`${fullName(a.client)} adherence, last 7 days`} />
                        <p className="mt-0.5 text-xs text-muted">
                          {a.completed7d}/{a.due7d} this week · {formatPercent(a.adherence28d)} over 28 days
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </CardBody>
          </Card>
        </div>

        <div className="flex flex-col gap-4 sm:gap-6">
          <Card>
            <CardHeader title="Recent PRs" description="Last 14 days" />
            <CardBody>
              {data.recentPersonalRecords.length === 0 ? (
                <p className="text-sm text-muted">No new records yet. They show up here as clients log sessions.</p>
              ) : (
                <ul className="flex flex-col gap-3">
                  {data.recentPersonalRecords.slice(0, 6).map((pr) => (
                    <li key={pr.id} className="flex items-start gap-3">
                      <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-full bg-pr-soft text-pr">
                        <Trophy className="size-4" aria-hidden />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">{pr.exercise.name}</p>
                        <p className="truncate text-xs text-muted">
                          {fullName(pr.client)} · {prLabels[pr.type]} · {formatAgo(pr.achievedAt)}
                        </p>
                      </div>
                      <div className="text-right">
                        <p className="tabular text-sm font-semibold">{formatPrValue(pr, units)}</p>
                        <p className="tabular text-xs text-pr">{formatPrDelta(pr, units)}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </CardBody>
          </Card>

          <Card>
            <CardHeader
              title="Pending invitations"
              action={
                <Button variant="ghost" size="sm" onClick={onInvite}>
                  <UserPlus aria-hidden />
                  Invite
                </Button>
              }
            />
            <CardBody>
              {data.pendingInvitations.items.length === 0 ? (
                <p className="text-sm text-muted">No invitations waiting.</p>
              ) : (
                <PendingInvitationList items={data.pendingInvitations.items} />
              )}
            </CardBody>
          </Card>

          <Card>
            <CardHeader title="Recent sessions" />
            <CardBody>
              {data.recentSessions.length === 0 ? (
                <p className="text-sm text-muted">No sessions logged yet.</p>
              ) : (
                <ul className="-mx-2 flex flex-col">
                  {data.recentSessions.slice(0, 6).map((s) => (
                    <li key={s.id}>
                      <Link to="/sessions/$sessionId" params={{ sessionId: s.id }} className="flex items-center gap-3 rounded-lg px-2 py-2 hover:bg-surface-2">
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm font-medium">{s.workoutName}</p>
                          <p className="truncate text-xs text-muted">
                            {fullName(s.client)} · {formatAgo(s.completedAt ?? s.startedAt)}
                          </p>
                        </div>
                        {s.personalRecordCount > 0 && (
                          <Badge tone="pr">
                            <Trophy aria-hidden />
                            {s.personalRecordCount}
                          </Badge>
                        )}
                        <span className="tabular text-xs text-muted">{formatVolume(s.totalVolumeKg, units)}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </CardBody>
          </Card>
        </div>
      </div>
    </div>
  );
}
