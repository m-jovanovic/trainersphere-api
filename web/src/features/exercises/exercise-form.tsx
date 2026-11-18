import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { exercisesApi } from '@/lib/api/endpoints';
import { ApiError, errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { Exercise } from '@/lib/api/types';
import { humanize } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { ChipGroup } from '@/components/ui/chips';
import { Dialog } from '@/components/ui/dialog';
import { Field, FormError } from '@/components/ui/field';
import { Input, NativeSelect, Textarea } from '@/components/ui/input';

const schema = z.object({
  name: z.string().trim().min(1, 'Name the exercise.').max(100, 'Use at most 100 characters.'),
  category: z.string().min(1, 'Choose a category.'),
  movementPattern: z.string().min(1, 'Choose a movement pattern.'),
  primaryMuscles: z.array(z.string()).min(1, 'Pick at least one primary muscle.'),
  secondaryMuscles: z.array(z.string()),
  equipment: z.array(z.string()),
  unilateral: z.boolean(),
  cue: z.string().trim().max(1000, 'Keep the cue under 1000 characters.'),
});
type Values = z.infer<typeof schema>;

const toOptions = (xs: string[]) => xs.map((x) => ({ value: x, label: humanize(x) }));

export function ExerciseFormDialog({ exercise, onClose, onSaved }: { exercise: Exercise | null; onClose: () => void; onSaved?: (e: Exercise) => void }) {
  const taxonomy = useQuery(queries.taxonomy());
  const qc = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, formState, control, setError } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: exercise
      ? { ...exercise, cue: exercise.cue ?? '' }
      : { name: '', category: 'strength', movementPattern: '', primaryMuscles: [], secondaryMuscles: [], equipment: [], unilateral: false, cue: '' },
  });
  const primary = useWatch({ control, name: 'primaryMuscles' });
  const t = taxonomy.data;

  const save = useMutation({
    mutationFn: (v: Values) => {
      const body = { ...v, secondaryMuscles: v.secondaryMuscles.filter((m) => !v.primaryMuscles.includes(m)), cue: v.cue || null };
      return exercise ? exercisesApi.update(exercise.id, body) : exercisesApi.create(body);
    },
    onSuccess: (saved) => {
      void qc.invalidateQueries({ queryKey: keys.exercises });
      qc.setQueryData(queries.exercise(saved.id).queryKey, saved);
      toast.success(exercise ? 'Exercise updated' : `${saved.name} added to your library`);
      onSaved?.(saved);
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiError && err.code === 'Exercise.NameInUse') {
        setError('name', { type: 'server', message: errorMessage(err) });
        return;
      }
      if (err instanceof ApiError) {
        const entries = Object.entries(err.fieldErrors).filter(([k]) => k in schema.shape);
        for (const [k, message] of entries) setError(k as keyof Values, { type: 'server', message });
        if (entries.length) return;
      }
      setFormError(errorMessage(err));
    },
  });

  return (
    <Dialog
      open
      variant="sheet"
      onOpenChange={(o) => !o && onClose()}
      title={exercise ? `Edit ${exercise.name}` : 'New exercise'}
      description={exercise ? undefined : 'Custom exercises are visible to you and your clients.'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="exercise-form" loading={save.isPending}>
            {exercise ? 'Save changes' : 'Create exercise'}
          </Button>
        </>
      }
    >
      <form id="exercise-form" noValidate className="flex flex-col gap-5" onSubmit={(e) => void handleSubmit((v) => save.mutate(v))(e)}>
        <FormError message={formError} />
        <Field label="Name" error={formState.errors.name?.message}>
          <Input autoComplete="off" {...register('name')} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Category" error={formState.errors.category?.message}>
            <NativeSelect {...register('category')}>
              {(t?.categories ?? ['strength']).map((c) => (
                <option key={c} value={c}>
                  {humanize(c)}
                </option>
              ))}
            </NativeSelect>
          </Field>
          <Field label="Movement pattern" error={formState.errors.movementPattern?.message}>
            <NativeSelect {...register('movementPattern')}>
              <option value="">Choose…</option>
              {(t?.movementPatterns ?? []).map((p) => (
                <option key={p} value={p}>
                  {humanize(p)}
                </option>
              ))}
            </NativeSelect>
          </Field>
        </div>
        <fieldset aria-describedby={formState.errors.primaryMuscles ? 'primary-error' : undefined}>
          <legend className="mb-2 text-sm font-medium">Primary muscles</legend>
          <Controller control={control} name="primaryMuscles" render={({ field }) => <ChipGroup label="Primary muscles" value={field.value} onChange={field.onChange} options={toOptions(t?.muscles ?? [])} />} />
          {formState.errors.primaryMuscles && (
            <p id="primary-error" role="alert" className="mt-1.5 text-sm text-danger">
              {formState.errors.primaryMuscles.message}
            </p>
          )}
        </fieldset>
        <fieldset>
          <legend className="mb-2 text-sm font-medium">
            Secondary muscles <span className="font-normal text-muted">(optional)</span>
          </legend>
          <Controller
            control={control}
            name="secondaryMuscles"
            render={({ field }) => <ChipGroup label="Secondary muscles" value={field.value} onChange={field.onChange} options={toOptions((t?.muscles ?? []).filter((m) => !primary.includes(m)))} />}
          />
        </fieldset>
        <fieldset>
          <legend className="mb-2 text-sm font-medium">
            Equipment <span className="font-normal text-muted">(optional)</span>
          </legend>
          <Controller control={control} name="equipment" render={({ field }) => <ChipGroup label="Equipment" value={field.value} onChange={field.onChange} options={toOptions(t?.equipment ?? [])} />} />
        </fieldset>
        <label className="flex min-h-11 items-center gap-3 text-sm">
          <input type="checkbox" className="size-5 accent-[var(--primary)]" {...register('unilateral')} />
          Unilateral (one side at a time)
        </label>
        <Field label="Coaching cue" optional error={formState.errors.cue?.message}>
          <Textarea rows={3} placeholder="One or two sentences the client sees while logging." {...register('cue')} />
        </Field>
      </form>
    </Dialog>
  );
}
