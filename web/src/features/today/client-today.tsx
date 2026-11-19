import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { format } from 'date-fns';
import { ChevronRight, Flame, Moon, MoreHorizontal, Play, Plus, Scale, SkipForward } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { assignmentsApi } from '@/lib/api/endpoints';
import { ApiError, errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { Assignment, ClientDashboard } from '@/lib/api/types';
import { useUnits, useUser } from '@/lib/auth/hooks';
import { formatAgo, formatDateTime, relativeDay } from '@/lib/dates';
import { formatGoal, formatLoad, formatPercent, formatVolume, formatWeight } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Input, Textarea } from '@/components/ui/input';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Badge, Card, CardBody, CardHeader, Skeleton } from '@/components/ui/misc';
import { PrList, SessionPrBadge } from '@/components/pr-list';
import { AdherenceMeter, AssignmentStatusBadge } from '@/components/status';
import { ExerciseArt } from '@/components/exercise-art';
import { QueryError } from '@/components/layout/page-states';
import { useLocalStarts, useStartWorkout } from '@/features/session/use-start-workout';

function greeting(d = new Date()) {
  const h = d.getHours();
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
}

export function ClientToday() {
  const user = useUser();
  const { data, error, refetch } = useQuery(queries.clientDashboard());
  const [freeOpen, setFreeOpen] = useState(false);

  return (
    <>
      <header className="mb-5">
        <p className="text-sm text-muted">{format(new Date(), 'EEEE d MMMM')}</p>
        <h1 className="text-2xl font-semibold tracking-tight">
          {greeting()}, {user.firstName}
        </h1>
      </header>
      {error && !data ? (
        <Card>
          <QueryError error={error} onRetry={() => void refetch()} />
        </Card>
      ) : !data ? (
        <div className="flex flex-col gap-4" aria-busy="true" aria-label="Loading today">
          <Skeleton className="h-72" />
          <Skeleton className="h-28" />
        </div>
      ) : (
        <TodayContent data={data} onFreeWorkout={() => setFreeOpen(true)} />
      )}
      <FreeWorkoutDialog open={freeOpen} onOpenChange={setFreeOpen} />
    </>
  );
}

function TodayContent({ data, onFreeWorkout }: { data: ClientDashboard; onFreeWorkout: () => void }) {
  const units = useUnits();
  const local = useLocalStarts();
  const start = useStartWorkout();
  const inProgressId = data.inProgressSession?.id ?? local.adHoc[0] ?? null;
  const inProgressShownInToday = data.today.some((a) => a.sessionId === inProgressId || local.byAssignment.get(a.id) === inProgressId);

  return (
    <div className="flex flex-col gap-4 sm:gap-6">
      {inProgressId && !inProgressShownInToday && (
        <Card className="border-primary/40 bg-primary-soft/50">
          <CardBody className="flex items-center gap-3">
            <span className="size-2.5 shrink-0 animate-pulse rounded-full bg-primary" aria-hidden />
            <div className="min-w-0 flex-1">
              <p className="font-semibold">Workout in progress</p>
              <p className="truncate text-sm text-muted">
                {data.inProgressSession ? `${data.inProgressSession.workoutName} · started ${formatAgo(data.inProgressSession.startedAt)}` : 'Started on this device'}
              </p>
            </div>
            <Button onClick={() => void start.open(inProgressId)}>
              <Play aria-hidden />
              Resume
            </Button>
          </CardBody>
        </Card>
      )}

      {data.today.length > 0 ? (
        data.today.map((a) => <TodayWorkout key={a.id} assignment={a} localSessionId={local.byAssignment.get(a.id)} />)
      ) : (
        <RestDay next={data.nextAssignment} localSessionId={data.nextAssignment ? local.byAssignment.get(data.nextAssignment.id) : undefined} />
      )}

      <Button variant="ghost" className="self-center" onClick={onFreeWorkout}>
        <Plus aria-hidden />
        Log a different workout
      </Button>

      <Card>
        <CardBody className="grid grid-cols-3 gap-3">
          <div className="flex flex-col gap-0.5">
            <span className="text-xs font-medium tracking-wide text-muted uppercase">This week</span>
            <span className="tabular text-2xl font-semibold">
              {data.thisWeek.completed}/{data.thisWeek.due}
            </span>
            <span className="text-xs text-muted">workouts done</span>
          </div>
          <div className="flex flex-col gap-0.5">
            <span className="text-xs font-medium tracking-wide text-muted uppercase">Streak</span>
            <span className="tabular flex items-center gap-1 text-2xl font-semibold">
              <Flame className="size-5 text-pr" aria-hidden />
              {data.streakWeeks}
            </span>
            <span className="text-xs text-muted">week{data.streakWeeks === 1 ? '' : 's'} in a row</span>
          </div>
          <div className="flex flex-col gap-0.5">
            <span className="text-xs font-medium tracking-wide text-muted uppercase">28 days</span>
            <span className="tabular text-2xl font-semibold">{formatPercent(data.adherence28d)}</span>
            <span className="text-xs text-muted">on plan</span>
          </div>
          <AdherenceMeter rate={data.thisWeek.rate} label="This week's workouts done" className="col-span-3" />
        </CardBody>
      </Card>

      <div className="grid gap-4 sm:gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader
            title="Recent records"
            description="Last 14 days"
            action={
              <Button asChild variant="link" size="sm">
                <Link to="/progress">Progress</Link>
              </Button>
            }
          />
          <CardBody>
            {data.recentPersonalRecords.length === 0 ? (
              <p className="text-sm text-muted">No new records in the last two weeks. They come from beating your own best sets.</p>
            ) : (
              <PrList records={data.recentPersonalRecords} units={units} limit={5} />
            )}
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Recent workouts"
            action={
              <Button asChild variant="link" size="sm">
                <Link to="/history">History</Link>
              </Button>
            }
          />
          {data.recentSessions.length === 0 ? (
            <CardBody>
              <p className="text-sm text-muted">Your finished workouts show up here.</p>
            </CardBody>
          ) : (
            <ul className="mt-2 divide-y divide-border">
              {data.recentSessions.map((s) => (
                <li key={s.id}>
                  <Link to="/sessions/$sessionId" params={{ sessionId: s.id }} className="flex items-center gap-3 px-4 py-3 hover:bg-surface-2 sm:px-5">
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-2">
                        <span className="truncate text-sm font-medium">{s.workoutName}</span>
                        {s.status === 'in-progress' && <Badge tone="primary">In progress</Badge>}
                      </span>
                      <span className="block text-xs text-muted">
                        {formatDateTime(s.startedAt, 'EEE d MMM')} · {formatVolume(s.totalVolumeKg, units)}
                      </span>
                    </span>
                    <SessionPrBadge count={s.personalRecordCount} />
                    <ChevronRight className="size-4 text-muted" aria-hidden />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      <Card>
        <CardBody className="flex items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-surface-2 text-muted">
            <Scale className="size-5" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-medium">Body weight</p>
            <p className="text-sm text-muted">
              {data.latestBodyMetric?.weightKg != null
                ? `${formatWeight(data.latestBodyMetric.weightKg, units)} · ${relativeDay(data.latestBodyMetric.date).toLowerCase()}`
                : 'Nothing logged yet'}
            </p>
          </div>
          <Button asChild variant="secondary" size="sm">
            <Link to="/progress" hash="body">
              Log
            </Link>
          </Button>
        </CardBody>
      </Card>
    </div>
  );
}

function TodayWorkout({ assignment: a, localSessionId }: { assignment: Assignment; localSessionId: string | undefined }) {
  const units = useUnits();
  const start = useStartWorkout();
  const [skipOpen, setSkipOpen] = useState(false);
  const sessionId = a.sessionId ?? localSessionId;
  const startedHere = !!sessionId;
  const canStart = a.status === 'scheduled' || a.status === 'missed';
  return (
    <Card className="overflow-hidden">
      <div className="flex items-start gap-3 px-4 pt-4 sm:px-5">
        <div className="min-w-0 flex-1">
          <p className="text-xs font-medium tracking-wide text-muted uppercase">{a.programName ?? "Today's workout"}</p>
          <h2 className="text-xl font-semibold">{a.workout.name}</h2>
          {a.workout.description && <p className="mt-0.5 text-sm text-muted">{a.workout.description}</p>}
        </div>
        <AssignmentStatusBadge status={a.status} inProgress={startedHere && a.status === 'scheduled'} />
        {canStart && !startedHere && (
          <Menu
            trigger={
              <Button variant="ghost" size="icon-sm" aria-label={`More options for ${a.workout.name}`}>
                <MoreHorizontal />
              </Button>
            }
          >
            <MenuItem icon={<SkipForward />} onSelect={() => setSkipOpen(true)}>
              Skip today
            </MenuItem>
          </Menu>
        )}
      </div>
      {a.notes && (
        <p className="mx-4 mt-3 rounded-lg border-l-2 border-primary bg-surface-2 px-3 py-2 text-sm sm:mx-5">
          <span className="font-medium">Coach: </span>
          {a.notes}
        </p>
      )}
      <ol className="mt-3 divide-y divide-border border-y border-border" aria-label={`${a.workout.name} exercises`}>
        {a.workout.exercises.map((e) => {
          const load = formatLoad(e.load, units);
          return (
            <li key={e.id} className="flex items-center gap-3 px-4 py-2.5 sm:px-5">
              <ExerciseArt illustration={e.exercise.illustration} name={e.exercise.name} variant="sm" className="size-10 shrink-0" />
              <span className="min-w-0 flex-1">
                <span className="flex items-center gap-2">
                  {e.supersetGroup && <span className="rounded bg-primary-soft px-1.5 text-xs font-semibold text-primary-soft-fg">{e.supersetGroup}</span>}
                  <span className="truncate text-sm font-medium">{e.exercise.name}</span>
                </span>
                <span className="tabular block truncate text-xs text-muted">
                  {e.sets} × {formatGoal(e.goal)}
                  {load ? ` @ ${load}` : ''}
                </span>
              </span>
            </li>
          );
        })}
      </ol>
      <div className="p-4 sm:p-5">
        {a.status === 'completed' && a.sessionId ? (
          <Button asChild variant="secondary" size="lg" className="w-full">
            <Link to="/sessions/$sessionId" params={{ sessionId: a.sessionId }}>
              See how it went
            </Link>
          </Button>
        ) : canStart ? (
          <Button size="xl" className="w-full" onClick={() => void start.fromAssignment(a, localSessionId)}>
            <Play aria-hidden />
            {startedHere ? 'Resume workout' : 'Start workout'}
          </Button>
        ) : (
          <p className="text-center text-sm text-muted">Skipped. Enjoy the rest, or log a different workout below.</p>
        )}
      </div>
      <SkipDialog assignment={a} open={skipOpen} onOpenChange={setSkipOpen} />
    </Card>
  );
}

function SkipDialog({ assignment: a, open, onOpenChange }: { assignment: Assignment; open: boolean; onOpenChange: (o: boolean) => void }) {
  const qc = useQueryClient();
  const [reason, setReason] = useState('');
  const skip = useMutation({
    mutationFn: () => assignmentsApi.skip(a.id, reason.trim() || undefined),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      void qc.invalidateQueries({ queryKey: keys.assignments });
      onOpenChange(false);
      toast.success('Skipped. Your trainer can see this.');
    },
    onError: (err) => {
      if (err instanceof ApiError && err.code === 'Assignment.NotScheduled') {
        void qc.invalidateQueries({ queryKey: keys.dashboard });
        void qc.invalidateQueries({ queryKey: keys.assignments });
        onOpenChange(false);
        toast.error(typeof err.problem.sessionId === 'string' ? 'You already started or finished this workout, so it can no longer be skipped.' : 'This workout was already completed or skipped.');
        return;
      }
      toast.error(errorMessage(err));
    },
  });
  return (
    <ConfirmDialog
      open={open}
      onOpenChange={onOpenChange}
      title={`Skip ${a.workout.name}?`}
      tone="primary"
      confirmLabel="Skip workout"
      loading={skip.isPending}
      onConfirm={() => skip.mutate()}
      description={
        <Field label="Reason" optional hint="Shared with your trainer.">
          <Textarea rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      }
    />
  );
}

function RestDay({ next, localSessionId }: { next: Assignment | null; localSessionId: string | undefined }) {
  const start = useStartWorkout();
  return (
    <Card>
      <CardBody className="flex flex-col items-center gap-2 py-8 text-center">
        <span className="mb-1 flex size-12 items-center justify-center rounded-full bg-surface-2 text-muted">
          <Moon className="size-6" aria-hidden />
        </span>
        <h2 className="text-lg font-semibold">Rest day</h2>
        <p className="max-w-sm text-sm text-muted">Nothing is scheduled today. Recovery is part of the plan.</p>
        {next && (
          <div className="mt-3 w-full max-w-sm rounded-lg border border-border p-3 text-left">
            <p className="text-xs font-medium tracking-wide text-muted uppercase">Up next · {relativeDay(next.date)}</p>
            <p className="font-medium">{next.workout.name}</p>
            <p className="text-sm text-muted">
              {next.workout.exercises.length} exercises{next.programName ? ` · ${next.programName}` : ''}
            </p>
            <Button variant="secondary" size="sm" className="mt-3" onClick={() => void start.fromAssignment(next, localSessionId)}>
              <Play aria-hidden />
              {localSessionId ? 'Resume' : 'Do it today instead'}
            </Button>
          </div>
        )}
      </CardBody>
    </Card>
  );
}

function FreeWorkoutDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  const start = useStartWorkout();
  const [name, setName] = useState('');
  const trimmed = name.trim();
  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title="Log a different workout"
      description="Train something that is not on your plan. You add the exercises as you go."
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            disabled={!trimmed}
            onClick={() => {
              onOpenChange(false);
              setName('');
              void start.adHoc(trimmed);
            }}
          >
            <Play aria-hidden />
            Start
          </Button>
        </>
      }
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!trimmed) return;
          onOpenChange(false);
          setName('');
          void start.adHoc(trimmed);
        }}
      >
        <Field label="Workout name">
          <Input value={name} maxLength={100} placeholder="e.g. Hotel gym" onChange={(e) => setName(e.target.value)} />
        </Field>
      </form>
    </Dialog>
  );
}
