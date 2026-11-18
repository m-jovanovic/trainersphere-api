import { Trophy } from 'lucide-react';
import type { PersonalRecord, PersonRef, Units } from '@/lib/api/types';
import { formatAgo } from '@/lib/dates';
import { formatPrDelta, formatPrValue, fullName, groupPersonalRecords, prGroupLabel } from '@/lib/domain';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/misc';
import { ExerciseArt } from '@/components/exercise-art';

type Pr = PersonalRecord & { client?: PersonRef };

/** Records grouped per exercise and session: one row with the lead record and the others named. */
export function PrList({ records, units, limit, showClient, showWhen = true, art = false, className }: { records: Pr[]; units: Units; limit?: number; showClient?: boolean; showWhen?: boolean; art?: boolean; className?: string }) {
  const groups = groupPersonalRecords(records).slice(0, limit);
  return (
    <ul className={cn('flex flex-col gap-3', className)}>
      {groups.map((g) => {
        const pr = g.primary;
        const meta = [showClient && pr.client ? fullName(pr.client) : null, prGroupLabel(g), showWhen ? formatAgo(pr.achievedAt) : null].filter(Boolean).join(' · ');
        return (
          <li key={g.key} className="flex items-start gap-3">
            {art ? (
              <ExerciseArt illustration={pr.exercise.illustration} name={pr.exercise.name} variant="sm" className="size-10 shrink-0" />
            ) : (
              <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-full bg-pr-soft text-pr">
                <Trophy className="size-4" aria-hidden />
              </span>
            )}
            <div className="min-w-0 flex-1">
              <p className="line-clamp-2 text-sm leading-snug font-medium">{pr.exercise.name}</p>
              <p className="mt-0.5 text-xs text-muted">{meta}</p>
            </div>
            <div className="shrink-0 text-right">
              <p className="tabular text-sm font-semibold">{formatPrValue(pr, units)}</p>
              <p className="tabular text-xs text-pr">{formatPrDelta(pr, units)}</p>
            </div>
          </li>
        );
      })}
    </ul>
  );
}

/** Session list badge: how many exercises set a record. */
export function SessionPrBadge({ count, long }: { count: number; long?: boolean }) {
  if (count <= 0) return null;
  const text = `Records on ${count} exercise${count === 1 ? '' : 's'}`;
  return (
    <Badge tone="pr" aria-label={text} title={text}>
      <Trophy aria-hidden />
      {long ? `${count} PR${count === 1 ? '' : 's'}` : count}
    </Badge>
  );
}
