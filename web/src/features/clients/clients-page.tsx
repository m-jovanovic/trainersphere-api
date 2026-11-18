import { useQuery } from '@tanstack/react-query';
import { Link, getRouteApi } from '@tanstack/react-router';
import { ChevronRight, UserPlus, Users } from 'lucide-react';
import { queries } from '@/lib/api/queries';
import type { ClientSort, ClientSummary } from '@/lib/api/types';
import { formatAgo, relativeDay } from '@/lib/dates';
import { fullName } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Segmented } from '@/components/ui/chips';
import { NativeSelect } from '@/components/ui/input';
import { Avatar, Badge, Card, CardBody, CardHeader, EmptyState, PageHeader, Skeleton } from '@/components/ui/misc';
import { SearchInput } from '@/components/ui/search-input';
import { AdherenceMeter } from '@/components/status';
import { QueryError } from '@/components/layout/page-states';
import { InviteClientDialog, PendingInvitationList } from './invitations';

const route = getRouteApi('/_authed/_shell/_trainer/clients/');

const sortOptions: { value: ClientSort; label: string }[] = [
  { value: 'name', label: 'Name' },
  { value: '-lastSessionAt', label: 'Recently active' },
  { value: 'adherence7d', label: 'Lowest adherence' },
];

export function ClientsPage() {
  const search = route.useSearch();
  const navigate = route.useNavigate();
  const status = search.status ?? 'active';
  const sort = search.sort ?? 'name';
  const q = search.q ?? '';
  const clients = useQuery(queries.clients({ search: q || undefined, status, sort }));
  const invites = useQuery({ ...queries.invitations('pending'), enabled: status === 'active' });

  const setSearch = (patch: Partial<typeof search>) => void navigate({ search: (prev) => ({ ...prev, ...patch }), replace: true });
  const openInvite = (open: boolean) => setSearch({ invite: open || undefined });

  const list = clients.data?.items ?? [];
  const pendingInvites = invites.data?.items ?? [];

  return (
    <>
      <PageHeader
        title="Clients"
        description={clients.data ? `${clients.data.totalCount} ${status} client${clients.data.totalCount === 1 ? '' : 's'}` : undefined}
        actions={
          <Button onClick={() => openInvite(true)}>
            <UserPlus aria-hidden />
            Invite client
          </Button>
        }
      />

      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center">
        <SearchInput label="Search clients" placeholder="Search by name or email" value={q} onChange={(v) => setSearch({ q: v || undefined })} className="sm:max-w-xs sm:flex-1" />
        <div className="flex items-center gap-2">
          <Segmented
            label="Client status"
            value={status}
            onChange={(v) => setSearch({ status: v === 'active' ? undefined : v })}
            options={[
              { value: 'active', label: 'Active' },
              { value: 'archived', label: 'Archived' },
            ]}
          />
          <NativeSelect aria-label="Sort clients" value={sort} onChange={(e) => setSearch({ sort: e.target.value === 'name' ? undefined : (e.target.value as ClientSort) })} className="w-auto">
            {sortOptions.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </NativeSelect>
        </div>
      </div>

      {status === 'active' && pendingInvites.length > 0 && !q && (
        <Card className="mb-4">
          <CardHeader title="Waiting to join" description={`${pendingInvites.length} pending invitation${pendingInvites.length === 1 ? '' : 's'}`} />
          <CardBody>
            <PendingInvitationList items={pendingInvites} />
          </CardBody>
        </Card>
      )}

      {clients.error && !clients.data ? (
        <QueryError error={clients.error} onRetry={() => void clients.refetch()} />
      ) : !clients.data ? (
        <div className="flex flex-col gap-2" aria-busy="true" aria-label="Loading clients">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-18" />
          ))}
        </div>
      ) : list.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Users />}
            title={q ? `No clients match “${q}”` : status === 'archived' ? 'No archived clients' : 'No clients yet'}
            description={q ? 'Try a different name or email.' : status === 'active' ? 'Invite a client to start planning their training.' : undefined}
            action={
              !q && status === 'active' ? (
                <Button onClick={() => openInvite(true)}>
                  <UserPlus aria-hidden />
                  Invite client
                </Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <Card>
          <div className="hidden grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.2fr)_minmax(0,1.4fr)_2rem] gap-4 border-b border-border px-5 py-2.5 text-xs font-medium tracking-wide text-muted uppercase md:grid" aria-hidden>
            <span>Client</span>
            <span>Last session</span>
            <span>Adherence 7d</span>
            <span>Next workout</span>
            <span />
          </div>
          <ul aria-label="Clients" className="divide-y divide-border">
            {list.map((c) => (
              <ClientRow key={c.id} client={c} />
            ))}
          </ul>
        </Card>
      )}

      <InviteClientDialog open={search.invite === true} onOpenChange={openInvite} />
    </>
  );
}

function ClientRow({ client: c }: { client: ClientSummary }) {
  return (
    <li>
      <Link
        to="/clients/$clientId"
        params={{ clientId: c.id }}
        className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-2 px-4 py-3 hover:bg-surface-2 sm:px-5 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.2fr)_minmax(0,1.4fr)_2rem]"
      >
        <span className="flex min-w-0 items-center gap-3">
          <Avatar name={c} />
          <span className="min-w-0">
            <span className="flex items-center gap-2 truncate font-medium">
              {fullName(c)}
              {c.status === 'archived' && <Badge>Archived</Badge>}
            </span>
            <span className="block truncate text-sm text-muted">{c.email}</span>
          </span>
        </span>
        <ChevronRight className="size-4 text-muted md:order-last" aria-hidden />
        <span className="text-sm text-muted md:text-fg">
          <span className="md:hidden">Last session </span>
          {formatAgo(c.lastSessionAt)}
        </span>
        <AdherenceMeter rate={c.adherence7d} label={`${fullName(c)} adherence, last 7 days`} className="col-span-2 md:col-span-1" />
        <span className="col-span-2 truncate text-sm md:col-span-1">
          {c.nextAssignment ? (
            <>
              <span className="text-muted">{relativeDay(c.nextAssignment.date)} · </span>
              {c.nextAssignment.workoutName}
            </>
          ) : (
            <span className="text-muted">Nothing scheduled</span>
          )}
        </span>
      </Link>
    </li>
  );
}
