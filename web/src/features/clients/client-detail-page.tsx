import { zodResolver } from "@hookform/resolvers/zod";
import {
  useMutation,
  useQuery,
  useQueryClient,
  useSuspenseQuery,
} from "@tanstack/react-query";
import { Link, getRouteApi } from "@tanstack/react-router";
import {
  Archive,
  ArchiveRestore,
  ArrowLeft,
  CalendarPlus,
  LayoutGrid,
  MoreHorizontal,
  Radio,
  Trash2,
} from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { clientsApi, programsApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { keys, queries } from "@/lib/api/queries";
import type {
  Assignment,
  ClientDetail,
  ProgramAssignment,
} from "@/lib/api/types";
import {
  addDaysIso,
  formatAgo,
  formatDay,
  relativeDay,
  todayIso,
} from "@/lib/dates";
import { formatPercent, fullName } from "@/lib/domain";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Field } from "@/components/ui/field";
import { Textarea } from "@/components/ui/input";
import { Menu, MenuItem } from "@/components/ui/menu";
import {
  Avatar,
  Badge,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  Skeleton,
  Stat,
} from "@/components/ui/misc";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { AdherenceMeter, AssignmentStatusBadge } from "@/components/status";
import { QueryError } from "@/components/layout/page-states";
import { SessionList } from "@/features/history/session-list";
import { BodyMetricsCard } from "@/features/progress/body-metrics";
import {
  ExerciseProgressCard,
  PersonalRecordsCard,
  TrainingSummaryCard,
} from "@/features/progress/progress-panels";
import {
  AssignmentDialog,
  ScheduleWorkoutDialog,
} from "@/features/schedule/assignment-dialog";
import { useInvalidateSchedule } from "@/features/schedule/use-invalidate-schedule";
import { AssignProgramDialog } from "@/features/programs/assign-program-dialog";

const route = getRouteApi("/_authed/_shell/_trainer/clients/$clientId");
type Tab = "overview" | "schedule" | "history" | "progress" | "body";

export function ClientDetailPage() {
  const { clientId } = route.useParams();
  const search = route.useSearch();
  const navigate = route.useNavigate();
  const { data: client } = useSuspenseQuery(queries.client(clientId));
  const tab: Tab = search.tab ?? "overview";
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [programOpen, setProgramOpen] = useState(false);
  const [archiveOpen, setArchiveOpen] = useState(false);
  const qc = useQueryClient();
  const archived = client.status === "archived";

  const toggleArchive = useMutation({
    mutationFn: () =>
      archived ? clientsApi.unarchive(clientId) : clientsApi.archive(clientId),
    onSuccess: (next) => {
      qc.setQueryData(queries.client(clientId).queryKey, next);
      void qc.invalidateQueries({ queryKey: keys.clients });
      void qc.invalidateQueries({ queryKey: keys.dashboard });
      void qc.invalidateQueries({ queryKey: keys.assignments });
      setArchiveOpen(false);
      toast.success(
        next.status === "archived"
          ? `${fullName(next)} archived`
          : `${fullName(next)} is active again`,
      );
    },
    onError: (err) => toast.error(errorMessage(err)),
  });

  return (
    <>
      <Link
        to="/clients"
        className="mb-3 inline-flex min-h-9 items-center gap-1.5 text-sm text-muted hover:text-fg"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Clients
      </Link>
      <header className="mb-5 flex flex-wrap items-center gap-x-4 gap-y-3">
        <div className="flex min-w-0 flex-1 items-center gap-4">
          <Avatar name={client} className="size-14 text-base" />
          <div className="min-w-0">
            <h1 className="flex items-center gap-2 truncate text-2xl font-semibold tracking-tight">
              {fullName(client)}
              {archived && <Badge>Archived</Badge>}
            </h1>
            <p className="truncate text-sm text-muted">
              {client.email} · joined{" "}
              {formatDay(client.joinedAt.slice(0, 10), "d MMM yyyy")}
            </p>
          </div>
        </div>
        {!archived && (
          <div className="order-last flex w-full items-center gap-2 sm:order-none sm:w-auto">
            <Button
              variant="secondary"
              className="flex-1 sm:flex-none"
              onClick={() => setProgramOpen(true)}
            >
              <LayoutGrid aria-hidden />
              Assign program
            </Button>
            <Button
              className="flex-1 sm:flex-none"
              onClick={() => setScheduleOpen(true)}
            >
              <CalendarPlus aria-hidden />
              Schedule workout
            </Button>
          </div>
        )}
        <Menu
          trigger={
            <Button
              variant="ghost"
              size="icon"
              aria-label="More client actions"
            >
              <MoreHorizontal />
            </Button>
          }
        >
          {archived ? (
            <MenuItem
              icon={<ArchiveRestore />}
              onSelect={() => toggleArchive.mutate()}
            >
              Restore client
            </MenuItem>
          ) : (
            <MenuItem
              icon={<Archive />}
              tone="danger"
              onSelect={() => setArchiveOpen(true)}
            >
              Archive client
            </MenuItem>
          )}
        </Menu>
      </header>

      {client.inProgressSessionId && (
        <Link
          to="/sessions/$sessionId"
          params={{ sessionId: client.inProgressSessionId }}
          className="mb-5 flex items-center gap-3 rounded-card border border-primary/40 bg-primary-soft px-4 py-3 text-sm text-primary-soft-fg"
        >
          <Radio className="size-4 animate-pulse" aria-hidden />
          <span className="flex-1 font-medium">
            {client.firstName} is training right now.
          </span>
          <span className="underline">View live session</span>
        </Link>
      )}

      <Tabs
        value={tab}
        onValueChange={(v) =>
          void navigate({
            search: (s) => ({
              ...s,
              tab: v === "overview" ? undefined : (v as Tab),
            }),
            replace: true,
          })
        }
      >
        <TabsList label="Client sections">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="schedule">Schedule</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="progress">Progress</TabsTrigger>
          <TabsTrigger value="body">Body</TabsTrigger>
        </TabsList>
        <TabsContent value="overview">
          <Overview client={client} onSchedule={() => setScheduleOpen(true)} />
        </TabsContent>
        <TabsContent value="schedule">
          <ClientSchedule
            clientId={clientId}
            onSchedule={() => setScheduleOpen(true)}
            archived={archived}
          />
        </TabsContent>
        <TabsContent value="history">
          <SessionList clientId={clientId} />
        </TabsContent>
        <TabsContent value="progress" className="flex flex-col gap-4 sm:gap-6">
          <ExerciseProgressCard
            clientId={clientId}
            exerciseId={search.exercise}
            onExerciseChange={(id) =>
              void navigate({
                search: (s) => ({ ...s, exercise: id }),
                replace: true,
              })
            }
          />
          <TrainingSummaryCard clientId={clientId} />
          <PersonalRecordsCard clientId={clientId} />
        </TabsContent>
        <TabsContent value="body">
          <BodyMetricsCard clientId={clientId} />
        </TabsContent>
      </Tabs>

      <ScheduleWorkoutDialog
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        clientId={clientId}
      />
      <AssignProgramDialog
        open={programOpen}
        onOpenChange={setProgramOpen}
        clientId={clientId}
      />
      <ConfirmDialog
        open={archiveOpen}
        onOpenChange={setArchiveOpen}
        title={`Archive ${fullName(client)}?`}
        description="Their future scheduled workouts are removed and they move to the archived list. History, records and metrics are kept, and you can restore them later."
        confirmLabel="Archive client"
        loading={toggleArchive.isPending}
        onConfirm={() => toggleArchive.mutate()}
      />
    </>
  );
}

function Overview({
  client,
  onSchedule,
}: {
  client: ClientDetail;
  onSchedule: () => void;
}) {
  const today = todayIso();
  const upcoming = useQuery(
    queries.assignments({
      clientId: client.id,
      from: today,
      to: addDaysIso(today, 13),
    }),
  );
  const [open, setOpen] = useState<Assignment | null>(null);
  return (
    <div className="grid gap-4 sm:gap-6 lg:grid-cols-3">
      <div className="flex flex-col gap-4 sm:gap-6 lg:col-span-2">
        <Card>
          <CardBody className="grid grid-cols-2 gap-5 sm:grid-cols-3">
            <div className="col-span-2 sm:col-span-1">
              <Stat
                label="Adherence 7d"
                value={formatPercent(client.adherence7d)}
              />
              <AdherenceMeter
                rate={client.adherence7d}
                label="Adherence, last 7 days"
                className="mt-2 [&>span]:hidden"
              />
            </div>
            <div>
              <Stat
                label="Adherence 28d"
                value={formatPercent(client.adherence28d)}
              />
              <AdherenceMeter
                rate={client.adherence28d}
                label="Adherence, last 28 days"
                className="mt-2 [&>span]:hidden"
              />
            </div>
            <Stat
              label="Last session"
              value={
                <span className="text-lg">
                  {formatAgo(client.lastSessionAt)}
                </span>
              }
            />
          </CardBody>
        </Card>
        <Card>
          <CardHeader
            title="Next two weeks"
            action={
              client.status === "active" && (
                <Button variant="ghost" size="sm" onClick={onSchedule}>
                  <CalendarPlus aria-hidden />
                  Add
                </Button>
              )
            }
          />
          <CardBody>
            {upcoming.error && !upcoming.data ? (
              <QueryError
                error={upcoming.error}
                onRetry={() => void upcoming.refetch()}
              />
            ) : !upcoming.data ? (
              <Skeleton className="h-32" />
            ) : upcoming.data.length === 0 ? (
              <p className="text-sm text-muted">
                Nothing scheduled. Assign a program or schedule a workout.
              </p>
            ) : (
              <AssignmentRows items={upcoming.data} onOpen={setOpen} />
            )}
          </CardBody>
        </Card>
        <PersonalRecordsCard clientId={client.id} limit={5} />
      </div>
      <div className="flex flex-col gap-4 sm:gap-6">
        <NotesCard client={client} />
        <ProgramsCard clientId={client.id} />
      </div>
      {open && (
        <AssignmentDialog
          assignment={open}
          onClose={() => setOpen(null)}
          showClient={false}
        />
      )}
    </div>
  );
}

function AssignmentRows({
  items,
  onOpen,
}: {
  items: Assignment[];
  onOpen: (a: Assignment) => void;
}) {
  return (
    <ul className="-mx-2 flex flex-col">
      {items.map((a) => (
        <li key={a.id}>
          <button
            type="button"
            onClick={() => onOpen(a)}
            className="flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left hover:bg-surface-2"
          >
            <span className="w-24 shrink-0 text-sm text-muted">
              {relativeDay(a.date)}
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium">
                {a.workout.name}
              </span>
              {a.programName && (
                <span className="block truncate text-xs text-muted">
                  {a.programName}
                </span>
              )}
            </span>
            <AssignmentStatusBadge
              status={a.status}
              inProgress={a.sessionId !== null}
            />
          </button>
        </li>
      ))}
    </ul>
  );
}

const notesSchema = z.object({
  goals: z.string().max(2000, "Keep this under 2000 characters."),
  trainerNotes: z.string().max(2000, "Keep this under 2000 characters."),
});
type NotesValues = z.infer<typeof notesSchema>;

function NotesCard({ client }: { client: ClientDetail }) {
  const qc = useQueryClient();
  const { register, handleSubmit, formState, reset } = useForm<NotesValues>({
    resolver: zodResolver(notesSchema),
    values: {
      goals: client.goals ?? "",
      trainerNotes: client.trainerNotes ?? "",
    },
  });
  const save = useMutation({
    mutationFn: (v: NotesValues) =>
      clientsApi.update(client.id, {
        goals: v.goals.trim() || null,
        trainerNotes: v.trainerNotes.trim() || null,
      }),
    onSuccess: (next) => {
      qc.setQueryData(queries.client(client.id).queryKey, next);
      reset({ goals: next.goals ?? "", trainerNotes: next.trainerNotes ?? "" });
      toast.success("Notes saved");
    },
    onError: (err) => toast.error(errorMessage(err)),
  });
  return (
    <Card>
      <CardHeader title="Goals and notes" />
      <CardBody>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(e) => void handleSubmit((v) => save.mutate(v))(e)}
        >
          <Field
            label="Goals"
            hint="Visible to the client."
            error={formState.errors.goals?.message}
          >
            <Textarea rows={3} {...register("goals")} />
          </Field>
          <Field
            label="Private notes"
            hint="Only you can see these."
            error={formState.errors.trainerNotes?.message}
          >
            <Textarea rows={3} {...register("trainerNotes")} />
          </Field>
          <Button
            type="submit"
            variant="secondary"
            className="self-end"
            disabled={!formState.isDirty}
            loading={save.isPending}
          >
            Save notes
          </Button>
        </form>
      </CardBody>
    </Card>
  );
}

function ProgramsCard({ clientId }: { clientId: string }) {
  const { data } = useQuery(queries.clientProgramAssignments(clientId));
  const invalidate = useInvalidateSchedule();
  const [removing, setRemoving] = useState<ProgramAssignment | null>(null);
  const today = todayIso();
  const unassign = useMutation({
    mutationFn: (pa: ProgramAssignment) => programsApi.unassign(pa.id),
    onSuccess: () => {
      invalidate();
      setRemoving(null);
      toast.success("Program removed; upcoming workouts were cleared");
    },
    onError: (err) => toast.error(errorMessage(err)),
  });
  const list = [...(data ?? [])].sort((a, b) =>
    b.startDate.localeCompare(a.startDate),
  );
  return (
    <Card>
      <CardHeader title="Programs" />
      <CardBody>
        {list.length === 0 ? (
          <p className="text-sm text-muted">No programs assigned.</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {list.map((pa) => {
              const state =
                pa.endDate < today
                  ? "Finished"
                  : pa.startDate > today
                    ? "Upcoming"
                    : "Current";
              return (
                <li key={pa.id} className="flex items-start gap-2">
                  <div className="min-w-0 flex-1">
                    <Link
                      to="/programs/$programId"
                      params={{ programId: pa.programId }}
                      className="block truncate text-sm font-medium hover:underline"
                    >
                      {pa.programName}
                    </Link>
                    <p className="text-xs text-muted">
                      {formatDay(pa.startDate, "d MMM")} –{" "}
                      {formatDay(pa.endDate, "d MMM yyyy")} ·{" "}
                      {pa.assignmentCount} workouts
                    </p>
                  </div>
                  <Badge tone={state === "Current" ? "primary" : "neutral"}>
                    {state}
                  </Badge>
                  {state !== "Finished" && (
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Remove ${pa.programName}`}
                      onClick={() => setRemoving(pa)}
                    >
                      <Trash2 />
                    </Button>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </CardBody>
      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(o) => !o && setRemoving(null)}
        title="Remove program?"
        description="Scheduled workouts from today onward are removed. Completed and skipped workouts stay in the history."
        confirmLabel="Remove program"
        loading={unassign.isPending}
        onConfirm={() => removing && unassign.mutate(removing)}
      />
    </Card>
  );
}

function ClientSchedule({
  clientId,
  onSchedule,
  archived,
}: {
  clientId: string;
  onSchedule: () => void;
  archived: boolean;
}) {
  const today = todayIso();
  const [offset, setOffset] = useState(0);
  const from = addDaysIso(today, offset - 28);
  const to = addDaysIso(today, offset + 55);
  const { data, error, refetch } = useQuery(
    queries.assignments({ clientId, from, to }),
  );
  const [open, setOpen] = useState<Assignment | null>(null);
  const upcoming = (data ?? []).filter((a) => a.date >= today);
  const past = (data ?? []).filter((a) => a.date < today).reverse();
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-1">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setOffset((o) => o - 28)}
          >
            Earlier
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setOffset((o) => o + 28)}
          >
            Later
          </Button>
          {offset !== 0 && (
            <Button variant="ghost" size="sm" onClick={() => setOffset(0)}>
              Today
            </Button>
          )}
        </div>
        <span className="text-sm text-muted">
          {formatDay(from, "d MMM")} – {formatDay(to, "d MMM yyyy")}
        </span>
        {!archived && (
          <Button size="sm" onClick={onSchedule}>
            <CalendarPlus aria-hidden />
            Schedule workout
          </Button>
        )}
      </div>
      {error && !data ? (
        <QueryError error={error} onRetry={() => void refetch()} />
      ) : !data ? (
        <Skeleton className="h-64" />
      ) : data.length === 0 ? (
        <Card>
          <EmptyState title="Nothing in this range" />
        </Card>
      ) : (
        <>
          {upcoming.length > 0 && (
            <Card>
              <CardHeader title="Upcoming" />
              <CardBody>
                <AssignmentRows items={upcoming} onOpen={setOpen} />
              </CardBody>
            </Card>
          )}
          {past.length > 0 && (
            <Card>
              <CardHeader title="Past" />
              <CardBody>
                <AssignmentRows items={past} onOpen={setOpen} />
              </CardBody>
            </Card>
          )}
        </>
      )}
      {open && (
        <AssignmentDialog
          assignment={open}
          onClose={() => setOpen(null)}
          showClient={false}
        />
      )}
    </div>
  );
}
