import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from '@tanstack/react-router';
import { Archive, CalendarRange, LayoutGrid, MoreHorizontal, Pencil, Plus } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { programsApi } from '@/lib/api/endpoints';
import { errorMessage } from '@/lib/api/http';
import { keys, queries } from '@/lib/api/queries';
import type { ProgramSummary } from '@/lib/api/types';
import { formatAgo } from '@/lib/dates';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/dialog';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Card, EmptyState, PageHeader, Skeleton } from '@/components/ui/misc';
import { SearchInput } from '@/components/ui/search-input';
import { QueryError } from '@/components/layout/page-states';
import { AssignProgramDialog } from './assign-program-dialog';

export function ProgramsPage() {
  const [q, setQ] = useState('');
  const list = useQuery(queries.programs(q));
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [archiving, setArchiving] = useState<ProgramSummary | null>(null);
  const [assigning, setAssigning] = useState<ProgramSummary | null>(null);
  const archive = useMutation({
    mutationFn: (p: ProgramSummary) => programsApi.archive(p.id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.programs });
      setArchiving(null);
      toast.success('Program archived');
    },
    onError: (err) => toast.error(errorMessage(err)),
  });
  const items = list.data?.items ?? [];
  return (
    <>
      <PageHeader
        title="Programs"
        description="Multi-week plans built from your workouts. Assign one and every session lands on the calendar."
        actions={
          <Button asChild>
            <Link to="/programs/$programId" params={{ programId: 'new' }}>
              <Plus aria-hidden />
              New program
            </Link>
          </Button>
        }
      />
      <SearchInput label="Search programs" value={q} onChange={setQ} className="mb-4 sm:max-w-sm" />
      {list.error && !list.data ? (
        <QueryError error={list.error} onRetry={() => void list.refetch()} />
      ) : !list.data ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-36" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <Card>
          <EmptyState
            icon={<LayoutGrid />}
            title={q ? `No programs match “${q}”` : 'No programs yet'}
            description={q ? undefined : 'Lay out workouts across weeks, then assign the whole block to clients.'}
            action={
              !q && (
                <Button asChild>
                  <Link to="/programs/$programId" params={{ programId: 'new' }}>
                    Create a program
                  </Link>
                </Button>
              )
            }
          />
        </Card>
      ) : (
        <ul aria-label="Programs" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((p) => (
            <li key={p.id}>
              <Card className="relative flex h-full flex-col gap-1 p-4 transition-colors hover:border-border-strong">
                <div className="flex items-start justify-between gap-2">
                  <Link to="/programs/$programId" params={{ programId: p.id }} className="font-semibold after:absolute after:inset-0 after:content-['']">
                    {p.name}
                  </Link>
                  <div className="relative z-10 -mt-1 -mr-1">
                    <Menu
                      trigger={
                        <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${p.name}`}>
                          <MoreHorizontal />
                        </Button>
                      }
                    >
                      <MenuItem icon={<Pencil />} onSelect={() => void navigate({ to: '/programs/$programId', params: { programId: p.id } })}>
                        Edit
                      </MenuItem>
                      <MenuItem icon={<CalendarRange />} onSelect={() => setAssigning(p)}>
                        Assign to clients
                      </MenuItem>
                      <MenuItem icon={<Archive />} tone="danger" onSelect={() => setArchiving(p)}>
                        Archive
                      </MenuItem>
                    </Menu>
                  </div>
                </div>
                {p.description && <p className="line-clamp-2 text-sm text-muted">{p.description}</p>}
                <p className="mt-auto pt-2 text-xs text-muted">
                  {p.weeks} week{p.weeks === 1 ? '' : 's'} · {p.workoutCount} workouts · updated {formatAgo(p.updatedAt)}
                </p>
              </Card>
            </li>
          ))}
        </ul>
      )}
      {assigning && <AssignProgramDialog open onOpenChange={(o) => !o && setAssigning(null)} programId={assigning.id} />}
      <ConfirmDialog
        open={archiving !== null}
        onOpenChange={(o) => !o && setArchiving(null)}
        title={`Archive ${archiving?.name ?? 'program'}?`}
        description="It is hidden from lists. Clients who already have it assigned keep their scheduled workouts."
        confirmLabel="Archive"
        loading={archive.isPending}
        onConfirm={() => archiving && archive.mutate(archiving)}
      />
    </>
  );
}
