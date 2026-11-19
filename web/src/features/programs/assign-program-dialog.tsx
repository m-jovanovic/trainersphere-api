import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { toast } from 'sonner';
import { programsApi } from '@/lib/api/endpoints';
import { errorMessage } from '@/lib/api/http';
import { queries } from '@/lib/api/queries';
import { addDaysIso, formatDay, todayIso } from '@/lib/dates';
import { fullName } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { Field, FormError } from '@/components/ui/field';
import { Input, NativeSelect } from '@/components/ui/input';
import { Avatar } from '@/components/ui/misc';
import { useInvalidateSchedule } from '@/features/schedule/use-invalidate-schedule';

/** Assigns a program to one or more clients from a start date. Either side can be preset. */
export function AssignProgramDialog({ open, onOpenChange, programId: fixedProgram, clientId: fixedClient }: { open: boolean; onOpenChange: (o: boolean) => void; programId?: string; clientId?: string }) {
  const invalidate = useInvalidateSchedule();
  const [programId, setProgramId] = useState(fixedProgram ?? '');
  const [clientIds, setClientIds] = useState<string[]>(fixedClient ? [fixedClient] : []);
  const [startDate, setStartDate] = useState(todayIso());
  const [error, setError] = useState<string | null>(null);
  const programs = useQuery({ ...queries.programs(''), enabled: open && !fixedProgram });
  const clients = useQuery({ ...queries.clients({ status: 'active', sort: 'name' }), enabled: open && !fixedClient });
  const program = useQuery({ ...queries.program(programId), enabled: open && programId !== '' });

  const assign = useMutation({
    mutationFn: () => programsApi.assign(programId, { clientIds, startDate }),
    onSuccess: (result) => {
      invalidate();
      const total = result.reduce((n, r) => n + r.assignmentCount, 0);
      toast.success(`${result[0]?.programName ?? 'Program'} assigned to ${result.length} client${result.length === 1 ? '' : 's'}`, {
        description: `${total} workouts scheduled from ${formatDay(startDate, 'd MMM')}.`,
      });
      onOpenChange(false);
      if (!fixedClient) setClientIds([]);
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const p = program.data;
  // A cleared date input reports '', which the date helpers cannot format.
  const validStart = /^\d{4}-\d{2}-\d{2}$/.test(startDate);
  const endDate = p && validStart ? addDaysIso(startDate, p.weeks * 7 - 1) : null;
  const submit = () => {
    const problem = !programId ? 'Choose a program.' : clientIds.length === 0 ? 'Choose at least one client.' : !validStart ? 'Pick a start date.' : null;
    setError(problem);
    if (!problem) assign.mutate();
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        if (!o) setError(null);
        onOpenChange(o);
      }}
      title="Assign program"
      description="Creates one scheduled workout per program slot, starting on the chosen date."
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={submit} loading={assign.isPending}>
            Assign program
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <FormError message={error} />
        {!fixedProgram && (
          <Field label="Program">
            <NativeSelect value={programId} onChange={(e) => setProgramId(e.target.value)}>
              <option value="">Choose a program…</option>
              {programs.data?.items.map((pr) => (
                <option key={pr.id} value={pr.id}>
                  {pr.name} ({pr.weeks} weeks)
                </option>
              ))}
            </NativeSelect>
          </Field>
        )}
        {!fixedClient && (
          <fieldset className="flex flex-col gap-1.5">
            <legend className="mb-1.5 text-sm font-medium">Clients</legend>
            {clients.data?.items.length === 0 && <p className="text-sm text-muted">No active clients.</p>}
            <div className="flex max-h-56 flex-col gap-1 overflow-y-auto">
              {clients.data?.items.map((c) => {
                const checked = clientIds.includes(c.id);
                return (
                  <label key={c.id} className="flex min-h-11 cursor-pointer items-center gap-3 rounded-lg px-2 hover:bg-surface-2">
                    <input
                      type="checkbox"
                      className="size-5 accent-[var(--primary)]"
                      checked={checked}
                      onChange={() => setClientIds((ids) => (checked ? ids.filter((x) => x !== c.id) : [...ids, c.id]))}
                    />
                    <Avatar name={c} className="size-7 text-[10px]" />
                    <span className="text-sm">{fullName(c)}</span>
                  </label>
                );
              })}
            </div>
          </fieldset>
        )}
        <Field label="Start date" hint={p && validStart ? `Day 1 is ${formatDay(startDate, 'EEEE')}; each program week starts on this weekday.` : undefined}>
          <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
        </Field>
        {p && endDate && (
          <p className="rounded-lg bg-surface-2 px-3 py-2 text-sm">
            <span className="font-medium">{p.slots.length} workouts</span> over {p.weeks} weeks, {formatDay(startDate, 'd MMM')} – {formatDay(endDate, 'd MMM yyyy')}
            {clientIds.length > 1 ? `, for each of ${clientIds.length} clients` : ''}.
          </p>
        )}
      </div>
    </Dialog>
  );
}
