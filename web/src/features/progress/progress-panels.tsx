import { useQuery } from '@tanstack/react-query';
import { Trophy } from 'lucide-react';
import { useState } from 'react';
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { queries } from '@/lib/api/queries';
import type { PersonalRecord, PrType, ProgressExercise, ProgressPoint, Units } from '@/lib/api/types';
import { useUnits } from '@/lib/auth/hooks';
import { formatAgo, formatDay } from '@/lib/dates';
import { formatPercent, formatPrExact, formatPrValue, formatVolume, formatWeight, kgToUnits, prLabels, round2, roundEstimate } from '@/lib/domain';
import { Segmented } from '@/components/ui/chips';
import { NativeSelect } from '@/components/ui/input';
import { Card, CardBody, CardHeader, EmptyState, Skeleton, Stat } from '@/components/ui/misc';
import { ChartFrame } from '@/components/chart';
import { chartAnimation, chartTheme } from '@/components/chart-theme';
import { ExerciseArt } from '@/components/exercise-art';
import { QueryError } from '@/components/layout/page-states';

const dims = { width: 600, height: 240 };

const plural = (n: number, word: string) => `${String(n)} ${word}${n === 1 ? '' : 's'}`;

export function TrainingSummaryCard({ clientId }: { clientId: string }) {
  const units = useUnits();
  const { data, error, refetch } = useQuery(queries.progressSummary(clientId, 12));
  if (error && !data) return <Card><QueryError error={error} onRetry={() => void refetch()} /></Card>;
  if (!data) return <Skeleton className="h-80" />;
  const rows = data.weeks.map((w) => ({ label: formatDay(w.weekStart, 'd MMM'), weekStart: w.weekStart, volume: Math.round(kgToUnits(w.volumeKg, units)), sessions: w.sessions }));
  const latest = data.weeks.at(-1);
  return (
    <Card>
      <CardHeader title="Training volume" description="Weekly, last 12 weeks" />
      <CardBody className="flex flex-col gap-5">
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Stat label="Sessions" value={data.totalSessions} hint="All time" />
          <Stat label="Streak" value={`${data.streakWeeks} wk`} hint="Weeks in a row" />
          <Stat label="Adherence 7d" value={formatPercent(data.adherence7d)} />
          <Stat label="Adherence 28d" value={formatPercent(data.adherence28d)} />
        </div>
        <ChartFrame
          caption="Weekly training volume"
          summary={`Weekly volume for the last ${rows.length} weeks${latest ? `; this week ${formatVolume(latest.volumeKg, units)} over ${plural(latest.sessions, 'session')}` : ''}.`}
          columns={['Week of', 'Volume']}
          rows={data.weeks.map((w) => [formatDay(w.weekStart, 'd MMM yyyy'), `${formatVolume(w.volumeKg, units)}, ${plural(w.sessions, 'session')}`])}
        >
          <ResponsiveContainer width="100%" height="100%" initialDimension={dims}>
            <BarChart data={rows} margin={{ top: 8, right: 4, bottom: 0, left: 0 }}>
              <CartesianGrid stroke={chartTheme.grid} vertical={false} />
              <XAxis dataKey="label" tick={chartTheme.tick} tickLine={false} axisLine={false} minTickGap={16} />
              <YAxis tick={chartTheme.tick} tickLine={false} axisLine={false} width={52} tickFormatter={(v: number) => (v >= 1000 ? `${Math.round(v / 100) / 10}k` : String(v))} />
              <Tooltip
                {...chartTheme.tooltip}
                formatter={(v) => [`${Number(v).toLocaleString('en-US')} ${units}`, 'Volume']}
                labelFormatter={(l) => (typeof l === 'string' ? `Week of ${l}` : '')}
              />
              <Bar {...chartAnimation} dataKey="volume" fill={chartTheme.primary} radius={[4, 4, 0, 0]} maxBarSize={36} />
            </BarChart>
          </ResponsiveContainer>
        </ChartFrame>
      </CardBody>
    </Card>
  );
}

type Metric = 'e1rm' | 'top' | 'volume';
const metricLabel: Record<Metric, string> = { e1rm: 'Estimated 1RM', top: 'Top set', volume: 'Volume' };

/** Chart value in the user's units, unrounded beyond 2 decimals. */
function metricValue(p: ProgressPoint, metric: Metric, units: Units): number | null {
  const kg = metric === 'e1rm' ? p.estimatedOneRepMaxKg : metric === 'top' ? p.topSetWeightKg : p.volumeKg;
  return kg === null ? null : round2(kgToUnits(kg, units));
}

function displayValue(value: number, metric: Metric, units: Units): number {
  return metric === 'e1rm' ? roundEstimate(value, units) : Math.round(value * 10) / 10;
}

/** The first metric that has data for these points, so a chart never opens empty when something was logged. */
function defaultMetric(points: ProgressPoint[]): Metric {
  if (points.some((p) => p.estimatedOneRepMaxKg !== null)) return 'e1rm';
  if (points.some((p) => p.topSetWeightKg !== null)) return 'top';
  return 'volume';
}

/**
 * Opens on the most recently trained exercise that has an estimated 1RM (an e1RM record exists), so the
 * default chart has data; otherwise the most recent exercise, whose chart then falls back to another metric.
 */
function defaultExercise(list: ProgressExercise[], records: PersonalRecord[] | undefined): string | undefined {
  const recent = [...list].sort((a, b) => b.lastPerformedAt.localeCompare(a.lastPerformedAt));
  const withE1rm = new Set((records ?? []).filter((r) => r.type === 'estimated-1rm').map((r) => r.exerciseId));
  return (recent.find((e) => withE1rm.has(e.exerciseId)) ?? recent[0])?.exerciseId;
}

export function ExerciseProgressCard({ clientId, exerciseId, onExerciseChange }: { clientId: string; exerciseId: string | undefined; onExerciseChange: (id: string) => void }) {
  const units = useUnits();
  // A metric picked by hand applies to the exercise it was picked on; another exercise opens on its own default.
  const [picked, setPicked] = useState<{ exerciseId: string; metric: Metric } | null>(null);
  const exercises = useQuery(queries.progressExercises(clientId));
  const records = useQuery(queries.personalRecords(clientId));
  const sorted = [...(exercises.data ?? [])].sort((a, b) => b.sessionCount - a.sessionCount);
  const recordsSettled = records.data !== undefined || records.isError;
  const selected = exerciseId && sorted.some((e) => e.exerciseId === exerciseId) ? exerciseId : defaultExercise(sorted, records.data);
  const progress = useQuery({ ...queries.exerciseProgress(clientId, selected ?? ''), enabled: selected !== undefined && (exerciseId !== undefined || recordsSettled) });

  if (exercises.error && !exercises.data) return <Card><QueryError error={exercises.error} onRetry={() => void exercises.refetch()} /></Card>;
  if (!exercises.data || (!exerciseId && !recordsSettled)) return <Skeleton className="h-96" />;
  if (sorted.length === 0) {
    return (
      <Card>
        <CardHeader title="Exercise progress" />
        <EmptyState title="No logged exercises yet" description="Charts appear after the first completed session." />
      </Card>
    );
  }

  const points = progress.data?.points ?? [];
  const metric = picked && picked.exerciseId === selected ? picked.metric : defaultMetric(points);
  const setMetric = (m: Metric) => selected && setPicked({ exerciseId: selected, metric: m });
  const data = points.map((p) => ({ date: p.date, label: formatDay(p.date, 'd MMM'), value: metricValue(p, metric, units), reps: p.topSetReps }));
  const withValues = data.filter((d): d is typeof d & { value: number } => d.value !== null);
  const firstRaw = withValues[0]?.value;
  const lastRaw = withValues.at(-1)?.value;
  const first = firstRaw === undefined ? null : displayValue(firstRaw, metric, units);
  const last = lastRaw === undefined ? null : displayValue(lastRaw, metric, units);
  const firstDate = withValues[0]?.date;
  const change = first !== null && last !== null ? Math.round((last - first) * 10) / 10 : null;
  const current = sorted.find((e) => e.exerciseId === selected);

  return (
    <Card>
      <CardHeader title="Exercise progress" description={current ? `${current.sessionCount} sessions · last ${formatAgo(current.lastPerformedAt)}` : undefined} />
      <CardBody className="flex flex-col gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <div className="flex min-w-0 flex-1 items-center gap-3">
            {current && <ExerciseArt illustration={current.exercise.illustration} name={current.exercise.name} variant="sm" className="size-12 shrink-0" />}
            <NativeSelect aria-label="Exercise" value={selected} onChange={(e) => onExerciseChange(e.target.value)} className="min-w-0 flex-1">
              {sorted.map((e) => (
                <option key={e.exerciseId} value={e.exerciseId}>
                  {e.exercise.name}
                </option>
              ))}
            </NativeSelect>
          </div>
          <Segmented
            label="Metric"
            value={metric}
            onChange={setMetric}
            options={(['e1rm', 'top', 'volume'] as const).map((m) => ({ value: m, label: m === 'e1rm' ? 'e1RM' : metricLabel[m] }))}
          />
        </div>
        <div className="flex items-baseline gap-3">
          <span className="tabular text-3xl font-semibold">{last === null ? '–' : `${last.toLocaleString('en-US')} ${units}`}</span>
          {change !== null && change !== 0 && firstDate && (
            <span className={change > 0 ? 'text-sm font-medium text-primary' : 'text-sm font-medium text-danger'}>
              {change > 0 ? '+' : ''}
              {change.toLocaleString('en-US')} {units} since {formatDay(firstDate, 'd MMM')}
            </span>
          )}
        </div>
        {progress.isPending ? (
          <Skeleton className="h-60" />
        ) : withValues.length === 0 ? (
          <p className="py-10 text-center text-sm text-muted">
            {metric === 'e1rm' && points.some((p) => p.topSetWeightKg !== null)
              ? 'No estimated 1RM yet: it counts sets of 1 to 12 reps. Top set and Volume show the rest.'
              : 'No weighted sets logged for this exercise.'}
          </p>
        ) : (
          <ChartFrame
            caption={`${metricLabel[metric]} for ${current?.exercise.name ?? 'exercise'}`}
            summary={`${metricLabel[metric]} over ${withValues.length} sessions, latest ${last ?? '–'} ${units}.`}
            columns={['Date', metricLabel[metric]]}
            rows={withValues.map((d) => [formatDay(d.date, 'd MMM yyyy'), `${String(displayValue(d.value, metric, units))} ${units}`])}
          >
            <ResponsiveContainer width="100%" height="100%" initialDimension={dims}>
              <LineChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                <CartesianGrid stroke={chartTheme.grid} vertical={false} />
                <XAxis dataKey="label" tick={chartTheme.tick} tickLine={false} axisLine={false} minTickGap={24} />
                <YAxis tick={chartTheme.tick} tickLine={false} axisLine={false} width={48} domain={['auto', 'auto']} />
                <Tooltip {...chartTheme.tooltip} formatter={(v) => [`${String(v)} ${units}`, metricLabel[metric]]} />
                <Line {...chartAnimation} type="monotone" dataKey="value" stroke={chartTheme.primary} strokeWidth={2.5} dot={{ r: 3, fill: chartTheme.primary }} activeDot={{ r: 5 }} connectNulls />
              </LineChart>
            </ResponsiveContainer>
          </ChartFrame>
        )}
      </CardBody>
    </Card>
  );
}

const prOrder: PrType[] = ['estimated-1rm', 'max-weight', 'max-reps', 'max-volume'];

export function PersonalRecordsCard({ clientId, limit }: { clientId: string; limit?: number }) {
  const units = useUnits();
  const { data, error, refetch } = useQuery(queries.personalRecords(clientId));
  if (error && !data) return <Card><QueryError error={error} onRetry={() => void refetch()} /></Card>;
  if (!data) return <Skeleton className="h-64" />;

  const byExercise = new Map<string, { name: string; illustration: string | null; latest: string; prs: Partial<Record<PrType, PersonalRecord>> }>();
  for (const pr of data) {
    const entry = byExercise.get(pr.exerciseId) ?? { name: pr.exercise.name, illustration: pr.exercise.illustration, latest: pr.achievedAt, prs: {} };
    entry.prs[pr.type] = pr;
    if (pr.achievedAt > entry.latest) entry.latest = pr.achievedAt;
    byExercise.set(pr.exerciseId, entry);
  }
  const rows = [...byExercise.entries()].sort((a, b) => b[1].latest.localeCompare(a[1].latest)).slice(0, limit);
  // A column nobody has a record in is just a row of dashes.
  const columns = prOrder.filter((t) => rows.some(([, r]) => r.prs[t] !== undefined));

  return (
    <Card>
      <CardHeader title="Personal records" description="Current best per exercise" />
      <CardBody>
        {rows.length === 0 ? (
          <EmptyState className="py-6" icon={<Trophy />} title="No records yet" description="Records are set from the second time an exercise is logged." />
        ) : (
          <div className="-mx-4 overflow-x-auto sm:-mx-5" role="region" aria-label="Personal records table" tabIndex={0}>
            <table className="w-full min-w-[32rem] text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs tracking-wide text-muted uppercase">
                  <th scope="col" className="px-4 py-2 font-medium sm:px-5">Exercise</th>
                  {columns.map((t) => (
                    <th key={t} scope="col" className="px-2 py-2 text-right font-medium">
                      {t === 'estimated-1rm' ? 'e1RM' : prLabels[t]}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {rows.map(([id, r]) => (
                  <tr key={id}>
                    <th scope="row" className="px-4 py-2.5 text-left font-medium sm:px-5">
                      <span className="flex items-center gap-2">
                        <ExerciseArt illustration={r.illustration} name={r.name} variant="sm" className="size-8 shrink-0" />
                        <span className="min-w-0">
                          <span className="block truncate">{r.name}</span>
                          <span className="block text-xs font-normal text-muted">{formatAgo(r.latest)}</span>
                        </span>
                      </span>
                    </th>
                    {columns.map((t) => {
                      const pr = r.prs[t];
                      return (
                        <td key={t} className="tabular px-2 py-2.5 text-right whitespace-nowrap" title={pr ? formatPrExact(pr, units) : undefined}>
                          {pr ? formatPrValue(pr, units) : <span className="text-muted">–</span>}
                          {pr && t === 'max-reps' && pr.weightKg ? <span className="block text-xs text-muted">@ {formatWeight(pr.weightKg, units)}</span> : null}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardBody>
    </Card>
  );
}
