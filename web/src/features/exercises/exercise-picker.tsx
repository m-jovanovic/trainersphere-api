import { useQuery } from '@tanstack/react-query';
import { Check, Plus } from 'lucide-react';
import { useState } from 'react';
import { queries } from '@/lib/api/queries';
import type { Exercise } from '@/lib/api/types';
import { humanize } from '@/lib/domain';
import { useDebounced } from '@/lib/use-debounced';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { Input, NativeSelect } from '@/components/ui/input';
import { Badge, EmptyState, Skeleton } from '@/components/ui/misc';
import { ExerciseArt } from '@/components/exercise-art';
import { ExerciseFormDialog } from './exercise-form';

/** Multi-select exercise browser used by the workout builder. Selection order is kept. */
export function ExercisePicker({ open, onOpenChange, onAdd }: { open: boolean; onOpenChange: (o: boolean) => void; onAdd: (exercises: Exercise[]) => void }) {
  const [text, setText] = useState('');
  const [muscle, setMuscle] = useState('');
  const [selected, setSelected] = useState<Exercise[]>([]);
  const [creating, setCreating] = useState(false);
  const search = useDebounced(text.trim(), 200);
  const taxonomy = useQuery({ ...queries.taxonomy(), enabled: open });
  const list = useQuery({ ...queries.exercises({ search: search || undefined, muscle: muscle ? [muscle] : undefined }), enabled: open });

  const close = () => {
    setSelected([]);
    setText('');
    onOpenChange(false);
  };
  const toggle = (e: Exercise) => setSelected((s) => (s.some((x) => x.id === e.id) ? s.filter((x) => x.id !== e.id) : [...s, e]));

  return (
    <>
      <Dialog
        open={open && !creating}
        variant="sheet"
        onOpenChange={(o) => !o && close()}
        title="Add exercises"
        footer={
          <>
            <Button variant="ghost" className="sm:mr-auto" onClick={() => setCreating(true)}>
              <Plus aria-hidden />
              New custom exercise
            </Button>
            <Button
              disabled={selected.length === 0}
              onClick={() => {
                onAdd(selected);
                close();
              }}
            >
              Add {selected.length || ''} exercise{selected.length === 1 ? '' : 's'}
            </Button>
          </>
        }
      >
        <div className="sticky -top-4 z-10 -mx-5 -mt-4 flex gap-2 bg-surface px-5 pt-4 pb-3">
          <Input type="search" aria-label="Search exercises" placeholder="Search exercises" value={text} onChange={(e) => setText(e.target.value)} className="flex-1" />
          <NativeSelect aria-label="Filter by muscle" value={muscle} onChange={(e) => setMuscle(e.target.value)} className="w-36">
            <option value="">All muscles</option>
            {taxonomy.data?.muscles.map((m) => (
              <option key={m} value={m}>
                {humanize(m)}
              </option>
            ))}
          </NativeSelect>
        </div>
        {!list.data ? (
          <div className="flex flex-col gap-2">
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-14" />
            ))}
          </div>
        ) : list.data.items.length === 0 ? (
          <EmptyState title="No exercises found" description="Try another search or create a custom exercise." />
        ) : (
          <ul aria-label="Exercises" className="flex flex-col gap-1">
            {list.data.items.map((e) => {
              const index = selected.findIndex((x) => x.id === e.id);
              const on = index >= 0;
              return (
                <li key={e.id}>
                  <button
                    type="button"
                    aria-pressed={on}
                    onClick={() => toggle(e)}
                    className={cn('flex w-full items-center gap-3 rounded-lg border px-2 py-2 text-left transition-colors', on ? 'border-primary bg-primary-soft' : 'border-transparent hover:bg-surface-2')}
                  >
                    <ExerciseArt illustration={e.illustration} name={e.name} variant="sm" className="size-12 shrink-0" />
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-2">
                        <span className="truncate font-medium">{e.name}</span>
                        {e.isCustom && <Badge>Custom</Badge>}
                      </span>
                      <span className="block truncate text-sm text-muted">{e.primaryMuscles.map(humanize).join(', ')}</span>
                    </span>
                    <span className={cn('flex size-7 shrink-0 items-center justify-center rounded-full border text-xs font-semibold', on ? 'border-primary bg-primary text-primary-fg' : 'border-border-strong')}>
                      {on ? index + 1 : <Check className="size-3.5 opacity-0" aria-hidden />}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </Dialog>
      {creating && <ExerciseFormDialog exercise={null} onClose={() => setCreating(false)} onSaved={(e) => setSelected((s) => [...s, e])} />}
    </>
  );
}
