import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  closestCenter,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
} from '@dnd-kit/core';
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, getRouteApi, useNavigate } from '@tanstack/react-router';
import { ArrowDown, ArrowLeft, ArrowUp, ChevronDown, GripVertical, MoreHorizontal, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Controller, useFieldArray, useForm, useWatch, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { workoutsApi } from '@/lib/api/endpoints';
import { applyProblem } from '@/lib/api/form-errors';
import { keys, queries } from '@/lib/api/queries';
import type { Exercise, ExerciseRef, GoalType, LoadType, Units, UpsertWorkoutRequest, Workout } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatDuration, formatGoal, formatLoad, kgToUnits, round2, unitsToKg, weightStep } from '@/lib/domain';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { Input, NativeSelect, Textarea } from '@/components/ui/input';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Card, EmptyState, Skeleton } from '@/components/ui/misc';
import { NumberStepper } from '@/components/ui/number-stepper';
import { ExerciseArt } from '@/components/exercise-art';
import { UnsavedChangesGuard } from '@/components/unsaved-guard';
import { QueryError } from '@/components/layout/page-states';
import { ExercisePicker } from '@/features/exercises/exercise-picker';

const route = getRouteApi('/_authed/_shell/_trainer/workouts/$workoutId');

const goalLimits: Record<GoalType, { max: number; unit: string; label: string }> = {
  reps: { max: 100, unit: 'reps', label: 'Reps' },
  time: { max: 7200, unit: 's', label: 'Time' },
  distance: { max: 100000, unit: 'm', label: 'Distance' },
};
const loadLabels: Record<LoadType, string> = { none: 'No load', bodyweight: 'Bodyweight', weight: 'Weight', 'percent-1rm': '% of 1RM', rpe: 'RPE' };
const restPresets = [0, 30, 45, 60, 75, 90, 120, 150, 180, 240, 300];

const itemSchema = z
  .object({
    key: z.string(),
    exerciseId: z.string(),
    exercise: z.custom<ExerciseRef>(),
    sets: z.number({ error: 'Enter the number of sets.' }).int().min(1, 'At least 1 set.').max(20, 'At most 20 sets.'),
    goalType: z.enum(['reps', 'time', 'distance']),
    goalMin: z.number({ error: 'Enter a target.' }).int('Use whole numbers.').min(1, 'At least 1.'),
    goalMax: z.number({ error: 'Enter a target.' }).int('Use whole numbers.').min(1, 'At least 1.'),
    loadType: z.enum(['none', 'bodyweight', 'weight', 'percent-1rm', 'rpe']),
    loadValue: z.number().nullable(),
    restSeconds: z.number().int().min(0).max(1800),
    tempo: z.string().trim().regex(/^$|^[0-9X]-[0-9X]-[0-9X]-[0-9X]$/i, 'Use four digits or X, e.g. 3-1-X-0.'),
    supersetGroup: z.string(),
    notes: z.string().trim().max(500, 'Keep notes under 500 characters.'),
  })
  .superRefine((v, ctx) => {
    const lim = goalLimits[v.goalType];
    if (v.goalMax < v.goalMin) ctx.addIssue({ code: 'custom', path: ['goalMax'], message: 'Must be at least the minimum.' });
    if (v.goalMax > lim.max) ctx.addIssue({ code: 'custom', path: ['goalMax'], message: `At most ${lim.max}.` });
    const val = v.loadValue;
    if (v.loadType === 'weight' && val === null) ctx.addIssue({ code: 'custom', path: ['loadValue'], message: 'Enter a weight.' });
    if (v.loadType === 'percent-1rm' && (val === null || val < 1 || val > 120)) ctx.addIssue({ code: 'custom', path: ['loadValue'], message: 'Between 1 and 120%.' });
    if (v.loadType === 'rpe' && (val === null || val < 1 || val > 10 || (val * 2) % 1 !== 0)) ctx.addIssue({ code: 'custom', path: ['loadValue'], message: 'RPE 1–10 in steps of 0.5.' });
  });

const schema = z.object({
  name: z.string().trim().min(1, 'Name the workout.').max(100, 'Use at most 100 characters.'),
  description: z.string().trim().max(1000, 'Keep the description under 1000 characters.'),
  exercises: z.array(itemSchema).min(1, 'Add at least one exercise.').max(30, 'A workout can have at most 30 exercises.'),
});
type Values = z.infer<typeof schema>;
type Item = Values['exercises'][number];

let keySeq = 0;
const newKey = () => `item-${++keySeq}`;

function toValues(w: Workout | undefined, units: Units): Values {
  if (!w) return { name: '', description: '', exercises: [] };
  return {
    name: w.name,
    description: w.description ?? '',
    exercises: w.exercises.map((e) => ({
      key: newKey(),
      exerciseId: e.exerciseId,
      exercise: e.exercise,
      sets: e.sets,
      goalType: e.goal.type,
      goalMin: e.goal.min,
      goalMax: e.goal.max,
      loadType: e.load.type,
      loadValue: e.load.value === null ? null : e.load.type === 'weight' || e.load.type === 'bodyweight' ? round2(kgToUnits(e.load.value, units)) : e.load.value,
      restSeconds: e.restSeconds,
      tempo: e.tempo ?? '',
      supersetGroup: e.supersetGroup ?? '',
      notes: e.notes ?? '',
    })),
  };
}

function fromExercise(e: Exercise): Item {
  const timed = e.category === 'cardio' || e.movementPattern === 'cardio';
  const bodyweight = e.equipment.length === 0 || e.equipment.every((x) => x === 'bodyweight' || x === 'pull-up-bar' || x === 'mat');
  return {
    key: newKey(),
    exerciseId: e.id,
    exercise: { id: e.id, slug: e.slug, name: e.name, illustration: e.illustration },
    sets: timed ? 1 : 3,
    goalType: timed ? 'time' : 'reps',
    goalMin: timed ? 600 : 8,
    goalMax: timed ? 600 : 12,
    loadType: timed ? 'none' : bodyweight ? 'bodyweight' : 'weight',
    loadValue: timed || bodyweight ? null : 0,
    restSeconds: timed ? 0 : 90,
    tempo: '',
    supersetGroup: '',
    notes: '',
  };
}

function toRequest(v: Values, units: Units): UpsertWorkoutRequest {
  return {
    name: v.name.trim(),
    description: v.description.trim() || null,
    exercises: v.exercises.map((e) => ({
      exerciseId: e.exerciseId,
      sets: e.sets,
      goal: { type: e.goalType, min: e.goalMin, max: e.goalMax },
      load: {
        type: e.loadType,
        value: e.loadType === 'none' ? null : e.loadValue === null ? null : e.loadType === 'weight' || e.loadType === 'bodyweight' ? unitsToKg(e.loadValue, units) : e.loadValue,
      },
      restSeconds: e.restSeconds,
      tempo: e.tempo.trim() ? e.tempo.trim().toUpperCase() : null,
      supersetGroup: e.supersetGroup || null,
      notes: e.notes.trim() || null,
    })),
  };
}

/** API error paths (`exercises.0.goal.min`) to form paths (`exercises.0.goalMin`). */
const FORM_ITEM_FIELDS = new Set(['exerciseId', 'sets', 'goalType', 'goalMin', 'goalMax', 'loadType', 'loadValue', 'restSeconds', 'tempo', 'supersetGroup', 'notes']);

function toFormPath(path: string): string {
  return path
    .replace(/\.goal\.(min|max)$/, (_, m: string) => `.goal${m === 'min' ? 'Min' : 'Max'}`)
    .replace(/\.goal\.type$/, '.goalType')
    .replace(/\.load\.value$/, '.loadValue')
    .replace(/\.load\.type$/, '.loadType')
    .replace(/\.exerciseId$/, '.exerciseId');
}

export function WorkoutBuilderPage() {
  const { workoutId } = route.useParams();
  const isNew = workoutId === 'new';
  const existing = useQuery({ ...queries.workout(workoutId), enabled: !isNew });
  if (!isNew && existing.error) return <QueryError error={existing.error} onRetry={() => void existing.refetch()} />;
  if (!isNew && !existing.data) return <Skeleton className="h-96" />;
  return <Builder key={existing.data?.id ?? 'new'} workout={existing.data} />;
}

function Builder({ workout }: { workout: Workout | undefined }) {
  const units = useUnits();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [pickerOpen, setPickerOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: toValues(workout, units) });
  const { control, register, handleSubmit, formState, reset, setError } = form;
  const items = useFieldArray({ control, name: 'exercises', keyName: 'fieldKey' });
  const watched = useWatch({ control, name: 'exercises' });

  const save = useMutation({
    mutationFn: (v: Values) => (workout ? workoutsApi.update(workout.id, toRequest(v, units)) : workoutsApi.create(toRequest(v, units))),
    onSuccess: (saved) => {
      qc.setQueryData(queries.workout(saved.id).queryKey, saved);
      void qc.invalidateQueries({ queryKey: [...keys.workouts, 'list'] });
      reset(toValues(saved, units));
      setExpanded(new Set());
      toast.success(workout ? 'Workout saved' : `${saved.name} created`);
      // The guard may not have re-rendered with the reset form yet, so it would block this redirect.
      if (!workout) void navigate({ to: '/workouts/$workoutId', params: { workoutId: saved.id }, replace: true, ignoreBlocker: true });
    },
    onError: (err) => {
      const count = form.getValues('exercises').length;
      const { formError, fields } = applyProblem(err, {
        mapPath: toFormPath,
        isField: (path) => {
          const m = /^exercises\.(\d+)\.(\w+)$/.exec(path);
          if (m) return Number(m[1]) < count && FORM_ITEM_FIELDS.has(m[2] ?? '');
          return path === 'name' || path === 'description' || path === 'exercises';
        },
        setFieldError: (path, message) => setError(path as 'name', { type: 'server', message }),
      });
      const keysWithErrors = new Set<string>();
      for (const path of fields) {
        const m = /^exercises\.(\d+)/.exec(path);
        const key = m ? form.getValues(`exercises.${Number(m[1])}.key`) : undefined;
        if (key) keysWithErrors.add(key);
      }
      if (keysWithErrors.size) setExpanded((s) => new Set([...s, ...keysWithErrors]));
      setFormError(formError ?? (fields.length ? 'Some fields need attention.' : null));
    },
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 150, tolerance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const nameOf = (key: string | number) => watched.find((w) => w.key === key)?.exercise.name ?? 'exercise';
  const positionOf = (key: string | number) => watched.findIndex((w) => w.key === key) + 1;
  const announcements: Announcements = {
    onDragStart: ({ active }) => `Picked up ${nameOf(active.id)}, position ${positionOf(active.id)} of ${watched.length}.`,
    onDragOver: ({ active, over }) => (over ? `${nameOf(active.id)} moved to position ${positionOf(over.id)}.` : undefined),
    onDragEnd: ({ active, over }) => (over ? `${nameOf(active.id)} dropped at position ${positionOf(over.id)}.` : `${nameOf(active.id)} dropped.`),
    onDragCancel: ({ active }) => `Reordering cancelled. ${nameOf(active.id)} returned to its place.`,
  };
  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over || active.id === over.id) return;
    const from = watched.findIndex((w) => w.key === active.id);
    const to = watched.findIndex((w) => w.key === over.id);
    if (from >= 0 && to >= 0) items.move(from, to);
  };
  const toggle = (key: string) =>
    setExpanded((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  const listError = formState.errors.exercises?.root?.message ?? formState.errors.exercises?.message;

  return (
    <form
      noValidate
      onSubmit={(e) =>
        void handleSubmit(
          (v) => {
            setFormError(null);
            save.mutate(v);
          },
          (errors) => {
            const bad = (errors.exercises ?? []) as FieldErrors<Item>[];
            const withErrors = watched.filter((_, i) => bad[i]).map((w) => w.key);
            if (withErrors.length) setExpanded((s) => new Set([...s, ...withErrors]));
          },
        )(e)
      }
    >
      <UnsavedChangesGuard when={formState.isDirty && !save.isPending} />
      <Link to="/workouts" className="mb-3 inline-flex min-h-9 items-center gap-1.5 text-sm text-muted hover:text-fg">
        <ArrowLeft className="size-4" aria-hidden />
        Workouts
      </Link>
      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">{workout ? 'Edit workout' : 'New workout'}</h1>
        <div className="flex items-center gap-2">
          {formState.isDirty && <span className="text-sm text-muted">Unsaved changes</span>}
          <Button type="submit" loading={save.isPending}>
            {workout ? 'Save workout' : 'Create workout'}
          </Button>
        </div>
      </div>
      <div className="flex flex-col gap-4">
        <FormError message={formError} />
        <Card className="grid gap-4 p-4 sm:p-5">
          <Field label="Workout name" error={formState.errors.name?.message}>
            <Input autoComplete="off" placeholder="e.g. Lower A" {...register('name')} />
          </Field>
          <Field label="Description" optional error={formState.errors.description?.message}>
            <Textarea rows={2} placeholder="Focus, intent or instructions for the client" {...register('description')} />
          </Field>
        </Card>

        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">
            Exercises <span className="font-normal text-muted">({items.fields.length})</span>
          </h2>
          <Button variant="secondary" onClick={() => setPickerOpen(true)}>
            <Plus aria-hidden />
            Add exercises
          </Button>
        </div>
        {listError && (
          <p role="alert" className="text-sm text-danger">
            {listError}
          </p>
        )}
        {items.fields.length === 0 ? (
          <Card>
            <EmptyState title="No exercises yet" description="Add exercises from the library, then set sets, reps, load and rest." action={<Button onClick={() => setPickerOpen(true)}>Add exercises</Button>} />
          </Card>
        ) : (
          <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd} accessibility={{ announcements, screenReaderInstructions: { draggable: 'To reorder, press space or enter on the handle, use the arrow keys to move, then space or enter to drop. Escape cancels.' } }}>
            <SortableContext items={items.fields.map((f) => f.key)} strategy={verticalListSortingStrategy}>
              <ol aria-label="Workout exercises" className="flex flex-col gap-2">
                {items.fields.map((f, index) => (
                  <SortableItem
                    key={f.key}
                    id={f.key}
                    index={index}
                    count={items.fields.length}
                    control={control}
                    register={register}
                    errors={formState.errors.exercises?.[index]}
                    units={units}
                    expanded={expanded.has(f.key)}
                    onToggle={() => toggle(f.key)}
                    onMove={(to) => items.move(index, to)}
                    onRemove={() => items.remove(index)}
                    prevGroup={watched[index - 1]?.supersetGroup ?? ''}
                    nextGroup={watched[index + 1]?.supersetGroup ?? ''}
                  />
                ))}
              </ol>
            </SortableContext>
          </DndContext>
        )}
        {items.fields.length > 0 && (
          <Button variant="outline" className="self-start" onClick={() => setPickerOpen(true)}>
            <Plus aria-hidden />
            Add more
          </Button>
        )}
      </div>
      <ExercisePicker
        open={pickerOpen}
        onOpenChange={setPickerOpen}
        onAdd={(added) => {
          const next = added.map(fromExercise);
          items.append(next);
          setExpanded((s) => new Set([...s, ...next.map((n) => n.key)]));
        }}
      />
    </form>
  );
}

interface ItemProps {
  id: string;
  index: number;
  count: number;
  control: Control<Values>;
  register: UseFormRegister<Values>;
  errors: FieldErrors<Item> | undefined;
  units: Units;
  expanded: boolean;
  onToggle: () => void;
  onMove: (to: number) => void;
  onRemove: () => void;
  prevGroup: string;
  nextGroup: string;
}

function SortableItem({ id, index, count, control, register, errors, units, expanded, onToggle, onMove, onRemove, prevGroup, nextGroup }: ItemProps) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({ id });
  const item = useWatch({ control, name: `exercises.${index}` });
  const name = item.exercise.name;
  const group = item.supersetGroup;
  const inSuperset = group !== '' && (group === prevGroup || group === nextGroup);
  const lim = goalLimits[item.goalType];
  const load = formatLoad({ type: item.loadType, value: item.loadValue === null ? null : item.loadType === 'weight' || item.loadType === 'bodyweight' ? unitsToKg(item.loadValue, units) : item.loadValue }, units);
  const summary = `${item.sets} × ${formatGoal({ type: item.goalType, min: item.goalMin, max: item.goalMax })}${load ? ` @ ${load}` : ''} · rest ${formatDuration(item.restSeconds)}`;
  const hasErrors = errors !== undefined && Object.keys(errors).length > 0;
  const detailsId = `${id}-details`;
  const p = `exercises.${index}` as const;

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      aria-label={`${index + 1}. ${name}`}
      className={cn('rounded-card border bg-surface', isDragging ? 'z-10 border-primary shadow-lg' : hasErrors ? 'border-danger' : 'border-border', inSuperset && 'border-l-4 border-l-primary')}
    >
      <div className="flex items-center gap-2 p-2 pr-3">
        <button
          type="button"
          ref={setActivatorNodeRef}
          {...attributes}
          {...listeners}
          aria-label={`Reorder ${name}`}
          className="flex size-11 shrink-0 cursor-grab touch-none items-center justify-center rounded-lg text-muted hover:bg-surface-2 active:cursor-grabbing"
        >
          <GripVertical className="size-5" aria-hidden />
        </button>
        <ExerciseArt illustration={item.exercise.illustration} name={name} variant="sm" className="size-11 shrink-0" />
        <button type="button" onClick={onToggle} aria-expanded={expanded} aria-controls={detailsId} className="flex min-h-11 min-w-0 flex-1 items-center gap-2 text-left">
          <span className="min-w-0 flex-1">
            <span className="flex items-center gap-2">
              <span className="tabular text-sm text-muted">{index + 1}</span>
              {group && <span className="rounded bg-primary-soft px-1.5 text-xs font-semibold text-primary-soft-fg">{group}</span>}
              <span className="line-clamp-2 min-w-0 leading-snug font-medium">{name}</span>
            </span>
            <span className={cn('block truncate text-sm', hasErrors ? 'text-danger' : 'text-muted')}>{hasErrors ? 'Fix the highlighted fields' : summary}</span>
          </span>
          <ChevronDown className={cn('size-4 shrink-0 text-muted transition-transform', expanded && 'rotate-180')} aria-hidden />
        </button>
        <Menu
          trigger={
            <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${name}`}>
              <MoreHorizontal />
            </Button>
          }
        >
          {index > 0 && (
            <MenuItem icon={<ArrowUp />} onSelect={() => onMove(index - 1)}>
              Move up
            </MenuItem>
          )}
          {index < count - 1 && (
            <MenuItem icon={<ArrowDown />} onSelect={() => onMove(index + 1)}>
              Move down
            </MenuItem>
          )}
          <MenuItem icon={<Trash2 />} tone="danger" onSelect={onRemove}>
            Remove
          </MenuItem>
        </Menu>
      </div>
      <div id={detailsId} hidden={!expanded} className="grid gap-4 border-t border-border p-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-1.5">
          <span className="text-sm font-medium">Sets</span>
          <Controller control={control} name={`${p}.sets`} render={({ field }) => <NumberStepper label={`Sets for ${name}`} value={field.value} onChange={(v) => field.onChange(v ?? 1)} step={1} min={1} max={20} />} />
          {errors?.sets && <p className="text-sm text-danger" role="alert">{errors.sets.message}</p>}
        </div>
        <Field label="Target" error={errors?.goalType?.message}>
          <NativeSelect aria-label={`Target type for ${name}`} {...register(`${p}.goalType`)}>
            <option value="reps">Reps</option>
            <option value="time">Time (seconds)</option>
            <option value="distance">Distance (meters)</option>
          </NativeSelect>
        </Field>
        <div className="flex flex-col gap-1.5 sm:col-span-2">
          <span className="text-sm font-medium" id={`${id}-range`}>
            {lim.label} range <span className="font-normal text-muted">(min – max {lim.unit})</span>
          </span>
          <div className="flex items-center gap-2" role="group" aria-labelledby={`${id}-range`}>
            <Input type="number" inputMode="numeric" min={1} aria-label={`Minimum ${lim.label.toLowerCase()} for ${name}`} aria-invalid={errors?.goalMin ? true : undefined} {...register(`${p}.goalMin`, { valueAsNumber: true })} />
            <span aria-hidden className="text-muted">–</span>
            <Input type="number" inputMode="numeric" min={1} aria-label={`Maximum ${lim.label.toLowerCase()} for ${name}`} aria-invalid={errors?.goalMax ? true : undefined} {...register(`${p}.goalMax`, { valueAsNumber: true })} />
          </div>
          {(errors?.goalMin ?? errors?.goalMax) && <p className="text-sm text-danger" role="alert">{(errors.goalMin ?? errors.goalMax)?.message}</p>}
        </div>
        <Field label="Load" error={errors?.loadType?.message}>
          <NativeSelect aria-label={`Load type for ${name}`} {...register(`${p}.loadType`)}>
            {(Object.keys(loadLabels) as LoadType[]).map((t) => (
              <option key={t} value={t}>
                {loadLabels[t]}
              </option>
            ))}
          </NativeSelect>
        </Field>
        {item.loadType !== 'none' && (
          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-medium">
              {item.loadType === 'weight' ? `Weight (${units})` : item.loadType === 'bodyweight' ? `Added weight (${units})` : item.loadType === 'rpe' ? 'Target RPE' : 'Percent of 1RM'}
            </span>
            <Controller
              control={control}
              name={`${p}.loadValue`}
              render={({ field }) => (
                <NumberStepper
                  label={`${loadLabels[item.loadType]} for ${name}`}
                  value={field.value}
                  onChange={field.onChange}
                  step={item.loadType === 'rpe' ? 0.5 : item.loadType === 'percent-1rm' ? 5 : weightStep(units)}
                  min={0}
                  max={item.loadType === 'rpe' ? 10 : item.loadType === 'percent-1rm' ? 120 : 2000}
                  suffix={item.loadType === 'percent-1rm' ? '%' : item.loadType === 'rpe' ? undefined : units}
                />
              )}
            />
            {errors?.loadValue && <p className="text-sm text-danger" role="alert">{errors.loadValue.message}</p>}
          </div>
        )}
        <Field label="Rest" error={errors?.restSeconds?.message}>
          <Controller
            control={control}
            name={`${p}.restSeconds`}
            render={({ field }) => (
              <NativeSelect aria-label={`Rest for ${name}`} value={String(field.value)} onChange={(e) => field.onChange(Number(e.target.value))}>
                {[...new Set([...restPresets, field.value])].sort((a, b) => a - b).map((s) => (
                  <option key={s} value={s}>
                    {s === 0 ? 'No rest' : formatDuration(s)}
                  </option>
                ))}
              </NativeSelect>
            )}
          />
        </Field>
        <Field label="Tempo" optional error={errors?.tempo?.message}>
          <Input placeholder="3-1-X-0" autoComplete="off" {...register(`${p}.tempo`)} />
        </Field>
        <Field label="Superset" hint="Same letter on neighbours" error={errors?.supersetGroup?.message}>
          <NativeSelect {...register(`${p}.supersetGroup`)}>
            <option value="">None</option>
            {['A', 'B', 'C', 'D', 'E', 'F'].map((g) => (
              <option key={g} value={g}>
                {g}
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field label="Notes" optional error={errors?.notes?.message} className="sm:col-span-2 lg:col-span-4">
          <Input autoComplete="off" placeholder="Shown to the client while logging" {...register(`${p}.notes`)} />
        </Field>
      </div>
    </li>
  );
}
