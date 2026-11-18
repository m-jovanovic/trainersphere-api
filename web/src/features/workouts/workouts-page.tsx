import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, getRouteApi, useNavigate } from '@tanstack/react-router';
import { Archive, Copy, ListChecks, MoreHorizontal, Pencil, Plus } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { workoutsApi } from '@/lib/api/endpoints';
import { errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { WorkoutSummary } from '@/lib/api/types';
import { formatAgo } from '@/lib/dates';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/dialog';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Card, EmptyState, PageHeader, Skeleton } from '@/components/ui/misc';
import { SearchInput } from '@/components/ui/search-input';
import { QueryError } from '@/components/layout/page-states';

const route = getRouteApi('/_authed/_shell/_trainer/workouts/');

export function WorkoutsPage() {
  const search = route.useSearch();
  const nav = route.useNavigate();
  const navigate = useNavigate();
  const q = search.q ?? '';
  const list = useQuery(queries.workouts(q));
  const qc = useQueryClient();
  const [archiving, setArchiving] = useState<WorkoutSummary | null>(null);

  const duplicate = useMutation({
    mutationFn: (w: WorkoutSummary) => workoutsApi.duplicate(w.id),
    onSuccess: (copy) => {
      void qc.invalidateQueries({ queryKey: keys.workouts });
      toast.success(`Created ${copy.name}`);
      void navigate({ to: '/workouts/$workoutId', params: { workoutId: copy.id } });
    },
    onError: (err) => toast.error(errorMessage(err)),
  });
  const archive = useMutation({
    mutationFn: (w: WorkoutSummary) => workoutsApi.archive(w.id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.workouts });
      setArchiving(null);
      toast.success('Workout archived');
    },
    onError: (err) => toast.error(errorMessage(err)),
  });

  const items = list.data?.items ?? [];
  return (
    <>
      <PageHeader
        title="Workouts"
        description="Reusable templates you schedule for clients or add to programs."
        actions={
          <Button asChild>
            <Link to="/workouts/$workoutId" params={{ workoutId: 'new' }}>
              <Plus aria-hidden />
              New workout
            </Link>
          </Button>
        }
      />
      <SearchInput label="Search workouts" value={q} onChange={(v) => void nav({ search: { q: v || undefined }, replace: true })} className="mb-4 sm:max-w-sm" />
      {list.error && !list.data ? (
        <QueryError error={list.error} onRetry={() => void list.refetch()} />
      ) : !list.data ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-32" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <Card>
          <EmptyState
            icon={<ListChecks />}
            title={q ? `No workouts match “${q}”` : 'No workouts yet'}
            description={q ? undefined : 'Build a template once and reuse it across clients and programs.'}
            action={
              !q && (
                <Button asChild>
                  <Link to="/workouts/$workoutId" params={{ workoutId: 'new' }}>
                    Create a workout
                  </Link>
                </Button>
              )
            }
          />
        </Card>
      ) : (
        <ul aria-label="Workouts" className={cn('grid gap-3 sm:grid-cols-2 lg:grid-cols-3', list.isPlaceholderData && 'opacity-60')}>
          {items.map((w) => (
            <li key={w.id}>
              <Card className="relative flex h-full flex-col gap-1 p-4 transition-colors hover:border-border-strong">
                <div className="flex items-start justify-between gap-2">
                  <Link to="/workouts/$workoutId" params={{ workoutId: w.id }} className="font-semibold after:absolute after:inset-0 after:content-['']">
                    {w.name}
                  </Link>
                  <div className="relative z-10 -mt-1 -mr-1">
                    <Menu
                      trigger={
                        <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${w.name}`}>
                          <MoreHorizontal />
                        </Button>
                      }
                    >
                      <MenuItem icon={<Pencil />} onSelect={() => void navigate({ to: '/workouts/$workoutId', params: { workoutId: w.id } })}>
                        Edit
                      </MenuItem>
                      <MenuItem icon={<Copy />} onSelect={() => duplicate.mutate(w)}>
                        Duplicate
                      </MenuItem>
                      <MenuItem icon={<Archive />} tone="danger" onSelect={() => setArchiving(w)}>
                        Archive
                      </MenuItem>
                    </Menu>
                  </div>
                </div>
                {w.description && <p className="line-clamp-2 text-sm text-muted">{w.description}</p>}
                <p className="mt-auto pt-2 text-xs text-muted">
                  {w.exerciseCount} exercise{w.exerciseCount === 1 ? '' : 's'} · updated {formatAgo(w.updatedAt)}
                </p>
              </Card>
            </li>
          ))}
        </ul>
      )}
      <ConfirmDialog
        open={archiving !== null}
        onOpenChange={(o) => !o && setArchiving(null)}
        title={`Archive ${archiving?.name ?? 'workout'}?`}
        description="It is hidden from lists and pickers. Workouts already scheduled keep their copy."
        confirmLabel="Archive"
        loading={archive.isPending}
        onConfirm={() => archiving && archive.mutate(archiving)}
      />
    </>
  );
}
