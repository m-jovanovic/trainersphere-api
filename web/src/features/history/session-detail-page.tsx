import { useQuery, useSuspenseQuery } from '@tanstack/react-query';
import { Link, getRouteApi, useRouter } from '@tanstack/react-router';
import { ArrowLeft, Check, Play } from 'lucide-react';
import { queries } from '@/lib/api/queries';
import type { SessionExercise } from '@/lib/api/types';
import { useUnits, useUser } from '@/lib/auth/hooks';
import { formatDateTime } from '@/lib/dates';
import { formatDuration, formatGoal, formatLoad, formatVolume, formatWeight, fullName, prHeading } from '@/lib/domain';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge, Card, CardBody, CardHeader, Stat } from '@/components/ui/misc';
import { ExerciseArt } from '@/components/exercise-art';
import { PrList } from '@/components/pr-list';

const route = getRouteApi('/_authed/_shell/sessions/$sessionId');

export function SessionDetailPage() {
  const { sessionId } = route.useParams();
  const { data: s } = useSuspenseQuery(queries.session(sessionId));
  const user = useUser();
  const units = useUnits();
  const router = useRouter();
  const isTrainer = user.role === 'trainer';
  const client = useQuery({ ...queries.client(s.clientId), enabled: isTrainer });
  const inProgress = s.status === 'in-progress';
  const duration = s.summary.durationSeconds ?? (s.completedAt ? (Date.parse(s.completedAt) - Date.parse(s.startedAt)) / 1000 : null);
  const prs = s.summary.personalRecords;

  return (
    <>
      <button type="button" onClick={() => router.history.back()} className="mb-3 inline-flex min-h-9 items-center gap-1.5 text-sm text-muted hover:text-fg">
        <ArrowLeft className="size-4" aria-hidden />
        Back
      </button>
      <header className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="text-sm text-muted">
            {isTrainer && client.data && (
              <>
                <Link to="/clients/$clientId" params={{ clientId: s.clientId }} className="font-medium text-fg hover:underline">
                  {fullName(client.data)}
                </Link>
                {' · '}
              </>
            )}
            {formatDateTime(s.startedAt, 'EEEE d MMMM yyyy, HH:mm')}
          </p>
          <h1 className="flex items-center gap-2 truncate text-2xl font-semibold tracking-tight">
            {s.workoutName}
            {inProgress && <Badge tone="primary">In progress</Badge>}
          </h1>
        </div>
        {inProgress && !isTrainer && (
          <Button asChild size="lg">
            <Link to="/log/$sessionId" params={{ sessionId: s.id }}>
              <Play aria-hidden />
              Resume workout
            </Link>
          </Button>
        )}
      </header>

      <div className="flex flex-col gap-4 sm:gap-6">
        <Card>
          <CardBody className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Stat label="Volume" value={formatVolume(s.summary.totalVolumeKg, units)} />
            <Stat label="Sets" value={s.summary.completedSets} />
            <Stat label="Reps" value={s.summary.totalReps} />
            <Stat label="Duration" value={duration === null ? '–' : formatDuration(duration)} />
          </CardBody>
        </Card>

        {prs.length > 0 && (
          <Card className="border-pr/30 bg-pr-soft/40">
            <CardHeader title={prHeading(prs)} />
            <CardBody>
              <PrList records={prs} units={units} showWhen={false} className="grid gap-3 sm:grid-cols-2" />
            </CardBody>
          </Card>
        )}

        {s.notes && (
          <Card>
            <CardHeader title="Notes" />
            <CardBody>
              <p className="text-sm whitespace-pre-line">{s.notes}</p>
            </CardBody>
          </Card>
        )}

        <ol className="flex flex-col gap-3" aria-label="Exercises">
          {s.exercises.map((e) => (
            <ExerciseResult key={e.position} e={e} />
          ))}
        </ol>
      </div>
    </>
  );
}

function ExerciseResult({ e }: { e: SessionExercise }) {
  const units = useUnits();
  const p = e.prescription;
  const load = p ? formatLoad(p.load, units) : null;
  const sets = [...e.sets].sort((a, b) => a.setNumber - b.setNumber);
  const done = sets.filter((x) => x.completed).length;
  return (
    <li>
      <Card>
        <div className="flex items-center gap-3 p-4 pb-2 sm:px-5">
          <ExerciseArt illustration={e.exercise.illustration} name={e.exercise.name} variant="sm" className="size-12 shrink-0" />
          <div className="min-w-0 flex-1">
            <h2 className="truncate font-semibold">{e.exercise.name}</h2>
            <p className="text-sm text-muted">
              {p ? `Planned ${p.sets} × ${formatGoal(p.goal)}${load ? ` @ ${load}` : ''}` : 'Added during the session'}
            </p>
          </div>
          <span className="tabular text-sm text-muted">
            {done}/{p ? Math.max(p.sets, sets.length) : sets.length}
          </span>
        </div>
        {sets.length === 0 ? (
          <p className="px-4 pb-4 text-sm text-muted sm:px-5">Not logged.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs tracking-wide text-muted uppercase">
                <th scope="col" className="px-4 py-1.5 font-medium sm:px-5">Set</th>
                <th scope="col" className="px-2 py-1.5 text-right font-medium">Weight</th>
                <th scope="col" className="px-2 py-1.5 text-right font-medium">{p?.goal.type === 'time' ? 'Time' : p?.goal.type === 'distance' ? 'Distance' : 'Reps'}</th>
                <th scope="col" className="px-2 py-1.5 text-right font-medium">RPE</th>
                <th scope="col" className="w-12 px-4 py-1.5 sm:px-5"><span className="sr-only">Done</span></th>
              </tr>
            </thead>
            <tbody>
              {sets.map((x) => (
                <tr key={x.id} className={cn('border-t border-border', !x.completed && 'text-muted')}>
                  <td className="tabular px-4 py-2 sm:px-5">{x.setNumber}</td>
                  <td className="tabular px-2 py-2 text-right">{x.weightKg ? formatWeight(x.weightKg, units) : '–'}</td>
                  <td className="tabular px-2 py-2 text-right">
                    {x.durationSeconds != null ? formatDuration(x.durationSeconds) : x.distanceMeters != null ? `${x.distanceMeters} m` : (x.reps ?? '–')}
                  </td>
                  <td className="tabular px-2 py-2 text-right">{x.rpe ?? '–'}</td>
                  <td className="px-4 py-2 sm:px-5">{x.completed ? <Check className="size-4 text-primary" aria-label="Completed" /> : <span className="sr-only">Not completed</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </li>
  );
}
