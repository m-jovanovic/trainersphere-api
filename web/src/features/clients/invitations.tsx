import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { MailPlus, MoreHorizontal, RotateCw, X } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { invitationsApi } from '@/lib/api/endpoints';
import { ApiError, errorMessage } from '@/lib/api/http';
import { keys } from '@/lib/api/queries';
import type { Invitation } from '@/lib/api/types';
import { formatAgo } from '@/lib/dates';
import { fullName } from '@/lib/domain';
import { emailSchema, nameSchema } from '@/features/auth/forms';
import { Button } from '@/components/ui/button';
import { Dialog, ConfirmDialog } from '@/components/ui/dialog';
import { Field, FormError } from '@/components/ui/field';
import { Input, Textarea } from '@/components/ui/input';
import { Menu, MenuItem } from '@/components/ui/menu';
import { Avatar, Badge } from '@/components/ui/misc';

const inviteSchema = z.object({
  firstName: nameSchema('their first name'),
  lastName: nameSchema('their last name'),
  email: emailSchema,
  message: z.string().trim().max(500, 'Keep the message under 500 characters.'),
});
type InviteForm = z.infer<typeof inviteSchema>;

function useInvalidateInvites() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: keys.invitations });
    void qc.invalidateQueries({ queryKey: keys.dashboard });
  };
}

export function InviteClientDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const invalidate = useInvalidateInvites();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<InviteForm>({ resolver: zodResolver(inviteSchema), defaultValues: { firstName: '', lastName: '', email: '', message: '' } });
  const { register, handleSubmit, formState, setError, reset } = form;

  const create = useMutation({
    mutationFn: (v: InviteForm) => invitationsApi.create({ ...v, ...(v.message ? { message: v.message } : {}) }),
    onSuccess: (inv) => {
      invalidate();
      toast.success(`Invitation sent to ${inv.email}`, { description: 'They will get an email with a link to join.' });
      reset();
      onOpenChange(false);
    },
    onError: (err) => {
      if (err instanceof ApiError && (err.code === 'Invitation.PendingExists' || err.code === 'User.EmailInUse')) {
        setError('email', { type: 'server', message: errorMessage(err) });
        return;
      }
      if (err instanceof ApiError) {
        let matched = false;
        for (const [field, message] of Object.entries(err.fieldErrors)) {
          if (field in inviteSchema.shape) {
            setError(field as keyof InviteForm, { type: 'server', message });
            matched = true;
          }
        }
        if (matched) return;
      }
      setFormError(errorMessage(err));
    },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        if (!o) setFormError(null);
        onOpenChange(o);
      }}
      title="Invite a client"
      description="They get an email with a link to set their password. Links expire after 7 days."
      footer={
        <>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" form="invite-form" loading={create.isPending}>
            <MailPlus aria-hidden />
            Send invitation
          </Button>
        </>
      }
    >
      <form
        id="invite-form"
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(e) =>
          void handleSubmit((v) => {
            setFormError(null);
            create.mutate(v);
          })(e)
        }
      >
        <FormError message={formError} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="First name" error={formState.errors.firstName?.message}>
            <Input autoComplete="off" {...register('firstName')} />
          </Field>
          <Field label="Last name" error={formState.errors.lastName?.message}>
            <Input autoComplete="off" {...register('lastName')} />
          </Field>
        </div>
        <Field label="Email" error={formState.errors.email?.message}>
          <Input type="email" autoComplete="off" inputMode="email" {...register('email')} />
        </Field>
        <Field label="Personal message" optional error={formState.errors.message?.message} hint="Shown on the invitation page.">
          <Textarea rows={3} {...register('message')} />
        </Field>
      </form>
    </Dialog>
  );
}

export function PendingInvitationList({ items }: { items: Invitation[] }) {
  const invalidate = useInvalidateInvites();
  const [cancelling, setCancelling] = useState<Invitation | null>(null);
  const resend = useMutation({
    mutationFn: (inv: Invitation) => invitationsApi.resend(inv.id),
    onSuccess: (inv) => {
      invalidate();
      toast.success(`New invitation sent to ${inv.email}`);
    },
    onError: (err) => toast.error(errorMessage(err)),
  });
  const cancel = useMutation({
    mutationFn: (inv: Invitation) => invitationsApi.cancel(inv.id),
    onSuccess: () => {
      invalidate();
      setCancelling(null);
      toast.success('Invitation cancelled');
    },
    onError: (err) => toast.error(errorMessage(err)),
  });

  return (
    <>
      <ul className="divide-y divide-border">
        {items.map((inv) => {
          const expired = inv.status === 'expired' || new Date(inv.expiresAt) < new Date();
          return (
            <li key={inv.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
              <Avatar name={inv} className="opacity-70" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{fullName(inv)}</p>
                <p className="truncate text-xs text-muted">
                  {inv.email} · {expired ? 'expired' : `sent ${formatAgo(inv.createdAt)}`}
                </p>
              </div>
              {expired && <Badge tone="warning">Expired</Badge>}
              <Menu
                trigger={
                  <Button variant="ghost" size="icon-sm" aria-label={`Actions for invitation to ${fullName(inv)}`}>
                    <MoreHorizontal />
                  </Button>
                }
              >
                <MenuItem icon={<RotateCw />} onSelect={() => resend.mutate(inv)}>
                  Resend invitation
                </MenuItem>
                <MenuItem icon={<X />} tone="danger" onSelect={() => setCancelling(inv)}>
                  Cancel invitation
                </MenuItem>
              </Menu>
            </li>
          );
        })}
      </ul>
      <ConfirmDialog
        open={cancelling !== null}
        onOpenChange={(o) => !o && setCancelling(null)}
        title="Cancel invitation?"
        description={cancelling ? `The link sent to ${cancelling.email} will stop working.` : null}
        confirmLabel="Cancel invitation"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling)}
      />
    </>
  );
}
