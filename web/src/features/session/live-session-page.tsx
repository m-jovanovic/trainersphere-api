import { onlineManager, useMutation, useMutationState, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, getRouteApi, useNavigate } from '@tanstack/react-router';
import { ChevronLeft, ChevronRight, Dumbbell, Flag, Loader2, MoreHorizontal, Plus, Trash2, Trophy, WifiOff, X } from 'lucide-react';
import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { toast } from 'sonner';
import { sessionsApi } from '@/lib/api/endpoints';
import { ApiError, NetworkError, errorMessage, isApiError } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { Exercise, Session, SessionExercise } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatDay } from '@/lib/dates';
import { formatDuration, formatPrDelta, formatPrValue, formatSetLine, formatVolume, prLabels } from '@/lib/domain';
import { useNow } from '@/lib/use-now';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Textarea } from '@/components/ui/input';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Badge, Card, CardBody, EmptyState, Stat } from '@/components/ui/misc';
import { ExerciseArt } from '@/components/exercise-art';
import { NotFound, PageSpinner } from '@/components/layout/page-states';
import { SyncStatus } from '@/components/layout/sync-status';
import { ExercisePicker } from '@/features/exercises/exercise-picker';
import { afterSetCompleted, completedCount, firstIncompleteIndex, lastTimeHint, localTotals, plannedRows, restText, sessionProgress, targetText } from './logging';
import { originalId, useResolvedId } from './id-aliases';
import { runSessionWrite, sessionKeys, type CompleteVars } from './offline';
import { RestTimerBar } from './rest-timer';
import { useRestTimer } from './use-rest-timer';
import { SetRow } from './set-row';
import { useWakeLock } from './use-wake-lock';

const route = getRouteApi('/_authed/log/$sessionId');
const MAX_POSITION = 50;
const ignore = () => undefined;

export function LiveSessionPage() {
  const { sessionId } = route.useParams();
  const navigate = useNavigate();
  const resolved = useResolvedId(sessionId);
  // Local edits are the source of truth while logging; a background refetch would drop queued sets.
  const q = useQuery({ ...queries.session(resolved), staleTime: Infinity, refetchOnWindowFocus: false, refetchOnReconnect: false });
  const startError = useMutationState({
    filters: { mutationKey: sessionKeys.start, predicate: (m) => (m.state.variables as { id?: string } | undefined)?.id === originalId(sessionId) },
    select: (m) => m.state.error,
  }).at(-1);

  useEffect(() => {
    if (resolved !== sessionId) void navigate({ to: '/log/$sessionId', params: { sessionId: resolved }, replace: true });
  }, [resolved, sessionId, navigate]);

  if (startError instanceof ApiError) {
    const other = typeof startError.problem.sessionId === 'string' ? startError.problem.sessionId : null;
    if (startError.code === 'Session.AlreadyStartedForAssignment' || (startError.code === 'Assignment.NotScheduled' && other)) return <AlreadyStarted otherId={other} />;
    return <StartRejected error={startError} />;
  }
  if (!q.data) {
    if (q.error) {
      if (isApiError(q.error, 404)) {
        return (
          <FullScreen>
            <NotFound />
          </FullScreen>
        );
      }
      return (
        <FullScreen>
          <EmptyState
            className="min-h-[60dvh]"
            icon={<WifiOff />}
            title={q.error instanceof NetworkError ? 'This workout is not on this device' : 'Could not open this workout'}
            description={q.error instanceof NetworkError ? 'Reconnect to open it. Workouts you start here keep working offline.' : errorMessage(q.error)}
            action={
              <Button variant="secondary" onClick={() => void q.refetch()}>
                Try again
              </Button>
            }
          />
        </FullScreen>
      );
    }
    return (
      <FullScreen>
        <PageSpinner />
      </FullScreen>
    );
  }
  if (q.data.status === 'completed') return <SessionComplete session={q.data} />;
  return <Logger session={q.data} />;
}

function FullScreen({ children }: { children: React.ReactNode }) {
  return <div className="min-h-dvh bg-bg px-4 pt-safe">{children}</div>;
}

function AlreadyStarted({ otherId }: { otherId: string | null }) {
  return (
    <FullScreen>
      <EmptyState
        className="min-h-[60dvh]"
        icon={<Dumbbell />}
        title="This workout was already started"
        description="It was started on another device. Continue it there, or open it here."
        action={
          <div className="flex flex-col gap-2 sm:flex-row">
            {otherId && (
              <Button asChild>
                <Link to="/log/$sessionId" params={{ sessionId: otherId }}>
                  Open that workout
                </Link>
              </Button>
            )}
            <Button asChild variant="secondary">
              <Link to="/">Back to today</Link>
            </Button>
          </div>
        }
      />
    </FullScreen>
  );
}

function StartRejected({ error }: { error: ApiError }) {
  const description =
    error.code === 'Assignment.NotScheduled'
      ? 'It was completed or skipped in the meantime, so it can no longer be logged.'
      : error.code === 'Client.Archived'
        ? 'Your account is archived by your trainer, so new workouts cannot be logged.'
        : errorMessage(error);
  return (
    <FullScreen>
      <EmptyState
        className="min-h-[60dvh]"
        icon={<Dumbbell />}
        title="This workout could not be started"
        description={description}
        action={
          <Button asChild variant="secondary">
            <Link to="/">Back to today</Link>
          </Button>
        }
      />
    </FullScreen>
  );
}

function Elapsed({ startedAt }: { startedAt: string }) {
  const now = useNow(1000);
  return <span className="tabular">{formatDuration((now - Date.parse(startedAt)) / 1000)}</span>;
}

function Logger({ session: s }: { session: Session }) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [index, setIndex] = useState(() => firstIncompleteIndex(s));
  const [extraRows, setExtraRows] = useState<Record<number, number>>({});
  const [expanded, setExpanded] = useState<Record<number, number | null>>({});
  const [finishOpen, setFinishOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [announcement, setAnnouncement] = useState('');
  const rest = useRestTimer();
  useWakeLock();

  const discard = useMutation({
    mutationFn: () => sessionsApi.discard(s.id),
    onSuccess: async () => {
      qc.removeQueries({ queryKey: queries.session(s.id).queryKey });
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      void qc.invalidateQueries({ queryKey: keys.assignments });
      void qc.invalidateQueries({ queryKey: keys.sessions });
      toast.success('Workout discarded.');
      await navigate({ to: '/', replace: true });
    },
    onError: (err) => toast.error(errorMessage(err)),
  });

  const exercises = s.exercises;
  const safeIndex = Math.min(index, Math.max(0, exercises.length - 1));
  const current = exercises[safeIndex];
  const progress = sessionProgress(s);

  const goTo = (i: number) => {
    const target = exercises[i];
    if (!target) return;
    setIndex(i);
    setAnnouncement(`Exercise ${i + 1} of ${exercises.length}: ${target.exercise.name}`);
    window.scrollTo({ top: 0 });
  };

  const addExercises = (picked: Exercise[]) => {
    const key = queries.session(s.id).queryKey;
    const prev = qc.getQueryData<Session>(key);
    if (!prev) return;
    let pos = prev.exercises.reduce((m, e) => Math.max(m, e.position), 0);
    const added: SessionExercise[] = picked.slice(0, MAX_POSITION - pos).map((x) => ({
      position: ++pos,
      exerciseId: x.id,
      exercise: { id: x.id, slug: x.slug, name: x.name, illustration: x.illustration },
      prescription: null,
      lastPerformance: null,
      sets: [],
    }));
    qc.setQueryData<Session>(key, { ...prev, exercises: [...prev.exercises, ...added] });
    setIndex(prev.exercises.length);
  };

  const onSetCompleted = (e: SessionExercise, setNumber: number) => {
    const i = exercises.indexOf(e);
    const ticked: Session = {
      ...s,
      exercises: exercises.map((x) =>
        x === e
          ? { ...x, sets: [...x.sets.filter((y) => y.setNumber !== setNumber), { id: '', exerciseId: x.exerciseId, position: x.position, setNumber, weightKg: null, reps: null, durationSeconds: null, distanceMeters: null, rpe: null, completed: true, notes: null, updatedAt: '' }] }
          : x,
      ),
    };
    const updated = ticked.exercises[i];
    if (!updated) return;
    const rows = plannedRows(updated) + (extraRows[e.position] ?? 0);
    const nextOpen = Array.from({ length: rows }, (_, k) => k + 1).find((n) => n > setNumber && !updated.sets.some((x) => x.setNumber === n && x.completed));
    setExpanded((m) => ({ ...m, [e.position]: nextOpen ?? null }));

    const { rest: shouldRest, nextIndex } = afterSetCompleted(ticked, i);
    const restSeconds = updated.prescription?.restSeconds ?? 0;
    if (shouldRest && restSeconds > 0 && sessionProgress(ticked).done < sessionProgress(ticked).planned) rest.start(restSeconds);
    if (nextIndex !== i) goTo(nextIndex);
  };

  const isLast = safeIndex >= exercises.length - 1;
  const currentDone = current ? completedCount(current) >= plannedRows(current) : false;
  const next = exercises[safeIndex + 1];

  return (
    <div className="min-h-dvh bg-bg">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/95 pt-safe backdrop-blur">
        <div className="mx-auto flex max-w-2xl items-center gap-1.5 px-2 py-2">
          <Button asChild variant="ghost" size="icon" aria-label="Close (the workout stays in progress)">
            <Link to="/">
              <X />
            </Link>
          </Button>
          <div className="min-w-0 flex-1">
            <h1 className="truncate font-semibold">{s.workoutName}</h1>
            <p className="text-xs text-muted">
              <Elapsed startedAt={s.startedAt} /> · {progress.done}/{progress.planned} sets
            </p>
          </div>
          <SyncStatus compact />
          <Menu
            trigger={
              <Button variant="ghost" size="icon" aria-label="Workout options">
                <MoreHorizontal />
              </Button>
            }
          >
            <MenuItem icon={<Plus />} onSelect={() => setPickerOpen(true)}>
              Add exercise
            </MenuItem>
            <MenuItem icon={<Trash2 />} tone="danger" onSelect={() => setDiscardOpen(true)}>
              Discard workout
            </MenuItem>
          </Menu>
          <Button onClick={() => setFinishOpen(true)}>
            <Flag aria-hidden />
            Finish
          </Button>
        </div>
        <div className="h-1 bg-surface-2" aria-hidden>
          <div className="h-full bg-primary transition-[width]" style={{ width: `${progress.planned ? (progress.done / progress.planned) * 100 : 0}%` }} />
        </div>
      </header>

      {exercises.length > 1 && <ExerciseStrip exercises={exercises} index={safeIndex} onSelect={goTo} />}

      <main className={cn('mx-auto max-w-2xl px-4 pt-4', rest.state ? 'pb-48' : 'pb-28')}>
        {current ? (
          <ExercisePanel
            key={current.position}
            sessionId={s.id}
            exercise={current}
            index={safeIndex}
            count={exercises.length}
            extraRows={extraRows[current.position] ?? 0}
            expandedSet={expanded[current.position]}
            onExpand={(n) => setExpanded((m) => ({ ...m, [current.position]: n }))}
            onAddRow={() => {
              const rows = plannedRows(current) + (extraRows[current.position] ?? 0);
              setExtraRows((m) => ({ ...m, [current.position]: (m[current.position] ?? 0) + 1 }));
              setExpanded((m) => ({ ...m, [current.position]: rows + 1 }));
            }}
            onRemoveRow={() => setExtraRows((m) => ({ ...m, [current.position]: Math.max(0, (m[current.position] ?? 0) - 1) }))}
            onSetCompleted={(n) => onSetCompleted(current, n)}
            onSwipe={(dir) => goTo(safeIndex + dir)}
          />
        ) : (
          <Card>
            <EmptyState
              icon={<Dumbbell />}
              title="Add your first exercise"
              description="Pick what you are training; sets are saved as you log them."
              action={
                <Button onClick={() => setPickerOpen(true)}>
                  <Plus aria-hidden />
                  Add exercise
                </Button>
              }
            />
          </Card>
        )}
        {current && exercises.length < MAX_POSITION && (
          <Button variant="ghost" className="mt-4 w-full" onClick={() => setPickerOpen(true)}>
            <Plus aria-hidden />
            Add exercise
          </Button>
        )}
      </main>

      <div className="fixed inset-x-0 bottom-0 z-20">
        {rest.state && <RestTimerBar state={rest.state} onAdd={rest.add} onStop={rest.stop} />}
        {current && (
          <nav aria-label="Exercise navigation" className="border-t border-border bg-surface pb-[max(0.5rem,env(safe-area-inset-bottom))]">
            <div className="mx-auto flex max-w-2xl gap-2 px-4 pt-2">
              <Button variant="secondary" size="lg" className="px-4" aria-label="Previous exercise" disabled={safeIndex === 0} onClick={() => goTo(safeIndex - 1)}>
                <ChevronLeft />
              </Button>
              {isLast ? (
                <Button size="lg" className="flex-1" variant={currentDone ? 'primary' : 'secondary'} onClick={() => setFinishOpen(true)}>
                  <Flag aria-hidden />
                  Finish workout
                </Button>
              ) : (
                <Button size="lg" className="min-w-0 flex-1" variant={currentDone ? 'primary' : 'secondary'} onClick={() => goTo(safeIndex + 1)}>
                  <span className="truncate">Next: {next?.exercise.name}</span>
                  <ChevronRight aria-hidden />
                </Button>
              )}
            </div>
          </nav>
        )}
      </div>

      <p className="sr-only" aria-live="polite">
        {announcement}
      </p>

      <FinishDialog open={finishOpen} onOpenChange={setFinishOpen} session={s} onFinish={rest.stop} />
      <ConfirmDialog
        open={discardOpen}
        onOpenChange={setDiscardOpen}
        title="Discard this workout?"
        description="Everything logged in this workout is deleted. A scheduled workout goes back to your plan."
        confirmLabel="Discard workout"
        loading={discard.isPending}
        onConfirm={() => {
          if (!onlineManager.isOnline()) {
            toast.error('Reconnect to discard this workout.');
            return;
          }
          discard.mutate();
        }}
      />
      <ExercisePicker open={pickerOpen} onOpenChange={setPickerOpen} onAdd={addExercises} allowCreate={false} />
    </div>
  );
}

function ExerciseStrip({ exercises, index, onSelect }: { exercises: SessionExercise[]; index: number; onSelect: (i: number) => void }) {
  const currentRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    currentRef.current?.scrollIntoView({ inline: 'center', block: 'nearest' });
  }, [index]);
  return (
    <nav aria-label="Exercises" className="border-b border-border bg-surface">
      <ol className="mx-auto flex max-w-2xl gap-1.5 overflow-x-auto px-4 py-2 [scrollbar-width:none]">
        {exercises.map((e, i) => {
          const done = completedCount(e);
          const total = plannedRows(e);
          const complete = done >= total;
          return (
            <li key={e.position} className="shrink-0">
              <button
                ref={i === index ? currentRef : undefined}
                type="button"
                aria-current={i === index ? 'step' : undefined}
                onClick={() => onSelect(i)}
                className={cn(
                  'flex min-h-11 items-center gap-2 rounded-full border px-3 text-sm whitespace-nowrap',
                  i === index ? 'border-primary bg-primary-soft font-semibold text-primary-soft-fg' : 'border-border text-muted hover:text-fg',
                )}
              >
                <span className="max-w-36 truncate">
                  {i + 1}. {e.exercise.name}
                </span>
                <span className={cn('tabular text-xs', complete && 'text-primary')}>
                  {done}/{total}
                </span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

interface ExercisePanelProps {
  sessionId: string;
  exercise: SessionExercise;
  index: number;
  count: number;
  extraRows: number;
  expandedSet: number | null | undefined;
  onExpand: (setNumber: number) => void;
  onAddRow: () => void;
  onRemoveRow: () => void;
  onSetCompleted: (setNumber: number) => void;
  onSwipe: (dir: 1 | -1) => void;
}

function ExercisePanel({ sessionId, exercise: e, index, count, extraRows, expandedSet, onExpand, onAddRow, onRemoveRow, onSetCompleted, onSwipe }: ExercisePanelProps) {
  const units = useUnits();
  const p = e.prescription;
  const rows = plannedRows(e) + extraRows;
  const planned = p?.sets ?? 1;
  const firstOpen = Array.from({ length: rows }, (_, k) => k + 1).find((n) => !e.sets.some((x) => x.setNumber === n && x.completed));
  const open = expandedSet === undefined ? (firstOpen ?? null) : expandedSet;
  const target = targetText(p, units);
  const restLine = restText(p);
  const start = useRef<{ x: number; y: number } | null>(null);

  const onPointerDown = (ev: ReactPointerEvent) => {
    start.current = ev.pointerType === 'touch' ? { x: ev.clientX, y: ev.clientY } : null;
  };
  const onPointerUp = (ev: ReactPointerEvent) => {
    const s0 = start.current;
    start.current = null;
    if (!s0) return;
    const dx = ev.clientX - s0.x;
    const dy = ev.clientY - s0.y;
    if (Math.abs(dx) > 70 && Math.abs(dy) < 50) {
      if (dx < 0 && index < count - 1) onSwipe(1);
      if (dx > 0 && index > 0) onSwipe(-1);
    }
  };

  return (
    <section aria-labelledby="exercise-title" onPointerDown={onPointerDown} onPointerUp={onPointerUp}>
      <div className="mb-4 flex items-start gap-4">
        <ExerciseArt illustration={e.exercise.illustration} name={e.exercise.name} className="size-24 shrink-0 rounded-xl sm:size-28" />
        <div className="min-w-0 flex-1 pt-1">
          <p className="flex items-center gap-2 text-xs font-medium tracking-wide text-muted uppercase">
            Exercise {index + 1} of {count}
            {p?.supersetGroup && <Badge tone="primary">Superset {p.supersetGroup}</Badge>}
          </p>
          <h2 id="exercise-title" className="mt-0.5 text-xl leading-tight font-semibold">
            {e.exercise.name}
          </h2>
          {target && <p className="tabular mt-1 text-sm font-medium">{target}</p>}
          {(restLine || p?.tempo) && (
            <p className="text-sm text-muted">
              {[restLine, p?.tempo ? `Tempo ${p.tempo}` : null].filter(Boolean).join(' · ')}
            </p>
          )}
        </div>
      </div>
      {p?.notes && (
        <p className="mb-3 rounded-lg border-l-2 border-primary bg-surface-2 px-3 py-2 text-sm">
          <span className="font-medium">Coach: </span>
          {p.notes}
        </p>
      )}
      {e.lastPerformance && (
        <p className="mb-3 text-sm text-muted">
          Last time ({formatDay(e.lastPerformance.date, 'd MMM')}): {e.lastPerformance.sets.map((x) => formatSetLine(x, units)).join(', ')}
        </p>
      )}
      <ol aria-label={`${e.exercise.name} sets`} className="flex flex-col gap-2">
        {Array.from({ length: rows }, (_, k) => k + 1).map((n) => (
          <SetRow
            key={n}
            sessionId={sessionId}
            exercise={e}
            setNumber={n}
            set={e.sets.find((x) => x.setNumber === n)}
            lastHint={lastTimeHint(e, n, units)}
            expanded={open === n}
            onExpand={() => onExpand(n)}
            removable={n === rows && n > planned}
            onRemoveRow={onRemoveRow}
            onCompleted={() => onSetCompleted(n)}
          />
        ))}
      </ol>
      {rows < MAX_POSITION && (
        <Button variant="secondary" className="mt-3 w-full" onClick={onAddRow}>
          <Plus aria-hidden />
          Add set
        </Button>
      )}
    </section>
  );
}

function FinishDialog({ open, onOpenChange, session: s, onFinish }: { open: boolean; onOpenChange: (o: boolean) => void; session: Session; onFinish: () => void }) {
  const qc = useQueryClient();
  const [notes, setNotes] = useState(s.notes ?? '');
  const { done, planned } = sessionProgress(s);
  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title="Finish workout?"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Keep going
          </Button>
          <Button
            onClick={() => {
              const vars: CompleteVars = { sessionId: s.id, completedAt: new Date().toISOString(), notes: notes.trim() || null };
              onFinish();
              onOpenChange(false);
              runSessionWrite(qc, sessionKeys.complete, s.id, vars).catch(ignore);
            }}
          >
            <Flag aria-hidden />
            Finish workout
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <p className="text-sm">
          <span className="tabular font-semibold">
            {done} of {planned}
          </span>{' '}
          sets logged.{' '}
          {done === 0 ? 'Nothing is logged yet; you can discard the workout from the options menu instead.' : done < planned ? 'Sets you did not tick are left out.' : 'Every planned set is done.'}
        </p>
        <Field label="Notes" optional hint="How did it feel? Your trainer can read this.">
          <Textarea rows={3} value={notes} maxLength={2000} onChange={(ev) => setNotes(ev.target.value)} />
        </Field>
      </div>
    </Dialog>
  );
}

function SessionComplete({ session: s }: { session: Session }) {
  const units = useUnits();
  const state = useMutationState({
    filters: { mutationKey: sessionKeys.complete, predicate: (m) => (m.state.variables as CompleteVars | undefined)?.sessionId === s.id },
    select: (m) => ({ status: m.state.status, paused: m.state.isPaused }),
  }).at(-1);
  const syncing = state?.status === 'pending';
  const totals = syncing ? localTotals(s) : s.summary;
  const duration = s.summary.durationSeconds ?? (s.completedAt ? (Date.parse(s.completedAt) - Date.parse(s.startedAt)) / 1000 : null);
  const prs = s.summary.personalRecords;

  return (
    <div className="min-h-dvh bg-bg pt-safe">
      <main className="mx-auto flex max-w-xl flex-col gap-5 px-4 py-8">
        <div className="flex flex-col items-center text-center">
          <span className="mb-3 flex size-16 items-center justify-center rounded-full bg-primary-soft text-primary">
            <Flag className="size-8" aria-hidden />
          </span>
          <h1 className="text-2xl font-semibold tracking-tight">Workout complete</h1>
          <p className="mt-1 text-muted">{s.workoutName}</p>
        </div>
        <Card>
          <CardBody className="grid grid-cols-2 gap-4">
            <Stat label="Volume" value={formatVolume(totals.totalVolumeKg, units)} />
            <Stat label="Sets" value={totals.completedSets} />
            <Stat label="Reps" value={totals.totalReps} />
            <Stat label="Duration" value={duration === null ? '–' : formatDuration(duration)} />
          </CardBody>
        </Card>
        <Card className={cn(prs.length > 0 && 'border-pr/30 bg-pr-soft/40')}>
          <CardBody>
            {syncing ? (
              <p role="status" className="flex items-center gap-2 text-sm text-muted">
                {state.paused ? <WifiOff className="size-4" aria-hidden /> : <Loader2 className="size-4 animate-spin" aria-hidden />}
                {state.paused ? 'Saved on this device. Personal records show up once it syncs.' : 'Checking for personal records…'}
              </p>
            ) : prs.length === 0 ? (
              <p className="text-sm text-muted">No new personal records this time. Showing up is what builds them.</p>
            ) : (
              <>
                <h2 className="mb-3 flex items-center gap-2 font-semibold">
                  <Trophy className="size-5 text-pr" aria-hidden />
                  {prs.length} new personal record{prs.length === 1 ? '' : 's'}
                </h2>
                <ul className="flex flex-col gap-3">
                  {prs.map((pr) => (
                    <li key={pr.id} className="flex items-center gap-3">
                      <ExerciseArt illustration={pr.exercise.illustration} name={pr.exercise.name} variant="sm" className="size-10 shrink-0" />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{pr.exercise.name}</span>
                        <span className="block text-xs text-muted">{prLabels[pr.type]}</span>
                      </span>
                      <span className="text-right">
                        <span className="tabular block text-sm font-semibold">{formatPrValue(pr, units)}</span>
                        <span className="tabular block text-xs text-pr">{formatPrDelta(pr, units)}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </CardBody>
        </Card>
        <div className="flex flex-col gap-2">
          <Button asChild size="xl">
            <Link to="/">Done</Link>
          </Button>
          <Button asChild variant="ghost">
            <Link to="/sessions/$sessionId" params={{ sessionId: s.id }}>
              View workout details
            </Link>
          </Button>
        </div>
      </main>
    </div>
  );
}
