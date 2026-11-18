import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { CircleSlash, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { assignmentsApi } from '@/lib/api/endpoints';
import { ApiError } from '@/lib/api/http';
import { applyProblem } from '@/lib/api/form-errors';
import { queries } from '@/lib/api/queries';
import type { Assignment } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatDay, todayIso } from '@/lib/dates';
import { formatGoal, formatLoad, fullName } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { Field, FormError } from '@/components/ui/field';
import { Input, NativeSelect, Textarea } from '@/components/ui/input';
import { AssignmentStatusBadge } from '@/components/status';
import { useInvalidateSchedule } from './use-invalidate-schedule';

const createSchema = z.object({
  clientId: z.string().min(1, 'Choose a client.'),
  workoutId: z.string().min(1, 'Choose a workout.'),
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date.'),
  notes: z.string().trim().max(1000, 'Keep notes under 1000 characters.'),
});
type CreateValues = z.infer<typeof createSchema>;

/** Schedules a single workout for a client. */
export function ScheduleWorkoutDialog({ open, onOpenChange, clientId, date }: { open: boolean; onOpenChange: (o: boolean) => void; clientId?: string; date?: string }) {
  const invalidate = useInvalidateSchedule();
  const clients = useQuery({ ...queries.clients({ status: 'active', sort: 'name' }), enabled: open && !clientId });
  const workouts = useQuery({ ...queries.workouts(''), enabled: open });
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, formState, reset, setError } = useForm<CreateValues>({
    resolver: zodResolver(createSchema),
    values: { clientId: clientId ?? '', workoutId: '', date: date ?? todayIso(), notes: '' },
    resetOptions: { keepDirtyValues: true },
  });
  const create = useMutation({
    mutationFn: (v: CreateValues) => assignmentsApi.create({ ...v, notes: v.notes || null }),
    onSuccess: (a) => {
      invalidate();
      toast.success(`${a.workout.name} scheduled for ${fullName(a.client)}`, { description: formatDay(a.date, 'EEEE d MMMM') });
      reset();
      onOpenChange(false);
    },
    onError: (err) =>
      setFormError(applyProblem(err, { isField: (k) => k in createSchema.shape, setFieldError: (k, message) => setError(k as keyof CreateValues, { type: 'server', message }) }).formError),
  });
  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        if (!o) setFormError(null);
        onOpenChange(o);
      }}
      title="Schedule a workout"
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" form="schedule-form" loading={create.isPending}>
            Schedule
          </Button>
        </>
      }
    >
      <form id="schedule-form" noValidate className="flex flex-col gap-4" onSubmit={(e) => void handleSubmit((v) => create.mutate(v))(e)}>
        <FormError message={formError} />
        {!clientId && (
          <Field label="Client" error={formState.errors.clientId?.message}>
            <NativeSelect {...register('clientId')}>
              <option value="">Choose a client…</option>
              {clients.data?.items.map((c) => (
                <option key={c.id} value={c.id}>
                  {fullName(c)}
                </option>
              ))}
            </NativeSelect>
          </Field>
        )}
        <Field label="Workout" error={formState.errors.workoutId?.message}>
          <NativeSelect {...register('workoutId')}>
            <option value="">Choose a workout…</option>
            {workouts.data?.items.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name} ({w.exerciseCount} exercises)
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field label="Date" error={formState.errors.date?.message}>
          <Input type="date" {...register('date')} />
        </Field>
        <Field label="Notes for the client" optional error={formState.errors.notes?.message}>
          <Textarea rows={2} {...register('notes')} />
        </Field>
      </form>
    </Dialog>
  );
}

const editSchema = z.object({
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date.'),
  notes: z.string().trim().max(1000, 'Keep notes under 1000 characters.'),
});
type EditValues = z.infer<typeof editSchema>;

/** Details of one scheduled workout, with reschedule, skip and delete for the trainer. */
export function AssignmentDialog({ assignment: a, onClose, showClient = true }: { assignment: Assignment; onClose: () => void; showClient?: boolean }) {
  const units = useUnits();
  const invalidate = useInvalidateSchedule();
  const [formError, setFormError] = useState<string | null>(null);
  const [startedSessionId, setStartedSessionId] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const editable = a.status === 'scheduled' || a.status === 'missed';
  const { register, handleSubmit, formState, setError } = useForm<EditValues>({ resolver: zodResolver(editSchema), defaultValues: { date: a.date, notes: a.notes ?? '' } });
  const fail = (err: unknown) => {
    if (err instanceof ApiError && err.code === 'Assignment.NotScheduled') {
      const sessionId = typeof err.problem.sessionId === 'string' ? err.problem.sessionId : null;
      setStartedSessionId(sessionId);
      setFormError(
        sessionId
          ? 'The client already started this workout, so it can no longer be removed or skipped.'
          : 'This workout was already completed or skipped, so it can no longer be changed.',
      );
      invalidate();
      return;
    }
    setStartedSessionId(null);
    setFormError(applyProblem(err, { isField: (k) => k in editSchema.shape, setFieldError: (k, message) => setError(k as keyof EditValues, { type: 'server', message }) }).formError);
  };

  const update = useMutation({
    mutationFn: (v: EditValues) => assignmentsApi.update(a.id, { date: v.date, notes: v.notes || null }),
    onSuccess: (next) => {
      invalidate();
      toast.success(next.date === a.date ? 'Workout updated' : `Moved to ${formatDay(next.date, 'EEEE d MMMM')}`);
      onClose();
    },
    onError: fail,
  });
  const skip = useMutation({
    mutationFn: () => assignmentsApi.skip(a.id),
    onSuccess: () => {
      invalidate();
      toast.success('Marked as skipped');
      onClose();
    },
    onError: fail,
  });
  const remove = useMutation({
    mutationFn: () => assignmentsApi.remove(a.id),
    onSuccess: () => {
      invalidate();
      toast.success('Workout removed from the schedule');
      onClose();
    },
    onError: (err) => {
      setConfirmDelete(false);
      fail(err);
    },
  });

  return (
    <>
      <Dialog
        open
        onOpenChange={(o) => !o && onClose()}
        title={a.workout.name}
        description={`${showClient ? `${fullName(a.client)} · ` : ''}${formatDay(a.date, 'EEEE d MMMM yyyy')}${a.programName ? ` · ${a.programName}` : ''}`}
        footer={
          editable ? (
            <>
              <Button variant="ghost" className="text-danger sm:mr-auto" onClick={() => setConfirmDelete(true)} disabled={a.sessionId !== null} title={a.sessionId ? 'Started workouts stay in the history' : undefined}>
                <Trash2 aria-hidden />
                Delete
              </Button>
              <Button variant="secondary" onClick={() => skip.mutate()} loading={skip.isPending} disabled={a.sessionId !== null}>
                <CircleSlash aria-hidden />
                Mark skipped
              </Button>
              <Button type="submit" form="assignment-form" loading={update.isPending}>
                Save changes
              </Button>
            </>
          ) : a.sessionId ? (
            <Button asChild>
              <Link to="/sessions/$sessionId" params={{ sessionId: a.sessionId }}>
                View session
              </Link>
            </Button>
          ) : undefined
        }
      >
        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-2">
            <AssignmentStatusBadge status={a.status} inProgress={a.sessionId !== null} />
          </div>
          <FormError message={formError} />
          {startedSessionId && (
            <Link to="/sessions/$sessionId" params={{ sessionId: startedSessionId }} className="-mt-2 text-sm font-medium text-primary underline-offset-4 hover:underline">
              View the session
            </Link>
          )}
          {editable && (
            <form id="assignment-form" noValidate className="grid gap-4" onSubmit={(e) => void handleSubmit((v) => update.mutate(v))(e)}>
              <Field label="Date" error={formState.errors.date?.message}>
                <Input type="date" {...register('date')} />
              </Field>
              <Field label="Notes for the client" optional error={formState.errors.notes?.message}>
                <Textarea rows={2} {...register('notes')} />
              </Field>
            </form>
          )}
          {!editable && a.notes && <p className="text-sm">{a.notes}</p>}
          <div>
            <h3 className="mb-2 text-sm font-semibold">Exercises</h3>
            <ol className="flex flex-col divide-y divide-border rounded-lg border border-border">
              {a.workout.exercises.map((e) => (
                <li key={e.id} className="flex items-baseline justify-between gap-3 px-3 py-2 text-sm">
                  <span className="min-w-0 truncate">
                    {e.supersetGroup && <span className="mr-1.5 font-semibold text-primary">{e.supersetGroup}</span>}
                    {e.exercise.name}
                  </span>
                  <span className="tabular shrink-0 text-muted">
                    {e.sets} × {formatGoal(e.goal)}
                    {formatLoad(e.load, units) ? ` @ ${formatLoad(e.load, units)}` : ''}
                  </span>
                </li>
              ))}
            </ol>
          </div>
        </div>
      </Dialog>
      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete this workout?"
        description={`${a.workout.name} on ${formatDay(a.date, 'EEEE d MMMM')} will be removed from ${fullName(a.client)}'s schedule.`}
        confirmLabel="Delete"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
      />
    </>
  );
}
