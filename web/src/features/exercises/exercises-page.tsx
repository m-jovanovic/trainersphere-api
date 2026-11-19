import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getRouteApi } from '@tanstack/react-router';
import { Archive, Dumbbell, Pencil, Plus, SlidersHorizontal } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { exercisesApi } from '@/lib/api/endpoints';
import { errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { Exercise } from '@/lib/api/types';
import { useUser } from '@/lib/auth/hooks';
import { humanize } from '@/lib/domain';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { ChipGroup, Segmented } from '@/components/ui/chips';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { Badge, Card, EmptyState, PageHeader, Skeleton } from '@/components/ui/misc';
import { SearchInput } from '@/components/ui/search-input';
import { ExerciseArt } from '@/components/exercise-art';
import { QueryError } from '@/components/layout/page-states';
import { ExerciseFormDialog } from './exercise-form';

const route = getRouteApi('/_authed/_shell/exercises');

export function ExercisesPage() {
  const user = useUser();
  const isTrainer = user.role === 'trainer';
  const search = route.useSearch();
  const navigate = route.useNavigate();
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [editing, setEditing] = useState<Exercise | 'new' | null>(null);
  const taxonomy = useQuery(queries.taxonomy());
  const filters = { muscle: search.muscle ?? [], equipment: search.equipment ?? [], pattern: search.pattern ?? [] };
  const list = useQuery(
    queries.exercises({
      search: search.q || undefined,
      muscle: filters.muscle.length ? filters.muscle : undefined,
      equipment: filters.equipment.length ? filters.equipment : undefined,
      pattern: filters.pattern.length ? filters.pattern : undefined,
      custom: search.custom,
    }),
  );
  const activeFilters = filters.muscle.length + filters.equipment.length + filters.pattern.length;
  const setSearch = (patch: Partial<typeof search>) => void navigate({ search: (s) => ({ ...s, ...patch }), replace: true });
  const arr = (v: string[]) => (v.length ? v : undefined);
  const items = list.data?.items ?? [];

  return (
    <>
      <PageHeader
        title="Exercises"
        description={list.data ? `${list.data.totalCount} exercise${list.data.totalCount === 1 ? '' : 's'}` : undefined}
        actions={
          isTrainer && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden />
              New exercise
            </Button>
          )
        }
      />
      <div className="mb-4 flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <SearchInput label="Search exercises" value={search.q ?? ''} onChange={(v) => setSearch({ q: v || undefined })} className="min-w-0 flex-1 sm:max-w-sm" />
          <Button variant="secondary" aria-expanded={filtersOpen} aria-controls="exercise-filters" onClick={() => setFiltersOpen((o) => !o)}>
            <SlidersHorizontal aria-hidden />
            Filters{activeFilters ? ` (${activeFilters})` : ''}
          </Button>
          {isTrainer && (
            <Segmented
              label="Library"
              value={search.custom === undefined ? 'all' : search.custom ? 'custom' : 'library'}
              onChange={(v) => setSearch({ custom: v === 'all' ? undefined : v === 'custom' })}
              options={[
                { value: 'all', label: 'All' },
                { value: 'library', label: 'Library' },
                { value: 'custom', label: 'Custom' },
              ]}
            />
          )}
        </div>
        {filtersOpen && taxonomy.data && (
          <Card id="exercise-filters" className="flex flex-col gap-4 p-4">
            <FilterRow label="Muscle" options={taxonomy.data.muscles} value={filters.muscle} onChange={(v) => setSearch({ muscle: arr(v) })} />
            <FilterRow label="Equipment" options={taxonomy.data.equipment} value={filters.equipment} onChange={(v) => setSearch({ equipment: arr(v) })} />
            <FilterRow label="Movement" options={taxonomy.data.movementPatterns} value={filters.pattern} onChange={(v) => setSearch({ pattern: arr(v) })} />
            {activeFilters > 0 && (
              <Button variant="link" className="self-start" onClick={() => setSearch({ muscle: undefined, equipment: undefined, pattern: undefined })}>
                Clear filters
              </Button>
            )}
          </Card>
        )}
      </div>

      {list.error && !list.data ? (
        <QueryError error={list.error} onRetry={() => void list.refetch()} />
      ) : !list.data ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {Array.from({ length: 8 }, (_, i) => (
            <Skeleton key={i} className="aspect-[4/5]" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <Card>
          <EmptyState icon={<Dumbbell />} title="No exercises match" description="Try a different search or clear the filters." />
        </Card>
      ) : (
        <ul aria-label="Exercise library" className={cn('grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4', list.isPlaceholderData && 'opacity-60')}>
          {items.map((e) => (
            <li key={e.id}>
              <button
                type="button"
                onClick={() => setSearch({ id: e.id })}
                className="group flex w-full flex-col overflow-hidden rounded-card border border-border bg-surface text-left transition-colors hover:border-border-strong"
              >
                <ExerciseArt illustration={e.illustration} name={e.name} className="w-full rounded-none" />
                <span className="flex flex-col gap-0.5 p-3">
                  <span className="flex items-start justify-between gap-2">
                    <span className="text-sm leading-snug font-medium">{e.name}</span>
                    {e.isCustom && <Badge className="shrink-0">Custom</Badge>}
                  </span>
                  <span className="truncate text-xs text-muted">{e.primaryMuscles.map(humanize).join(', ')}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {search.id && <ExerciseDetail id={search.id} canEdit={isTrainer} onClose={() => setSearch({ id: undefined })} onEdit={setEditing} />}
      {editing !== null && <ExerciseFormDialog exercise={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  );
}

function FilterRow({ label, options, value, onChange }: { label: string; options: string[]; value: string[]; onChange: (v: string[]) => void }) {
  return (
    <div className="flex flex-col gap-2">
      <span className="text-xs font-semibold tracking-wide text-muted uppercase">{label}</span>
      <ChipGroup label={`Filter by ${label.toLowerCase()}`} value={value} onChange={onChange} options={options.map((o) => ({ value: o, label: humanize(o) }))} />
    </div>
  );
}

function ExerciseDetail({ id, canEdit, onClose, onEdit }: { id: string; canEdit: boolean; onClose: () => void; onEdit: (e: Exercise) => void }) {
  const { data: e, error } = useQuery(queries.exercise(id));
  const qc = useQueryClient();
  const [archiving, setArchiving] = useState(false);
  const archive = useMutation({
    mutationFn: () => exercisesApi.archive(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.exercises });
      toast.success('Exercise archived');
      setArchiving(false);
      onClose();
    },
    onError: (err) => toast.error(errorMessage(err)),
  });
  return (
    <>
      <Dialog
        open={!archiving}
        variant="sheet"
        onOpenChange={(o) => !o && onClose()}
        title={e?.name ?? 'Exercise'}
        description={e ? `${humanize(e.category)} · ${humanize(e.movementPattern)}${e.unilateral ? ' · unilateral' : ''}` : undefined}
        footer={
          e && canEdit && e.isCustom ? (
            <>
              <Button variant="ghost" className="text-danger sm:mr-auto" onClick={() => setArchiving(true)}>
                <Archive aria-hidden />
                Archive
              </Button>
              <Button
                variant="secondary"
                onClick={() => {
                  onEdit(e);
                  onClose();
                }}
              >
                <Pencil aria-hidden />
                Edit
              </Button>
            </>
          ) : undefined
        }
      >
        {error ? (
          <QueryError error={error} />
        ) : !e ? (
          <Skeleton className="aspect-square w-full" />
        ) : (
          <div className="flex flex-col gap-5">
            <ExerciseArt illustration={e.illustration} name={e.name} decorative={false} className="mx-auto w-full max-w-sm" />
            {e.cue && (
              <div>
                <h3 className="mb-1 text-sm font-semibold">Coaching cue</h3>
                <p className="text-sm leading-relaxed">{e.cue}</p>
              </div>
            )}
            <Tags title="Primary muscles" values={e.primaryMuscles} tone="primary" />
            <Tags title="Secondary muscles" values={e.secondaryMuscles} />
            <Tags title="Equipment" values={e.equipment} />
          </div>
        )}
      </Dialog>
      <ConfirmDialog
        open={archiving}
        onOpenChange={setArchiving}
        title="Archive exercise?"
        description="It disappears from the library and the picker. Workouts and history that use it keep working."
        confirmLabel="Archive"
        loading={archive.isPending}
        onConfirm={() => archive.mutate()}
      />
    </>
  );
}

function Tags({ title, values, tone = 'neutral' }: { title: string; values: string[]; tone?: 'neutral' | 'primary' }) {
  if (values.length === 0) return null;
  return (
    <div>
      <h3 className="mb-1.5 text-sm font-semibold">{title}</h3>
      <div className="flex flex-wrap gap-1.5">
        {values.map((v) => (
          <Badge key={v} tone={tone} className="text-sm">
            {humanize(v)}
          </Badge>
        ))}
      </div>
    </div>
  );
}
