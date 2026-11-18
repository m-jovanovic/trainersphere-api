import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, getRouteApi, useNavigate } from '@tanstack/react-router';
import { ArrowLeft, CalendarRange, Copy, Plus, X } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { programsApi } from '@/lib/api/endpoints';
import { errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { Program } from '@/lib/api/types';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { Input, Textarea } from '@/components/ui/input';
import { Menu, MenuItem, MenuLabel } from '@/components/ui/menu';
import { Card, Skeleton } from '@/components/ui/misc';
import { NumberStepper } from '@/components/ui/number-stepper';
import { UnsavedChangesGuard } from '@/components/unsaved-guard';
import { QueryError } from '@/components/layout/page-states';
import { AssignProgramDialog } from './assign-program-dialog';

const route = getRouteApi('/_authed/_shell/_trainer/programs/$programId');
const MAX_PER_DAY = 3;
const DAYS = [1, 2, 3, 4, 5, 6, 7];

interface Slot {
  key: string;
  week: number;
  day: number;
  workoutId: string;
  workoutName: string;
}
interface Draft {
  name: string;
  description: string;
  weeks: number;
  slots: Slot[];
}

let seq = 0;
const key = () => `slot-${++seq}`;

function toDraft(p: Program | undefined): Draft {
  if (!p) return { name: '', description: '', weeks: 4, slots: [] };
  return { name: p.name, description: p.description ?? '', weeks: p.weeks, slots: p.slots.map((s) => ({ key: key(), week: s.week, day: s.day, workoutId: s.workoutId, workoutName: s.workout.name })) };
}
const fingerprint = (d: Draft) => JSON.stringify({ ...d, slots: d.slots.map(({ week, day, workoutId }) => [week, day, workoutId]) });

export function ProgramEditorPage() {
  const { programId } = route.useParams();
  const isNew = programId === 'new';
  const existing = useQuery({ ...queries.program(programId), enabled: !isNew });
  if (!isNew && existing.error) return <QueryError error={existing.error} onRetry={() => void existing.refetch()} />;
  if (!isNew && !existing.data) return <Skeleton className="h-96" />;
  return <Editor key={existing.data?.id ?? 'new'} program={existing.data} />;
}

function Editor({ program }: { program: Program | undefined }) {
  const search = route.useSearch();
  const nav = route.useNavigate();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const workouts = useQuery(queries.workouts(''));
  const [saved, setSaved] = useState(() => toDraft(program));
  const [draft, setDraft] = useState(saved);
  const [errors, setErrors] = useState<{ name?: string; form?: string }>({});
  const dirty = fingerprint(draft) !== fingerprint(saved);
  const outOfRange = draft.slots.filter((s) => s.week > draft.weeks).length;

  const save = useMutation({
    mutationFn: () => {
      const body = {
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        weeks: draft.weeks,
        slots: draft.slots.filter((s) => s.week <= draft.weeks).sort((a, b) => a.week - b.week || a.day - b.day).map(({ week, day, workoutId }) => ({ week, day, workoutId })),
      };
      return program ? programsApi.update(program.id, body) : programsApi.create(body);
    },
    onSuccess: (p) => {
      qc.setQueryData(queries.program(p.id).queryKey, p);
      void qc.invalidateQueries({ queryKey: [...keys.programs, 'list'] });
      const next = toDraft(p);
      setSaved(next);
      setDraft(next);
      toast.success(program ? 'Program saved' : `${p.name} created`);
      // The guard may not have re-rendered with the saved state yet, so it would block this redirect.
      if (!program) void navigate({ to: '/programs/$programId', params: { programId: p.id }, replace: true, ignoreBlocker: true });
    },
    onError: (err) => setErrors({ form: errorMessage(err) }),
  });

  const submit = () => {
    const name = draft.name.trim();
    const nameError = !name ? 'Name the program.' : name.length > 100 ? 'Use at most 100 characters.' : undefined;
    if (nameError) {
      setErrors({ name: nameError });
      return;
    }
    setErrors({});
    save.mutate();
  };
  const patch = (p: Partial<Draft>) => setDraft((d) => ({ ...d, ...p }));
  const add = (week: number, day: number, w: { id: string; name: string }) => patch({ slots: [...draft.slots, { key: key(), week, day, workoutId: w.id, workoutName: w.name }] });
  const removeSlot = (k: string) => patch({ slots: draft.slots.filter((s) => s.key !== k) });
  const repeatWeek1 = () => {
    const week1 = draft.slots.filter((s) => s.week === 1);
    const rest = Array.from({ length: draft.weeks - 1 }, (_, i) => week1.map((s) => ({ ...s, key: key(), week: i + 2 }))).flat();
    patch({ slots: [...week1, ...rest] });
    toast.success(`Week 1 copied to weeks 2–${draft.weeks}`);
  };
  const total = draft.slots.filter((s) => s.week <= draft.weeks).length;
  const workoutList = workouts.data?.items ?? [];

  return (
    <>
      <UnsavedChangesGuard when={dirty && !save.isPending} />
      <Link to="/programs" className="mb-3 inline-flex min-h-9 items-center gap-1.5 text-sm text-muted hover:text-fg">
        <ArrowLeft className="size-4" aria-hidden />
        Programs
      </Link>
      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">{program ? 'Edit program' : 'New program'}</h1>
        <div className="flex flex-wrap items-center gap-2">
          {dirty && <span className="text-sm text-muted">Unsaved changes</span>}
          {program && (
            <Button variant="secondary" disabled={dirty} title={dirty ? 'Save first' : undefined} onClick={() => void nav({ search: { assign: true } })}>
              <CalendarRange aria-hidden />
              Assign
            </Button>
          )}
          <Button onClick={submit} loading={save.isPending}>
            {program ? 'Save program' : 'Create program'}
          </Button>
        </div>
      </div>
      <div className="flex flex-col gap-4">
        <FormError message={errors.form} />
        <Card className="grid gap-4 p-4 sm:grid-cols-[1fr_auto] sm:p-5">
          <Field label="Program name" error={errors.name}>
            <Input value={draft.name} onChange={(e) => patch({ name: e.target.value })} autoComplete="off" placeholder="e.g. Strength Base" />
          </Field>
          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-medium">Weeks</span>
            <NumberStepper label="Weeks" value={draft.weeks} onChange={(v) => patch({ weeks: v ?? 1 })} step={1} min={1} max={52} className="w-44" />
          </div>
          <Field label="Description" optional className="sm:col-span-2">
            <Textarea rows={2} value={draft.description} onChange={(e) => patch({ description: e.target.value })} />
          </Field>
        </Card>

        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-muted">
            <span className="font-medium text-fg">{total} workouts</span> across {draft.weeks} weeks. Day 1 is the weekday of the start date chosen when assigning.
          </p>
          {draft.weeks > 1 && draft.slots.some((s) => s.week === 1) && (
            <Button variant="secondary" size="sm" onClick={repeatWeek1}>
              <Copy aria-hidden />
              Repeat week 1
            </Button>
          )}
        </div>
        {outOfRange > 0 && (
          <p role="status" className="rounded-lg border border-border-strong bg-surface-2 px-3 py-2 text-sm">
            {outOfRange} workout{outOfRange === 1 ? '' : 's'} in weeks beyond {draft.weeks} will be dropped when you save.
          </p>
        )}

        <div className="flex flex-col gap-3">
          {Array.from({ length: draft.weeks }, (_, i) => i + 1).map((week) => (
            <Card key={week} className="p-3 sm:p-4" aria-labelledby={`week-${week}`}>
              <h2 id={`week-${week}`} className="mb-2 text-sm font-semibold">
                Week {week}
              </h2>
              <div className="grid gap-2 md:grid-cols-7">
                {DAYS.map((day) => {
                  const slots = draft.slots.filter((s) => s.week === week && s.day === day);
                  return (
                    <div key={day} className="flex min-w-0 items-start gap-2 rounded-lg bg-surface-2 p-2 md:min-h-24 md:flex-col" role="group" aria-label={`Week ${week}, day ${day}`}>
                      <span className="w-12 shrink-0 pt-1.5 text-xs font-medium text-muted md:w-auto md:pt-0">Day {day}</span>
                      <div className="flex min-w-0 flex-1 flex-wrap gap-1.5 md:w-full md:flex-col md:flex-nowrap">
                        {slots.map((s) => (
                          <span key={s.key} title={s.workoutName} className="flex min-h-8 min-w-0 items-center gap-1 rounded-md border border-border bg-surface py-0.5 pr-0.5 pl-2 text-xs font-medium">
                            <span className="line-clamp-2 min-w-0 flex-1 leading-tight break-words">{s.workoutName}</span>
                            <button type="button" aria-label={`Remove ${s.workoutName} from week ${week}, day ${day}`} onClick={() => removeSlot(s.key)} className="flex size-7 shrink-0 items-center justify-center rounded text-muted hover:bg-surface-2 hover:text-fg">
                              <X className="size-3.5" aria-hidden />
                            </button>
                          </span>
                        ))}
                        {slots.length < MAX_PER_DAY && (
                          <Menu
                            align="start"
                            trigger={
                              <button type="button" aria-label={`Add workout to week ${week}, day ${day}`} className="flex min-h-8 items-center justify-center gap-1 rounded-md border border-dashed border-border-strong px-2 text-xs text-muted hover:border-primary hover:text-primary">
                                <Plus className="size-3.5" aria-hidden />
                                <span className={slots.length ? 'sr-only' : undefined}>Add</span>
                              </button>
                            }
                          >
                            <MenuLabel>Add workout</MenuLabel>
                            {workoutList.length === 0 && <MenuLabel>No workouts yet</MenuLabel>}
                            {workoutList.map((w) => (
                              <MenuItem key={w.id} onSelect={() => add(week, day, w)}>
                                {w.name}
                              </MenuItem>
                            ))}
                          </Menu>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </Card>
          ))}
        </div>
      </div>
      {program && <AssignProgramDialog open={search.assign === true} onOpenChange={(o) => void nav({ search: { assign: o || undefined }, replace: true })} programId={program.id} />}
    </>
  );
}
