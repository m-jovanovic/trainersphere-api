import { useQueryClient } from '@tanstack/react-query';
import { Check, MoreHorizontal, RotateCcw, Trash2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import type { SessionExercise, SessionSet, Units } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatDuration, formatWeight, kgToUnits, unitsToKg, weightStep } from '@/lib/domain';
import { cn, uuid } from '@/lib/utils';
import { NumberStepper } from '@/components/ui/number-stepper';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Button } from '@/components/ui/button';
import { draftOf, draftValue, metricsFor, prefill, primaryMetric, withValue, type Metric, type SetDraft } from './logging';
import { runSessionWrite, sessionKeys, type DeleteSetVars, type PutSetVars } from './offline';

const EDIT_DEBOUNCE_MS = 600;
const rpeOptions = [6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10];
const ignore = () => undefined;

interface SetRowProps {
  sessionId: string;
  exercise: SessionExercise;
  setNumber: number;
  set: SessionSet | undefined;
  lastHint: string | null;
  expanded: boolean;
  onExpand: () => void;
  /** Rows past the plan can be removed; planned rows can only be cleared. */
  removable: boolean;
  onRemoveRow: () => void;
  onCompleted: () => void;
}

export function SetRow({ sessionId, exercise: e, setNumber, set, lastHint, expanded, onExpand, removable, onRemoveRow, onCompleted }: SetRowProps) {
  const qc = useQueryClient();
  const units = useUnits();
  const metrics = metricsFor(e.prescription);
  const main = primaryMetric(e.prescription);
  // The row owns its values while mounted, so slow or queued writes never overwrite typing.
  const [draft, setDraft] = useState<SetDraft>(() => (set ? draftOf(set) : prefill(e, setNumber)));
  const [ownId] = useState(() => set?.id ?? uuid());
  const id = set?.id ?? ownId;
  const completed = set?.completed ?? false;
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pending = useRef<(() => void) | null>(null);

  // Switching exercise unmounts the row; send an edit that is still waiting for its debounce.
  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
      pending.current?.();
    },
    [],
  );

  const write = (d: SetDraft, done: boolean) => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    pending.current = null;
    const vars: PutSetVars = { sessionId, setId: id, body: { exerciseId: e.exerciseId, position: e.position, setNumber, ...d, completed: done, notes: set?.notes ?? null } };
    runSessionWrite(qc, sessionKeys.putSet, sessionId, vars).catch(ignore);
  };

  const edit = (next: SetDraft) => {
    setDraft(next);
    // Unlogged rows stay on the device until ticked; logged ones save as you adjust.
    if (!set) return;
    if (timer.current) clearTimeout(timer.current);
    pending.current = () => write(next, completed);
    timer.current = setTimeout(() => pending.current?.(), EDIT_DEBOUNCE_MS);
  };

  const clear = () => {
    if (timer.current) clearTimeout(timer.current);
    pending.current = null;
    if (set) runSessionWrite(qc, sessionKeys.deleteSet, sessionId, { sessionId, setId: set.id } satisfies DeleteSetVars).catch(ignore);
    setDraft(prefill({ ...e, sets: e.sets.filter((s) => s.id !== set?.id) }, setNumber));
  };

  const toggleDone = () => {
    write(draft, !completed);
    if (!completed) onCompleted();
  };

  const missing = draftValue(draft, main) === null;
  const label = `Set ${setNumber}`;
  const summary = describe(draft, metrics, units);

  const doneButton = (big: boolean) => (
    <button
      type="button"
      aria-pressed={completed}
      aria-label={`${label} done`}
      disabled={missing && !completed}
      onClick={toggleDone}
      className={cn(
        'flex shrink-0 items-center justify-center gap-2 rounded-full border-2 font-semibold transition-colors disabled:opacity-40',
        big ? 'h-13 flex-1 text-base' : 'size-12',
        completed ? 'border-primary bg-primary text-primary-fg' : 'border-border-strong text-muted hover:border-primary hover:text-primary',
        big && !completed && 'border-primary bg-primary text-primary-fg hover:text-primary-fg',
      )}
    >
      <Check className={big ? 'size-5' : 'size-6'} strokeWidth={3} aria-hidden />
      {big && (completed ? 'Done' : 'Log set')}
    </button>
  );

  if (!expanded) {
    return (
      <li className={cn('flex items-center gap-2 rounded-xl border py-1.5 pr-1.5 pl-1 transition-colors', completed ? 'border-primary/40 bg-primary-soft/50' : 'border-border bg-surface')}>
        <button type="button" onClick={onExpand} aria-label={`Edit ${label.toLowerCase()}: ${summary}${completed ? ', done' : ''}`} className="flex min-h-12 min-w-0 flex-1 items-center gap-3 rounded-lg px-2 text-left">
          <SetBadge n={setNumber} done={completed} />
          <span className="min-w-0 flex-1">
            <span className={cn('tabular block truncate font-medium', !completed && !set && 'text-muted')}>{summary}</span>
            {lastHint && !completed && <span className="block truncate text-xs text-muted">Last time {lastHint}</span>}
            {draft.rpe !== null && completed && <span className="block text-xs text-muted">RPE {draft.rpe}</span>}
          </span>
        </button>
        {doneButton(false)}
      </li>
    );
  }

  return (
    <li aria-label={label} className={cn('rounded-xl border-2 p-3', completed ? 'border-primary/50 bg-primary-soft/50' : 'border-primary/60 bg-surface')}>
      <div className="mb-2 flex items-center gap-2">
        <SetBadge n={setNumber} done={completed} />
        <span className="min-w-0 flex-1">
          <span className="block text-sm font-semibold">{label}</span>
          {lastHint && <span className="block truncate text-xs text-muted">Last time {lastHint}</span>}
        </span>
        {(set !== undefined || removable) && (
          <Menu
            trigger={
              <Button variant="ghost" size="icon-sm" aria-label={`${label} options`}>
                <MoreHorizontal />
              </Button>
            }
          >
            {set && (
              <MenuItem icon={<RotateCcw />} onSelect={clear}>
                Clear set
              </MenuItem>
            )}
            {removable && (
              <MenuItem
                icon={<Trash2 />}
                tone="danger"
                onSelect={() => {
                  clear();
                  onRemoveRow();
                }}
              >
                Remove set
              </MenuItem>
            )}
          </Menu>
        )}
      </div>
      <div className={cn('grid gap-3', metrics.length === 2 ? 'grid-cols-2' : 'grid-cols-1')}>
        {metrics.map((m) => (
          <MetricInput key={m} metric={m} label={label} draft={draft} units={units} size={metrics.length === 2 ? 'md' : 'lg'} onChange={(v) => edit(withValue(draft, m, v))} />
        ))}
      </div>
      <div className="mt-3 flex items-center gap-2">
        <select
          aria-label={`${label} RPE`}
          value={draft.rpe ?? ''}
          onChange={(ev) => edit({ ...draft, rpe: ev.target.value === '' ? null : Number(ev.target.value) })}
          className="h-13 w-28 shrink-0 rounded-lg border border-border-strong bg-surface px-2 text-sm text-fg"
        >
          <option value="">RPE –</option>
          {rpeOptions.map((r) => (
            <option key={r} value={r}>
              RPE {r}
            </option>
          ))}
        </select>
        {doneButton(true)}
      </div>
    </li>
  );
}

function SetBadge({ n, done }: { n: number; done: boolean }) {
  return (
    <span className={cn('tabular flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold', done ? 'bg-primary text-primary-fg' : 'bg-surface-2')} aria-hidden>
      {n}
    </span>
  );
}

function describe(d: SetDraft, metrics: Metric[], units: Units): string {
  const parts = metrics.map((m) => {
    const v = draftValue(d, m);
    if (v === null) return m === 'weight' ? `– ${units}` : `– ${m === 'reps' ? 'reps' : m === 'duration' ? 's' : 'm'}`;
    if (m === 'weight') return formatWeight(v, units);
    if (m === 'reps') return `${v} reps`;
    if (m === 'duration') return formatDuration(v);
    return `${v} m`;
  });
  return parts.join(' × ');
}

const captions: Record<Metric, (u: Units) => string> = { weight: (u) => u, reps: () => 'reps', duration: () => 'seconds', distance: () => 'metres' };

function MetricInput({ metric, label, draft, units, size, onChange }: { metric: Metric; label: string; draft: SetDraft; units: Units; size: 'md' | 'lg'; onChange: (v: number | null) => void }) {
  const value = draftValue(draft, metric);
  const caption = captions[metric](units);
  const whole = (v: number | null) => onChange(v === null ? null : Math.round(v));
  const stepper = (() => {
    switch (metric) {
      case 'weight':
        return (
          <NumberStepper
            label={`${label} weight (${units})`}
            value={value === null ? null : Number(formatWeight(value, units, false))}
            onChange={(v) => onChange(v === null ? null : unitsToKg(v, units))}
            step={weightStep(units)}
            max={Math.round(kgToUnits(1000, units))}
            size={size}
          />
        );
      case 'reps':
        return <NumberStepper label={`${label} reps`} value={value} onChange={whole} step={1} max={1000} size={size} />;
      case 'duration':
        return <NumberStepper label={`${label} seconds`} value={value} onChange={whole} step={5} max={86_400} size={size} />;
      case 'distance':
        return <NumberStepper label={`${label} metres`} value={value} onChange={whole} step={10} max={1_000_000} size={size} />;
    }
  })();
  return (
    <div className="min-w-0">
      <span className="mb-1 block text-center text-xs font-medium tracking-wide text-muted uppercase" aria-hidden>
        {caption}
      </span>
      {stepper}
    </div>
  );
}
