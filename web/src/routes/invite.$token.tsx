import { zodResolver } from '@hookform/resolvers/zod';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { format, parseISO } from 'date-fns';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { authApi } from '@/lib/api/endpoints';
import { isApiError } from '@/lib/api/http';
import { session } from '@/lib/auth/session';
import { browserTimeZone } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/misc';
import { AuthCard, AuthLayout } from '@/features/auth/auth-layout';
import { PasswordInput } from '@/features/auth/password-input';
import { applyServerErrors, nameSchema, passwordSchema } from '@/features/auth/forms';

const schema = z
  .object({ firstName: nameSchema('your first name'), lastName: nameSchema('your last name'), password: passwordSchema, confirm: z.string() })
  .refine((v) => v.password === v.confirm, { path: ['confirm'], message: 'Passwords do not match.' });
type Values = z.infer<typeof schema>;

export const Route = createFileRoute('/invite/$token')({
  head: () => ({ meta: [{ title: 'Accept invitation · TrainerSphere' }] }),
  component: InvitePage,
});

function InvitePage() {
  const { token } = Route.useParams();
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);
  const preview = useQuery({ queryKey: ['invitation-preview', token], queryFn: () => authApi.previewInvitation(token), retry: false });
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { firstName: '', lastName: '', password: '', confirm: '' } });
  const { reset } = form;
  useEffect(() => {
    if (preview.data) reset({ firstName: preview.data.firstName, lastName: preview.data.lastName, password: '', confirm: '' });
  }, [preview.data, reset]);

  const accept = useMutation({
    mutationFn: (v: Values) => authApi.acceptInvitation(token, { firstName: v.firstName, lastName: v.lastName, password: v.password, timeZone: browserTimeZone() }),
    onSuccess: async (res) => {
      session.set(res);
      await navigate({ to: '/', replace: true });
    },
    onError: (err) => setFormError(applyServerErrors(err, form.setError, ['firstName', 'lastName', 'password'])),
  });

  const content = () => {
    if (preview.isPending) {
      return (
        <AuthCard title="Loading invitation…">
          <div className="flex flex-col gap-3" aria-hidden>
            <Skeleton className="h-11" />
            <Skeleton className="h-11" />
            <Skeleton className="h-11" />
          </div>
        </AuthCard>
      );
    }
    if (preview.isError) {
      const notPending = isApiError(preview.error, 409);
      return (
        <AuthCard title={notPending ? 'Invitation no longer valid' : 'Invitation not found'}>
          <p className="text-sm text-muted">
            {notPending
              ? 'This invitation was already used, cancelled or has expired. If you already set a password, log in instead. Otherwise ask your trainer for a new invite.'
              : 'Check the link from your email, or ask your trainer to send a new invite.'}
          </p>
          <Button asChild variant="secondary" className="mt-5 w-full">
            <Link to="/login">Go to log in</Link>
          </Button>
        </AuthCard>
      );
    }
    const inv = preview.data;
    const { errors } = form.formState;
    return (
      <AuthCard
        title={`Join ${inv.trainerName} on TrainerSphere`}
        description={
          <>
            Set a password for <strong className="text-fg">{inv.email}</strong>. The invite expires {format(parseISO(inv.expiresAt), 'd MMM yyyy')}.
          </>
        }
      >
        {inv.message && <blockquote className="mb-5 rounded-lg border-l-2 border-primary bg-surface-2 px-3 py-2 text-sm">“{inv.message}”</blockquote>}
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={form.handleSubmit((v) => {
            setFormError(null);
            accept.mutate(v);
          })}
        >
          <FormError message={formError} />
          <div className="grid grid-cols-2 gap-3">
            <Field label="First name" error={errors.firstName?.message}>
              <Input autoComplete="given-name" {...form.register('firstName')} />
            </Field>
            <Field label="Last name" error={errors.lastName?.message}>
              <Input autoComplete="family-name" {...form.register('lastName')} />
            </Field>
          </div>
          <Field label="Password" error={errors.password?.message} hint="At least 8 characters.">
            <PasswordInput autoComplete="new-password" autoFocus {...form.register('password')} />
          </Field>
          <Field label="Confirm password" error={errors.confirm?.message}>
            <PasswordInput autoComplete="new-password" {...form.register('confirm')} />
          </Field>
          <Button type="submit" size="lg" loading={accept.isPending}>
            Accept and continue
          </Button>
        </form>
      </AuthCard>
    );
  };

  return <AuthLayout>{content()}</AuthLayout>;
}
