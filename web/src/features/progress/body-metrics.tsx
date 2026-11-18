import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MoreHorizontal, Pencil, Plus, Scale, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { toast } from 'sonner';
import { z } from 'zod';
import { clientsApi } from '@/lib/api/endpoints';
import { ApiError, errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { BodyMetric, BodyMetricValues, Units } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatDay, todayIso } from '@/lib/dates';
import { kgToUnits, round2, unitsToKg } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Segmented } from '@/components/ui/chips';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { Field, FormError } from '@/components/ui/field';
import { Input, Textarea } from '@/components/ui/input';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Card, CardBody, CardHeader, EmptyState, Skeleton, Stat } from '@/components/ui/misc';
import { ChartFrame } from '@/components/chart';
import { chartTheme } from '@/components/chart-theme';
import { QueryError } from '@/components/layout/page-states';

type MeasureKey = Exclude<keyof BodyMetricValues, 'notes'>;

const measures: { key: MeasureKey; label: string; unit: 'weight' | 'cm' | '%' | 'bpm'; min: number; max: number }[] = [
  { key: 'weightKg', label: 'Body weight', unit: 'weight', min: 20, max: 400 },
  { key: 'bodyFatPercent', label: 'Body fat', unit: '%', min: 2, max: 75 },
  { key: 'waistCm', label: 'Waist', unit: 'cm', min: 10, max: 300 },
  { key: 'chestCm', label: 'Chest', unit: 'cm', min: 10, max: 300 },
  { key: 'hipsCm', label: 'Hips', unit: 'cm', min: 10, max: 300 },
  { key: 'armCm', label: 'Arm', unit: 'cm', min: 10, max: 300 },
  { key: 'thighCm', label: 'Thigh', unit: 'cm', min: 10, max: 300 },
  { key: 'restingHeartRate', label: 'Resting heart rate', unit: 'bpm', min: 25, max: 250 },
];

type Measure = (typeof measures)[number];
const measureByKey = Object.fromEntries(measures.map((m) => [m.key, m])) as Record<MeasureKey, Measure>;

const unitLabel = (u: (typeof measures)[number]['unit'], units: Units) => (u === 'weight' ? units : u);

function display(m: BodyMetric, key: MeasureKey, units: Units): number | null {
  const v = m[key];
  if (v === null) return null;
  return key === 'weightKg' ? round2(kgToUnits(v, units)) : v;
}

function fmt(v: number | null, unit: string) {
  return v === null ? '–' : `${Number(v.toFixed(1))} ${unit}`;
}

export function BodyMetricsCard({ clientId }: { clientId: string }) {
  const units = useUnits();
  const { data, error, refetch } = useQuery(queries.bodyMetrics(clientId));
  const [chartKey, setChartKey] = useState<'weightKg' | 'bodyFatPercent' | 'waistCm'>('weightKg');
  const [editing, setEditing] = useState<BodyMetric | 'new' | null>(null);
  const [deleting, setDeleting] = useState<BodyMetric | null>(null);
  const qc = useQueryClient();
  const remove = useMutation({
    mutationFn: (m: BodyMetric) => clientsApi.deleteBodyMetric(clientId, m.date),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queries.bodyMetrics(clientId).queryKey });
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      setDeleting(null);
      toast.success('Entry deleted');
    },
    onError: (err) => toast.error(errorMessage(err)),
  });

  if (error && !data) return <Card><QueryError error={error} onRetry={() => void refetch()} /></Card>;
  if (!data) return <Skeleton className="h-96" />;

  const meta = measureByKey[chartKey];
  const unit = unitLabel(meta.unit, units);
  const points = data.map((m) => ({ date: m.date, label: formatDay(m.date, 'd MMM'), value: display(m, chartKey, units) })).filter((p) => p.value !== null);
  const latest = [...data].reverse();
  const latestOf = (key: MeasureKey) => {
    const m = latest.find((x) => x[key] !== null);
    return m ? display(m, key, units) : null;
  };
  const firstVal = points[0]?.value ?? null;
  const lastVal = points.at(-1)?.value ?? null;
  const firstDate = points[0]?.date;

  return (
    <Card>
      <CardHeader
        title="Body metrics"
        description={data.length ? `${data.length} entries` : undefined}
        action={
          <Button size="sm" onClick={() => setEditing('new')}>
            <Plus aria-hidden />
            Log measurements
          </Button>
        }
      />
      <CardBody className="flex flex-col gap-5">
        {data.length === 0 ? (
          <EmptyState className="py-6" icon={<Scale />} title="No measurements yet" description="Log body weight, body fat or circumferences to track change over time." />
        ) : (
          <>
            <div className="grid grid-cols-3 gap-4">
              <Stat label="Weight" value={fmt(latestOf('weightKg'), units)} />
              <Stat label="Body fat" value={fmt(latestOf('bodyFatPercent'), '%')} />
              <Stat label="Waist" value={fmt(latestOf('waistCm'), 'cm')} />
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <Segmented
                label="Chart"
                value={chartKey}
                onChange={setChartKey}
                options={[
                  { value: 'weightKg', label: 'Weight' },
                  { value: 'bodyFatPercent', label: 'Body fat' },
                  { value: 'waistCm', label: 'Waist' },
                ]}
              />
              {firstVal !== null && lastVal !== null && firstDate && points.length > 1 && (
                <span className="tabular text-sm text-muted">
                  {lastVal - firstVal > 0 ? '+' : ''}
                  {Number((lastVal - firstVal).toFixed(1))} {unit} since {formatDay(firstDate, 'd MMM')}
                </span>
              )}
            </div>
            {points.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted">No {meta.label.toLowerCase()} entries yet.</p>
            ) : (
              <ChartFrame
                caption={`${meta.label} over time`}
                summary={`${meta.label}: ${points.length} entries, latest ${fmt(lastVal, unit)}.`}
                columns={['Date', meta.label]}
                rows={points.map((p) => [formatDay(p.date, 'd MMM yyyy'), fmt(p.value, unit)])}
              >
                <ResponsiveContainer width="100%" height="100%" initialDimension={{ width: 600, height: 240 }}>
                  <LineChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                    <CartesianGrid stroke={chartTheme.grid} vertical={false} />
                    <XAxis dataKey="label" tick={chartTheme.tick} tickLine={false} axisLine={false} minTickGap={24} />
                    <YAxis tick={chartTheme.tick} tickLine={false} axisLine={false} width={44} domain={['dataMin - 1', 'dataMax + 1']} tickFormatter={(v: number) => String(Math.round(v))} />
                    <Tooltip {...chartTheme.tooltip} formatter={(v) => [`${String(v)} ${unit}`, meta.label]} />
                    <Line type="monotone" dataKey="value" stroke={chartTheme.primary} strokeWidth={2.5} dot={{ r: 3, fill: chartTheme.primary }} />
                  </LineChart>
                </ResponsiveContainer>
              </ChartFrame>
            )}
            <div className="-mx-4 overflow-x-auto sm:-mx-5">
              <table className="w-full min-w-[30rem] text-sm">
                <thead>
                  <tr className="border-b border-border text-left text-xs tracking-wide text-muted uppercase">
                    <th scope="col" className="px-4 py-2 font-medium sm:px-5">Date</th>
                    <th scope="col" className="px-2 py-2 text-right font-medium">Weight</th>
                    <th scope="col" className="px-2 py-2 text-right font-medium">Body fat</th>
                    <th scope="col" className="px-2 py-2 text-right font-medium">Waist</th>
                    <th scope="col" className="px-2 py-2 font-medium">Notes</th>
                    <th scope="col" className="w-12 px-2 py-2"><span className="sr-only">Actions</span></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {latest.slice(0, 12).map((m) => (
                    <tr key={m.date}>
                      <th scope="row" className="px-4 py-2 text-left font-medium whitespace-nowrap sm:px-5">{formatDay(m.date, 'd MMM yyyy')}</th>
                      <td className="tabular px-2 py-2 text-right">{fmt(display(m, 'weightKg', units), units)}</td>
                      <td className="tabular px-2 py-2 text-right">{fmt(m.bodyFatPercent, '%')}</td>
                      <td className="tabular px-2 py-2 text-right">{fmt(m.waistCm, 'cm')}</td>
                      <td className="max-w-48 truncate px-2 py-2 text-muted">{m.notes}</td>
                      <td className="px-2 py-1">
                        <Menu
                          trigger={
                            <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${formatDay(m.date, 'd MMM yyyy')}`}>
                              <MoreHorizontal />
                            </Button>
                          }
                        >
                          <MenuItem icon={<Pencil />} onSelect={() => setEditing(m)}>
                            Edit
                          </MenuItem>
                          <MenuItem icon={<Trash2 />} tone="danger" onSelect={() => setDeleting(m)}>
                            Delete
                          </MenuItem>
                        </Menu>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </CardBody>
      {editing !== null && (
        <BodyMetricDialog clientId={clientId} existing={editing === 'new' ? null : editing} entries={data} onClose={() => setEditing(null)} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(o) => !o && setDeleting(null)}
        title="Delete entry?"
        description={deleting ? `Measurements from ${formatDay(deleting.date, 'd MMM yyyy')} will be removed.` : null}
        confirmLabel="Delete"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </Card>
  );
}

const numberText = z.string().trim().refine((s) => s === '' || Number.isFinite(Number(s.replace(',', '.'))), 'Enter a number.');
const formSchema = z.object({
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date.'),
  weightKg: numberText,
  bodyFatPercent: numberText,
  waistCm: numberText,
  chestCm: numberText,
  hipsCm: numberText,
  armCm: numberText,
  thighCm: numberText,
  restingHeartRate: numberText,
  notes: z.string().max(1000, 'Keep notes under 1000 characters.'),
});
type FormValues = z.infer<typeof formSchema>;

const num = (s: string) => (s.trim() === '' ? null : Number(s.replace(',', '.')));

function BodyMetricDialog({ clientId, existing, entries, onClose }: { clientId: string; existing: BodyMetric | null; entries: BodyMetric[]; onClose: () => void }) {
  const units = useUnits();
  const qc = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);
  const today = todayIso();
  const toText = (m: BodyMetric | null, key: MeasureKey) => {
    const v = m ? display(m, key, units) : null;
    return v === null ? '' : String(v);
  };
  const schema = formSchema.superRefine((v, ctx) => {
    if (v.date > today) ctx.addIssue({ code: 'custom', path: ['date'], message: 'The date cannot be in the future.' });
    if (measures.every((m) => v[m.key].trim() === '')) ctx.addIssue({ code: 'custom', path: ['weightKg'], message: 'Enter at least one measurement.' });
    for (const m of measures) {
      const n = num(v[m.key]);
      if (n === null || !Number.isFinite(n)) continue;
      const kgValue = m.key === 'weightKg' ? unitsToKg(n, units) : n;
      if (kgValue < m.min || kgValue > m.max) {
        const lo = m.key === 'weightKg' ? Math.round(kgToUnits(m.min, units)) : m.min;
        const hi = m.key === 'weightKg' ? Math.round(kgToUnits(m.max, units)) : m.max;
        ctx.addIssue({ code: 'custom', path: [m.key], message: `Enter a value between ${lo} and ${hi}.` });
      }
    }
  });
  const { register, handleSubmit, formState, setError, control } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      date: existing?.date ?? today,
      ...Object.fromEntries(measures.map((m) => [m.key, toText(existing, m.key)])),
      notes: existing?.notes ?? '',
    } as FormValues,
  });
  const date = useWatch({ control, name: 'date' });
  const overwrites = !existing && entries.some((e) => e.date === date);

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const values = Object.fromEntries(measures.map((m) => {
        const n = num(v[m.key]);
        return [m.key, n === null ? null : m.key === 'weightKg' ? unitsToKg(n, units) : n];
      })) as Record<MeasureKey, number | null>;
      return clientsApi.putBodyMetric(clientId, v.date, { ...values, notes: v.notes.trim() || null });
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queries.bodyMetrics(clientId).queryKey });
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      toast.success('Measurements saved');
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length) {
        for (const [k, message] of Object.entries(err.fieldErrors)) if (k in formSchema.shape) setError(k as keyof FormValues, { type: 'server', message });
        return;
      }
      setFormError(errorMessage(err));
    },
  });

  return (
    <Dialog
      open
      onOpenChange={(o) => !o && onClose()}
      title={existing ? 'Edit measurements' : 'Log measurements'}
      description="Fill in what you measured; everything is optional except one value."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="body-metric-form" loading={save.isPending}>
            Save
          </Button>
        </>
      }
    >
      <form id="body-metric-form" noValidate className="flex flex-col gap-4" onSubmit={(e) => void handleSubmit((v) => save.mutate(v))(e)}>
        <FormError message={formError} />
        <Field label="Date" error={formState.errors.date?.message} hint={overwrites ? 'An entry exists for this date; saving replaces it.' : undefined}>
          <Input type="date" max={today} disabled={existing !== null} {...register('date')} />
        </Field>
        <div className="grid grid-cols-2 gap-4">
          {measures.map((m) => (
            <Field key={m.key} label={`${m.label} (${unitLabel(m.unit, units)})`} error={formState.errors[m.key]?.message}>
              <Input inputMode="decimal" autoComplete="off" {...register(m.key)} />
            </Field>
          ))}
        </div>
        <Field label="Notes" optional error={formState.errors.notes?.message}>
          <Textarea rows={2} {...register('notes')} />
        </Field>
      </form>
    </Dialog>
  );
}
