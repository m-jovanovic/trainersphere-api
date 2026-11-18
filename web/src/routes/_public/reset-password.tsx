import { zodResolver } from '@hookform/resolvers/zod';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { authApi } from '@/lib/api/endpoints';
import { ApiError } from '@/lib/api/http';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { AuthCard } from '@/features/auth/auth-layout';
import { PasswordInput } from '@/features/auth/password-input';
import { applyServerErrors, passwordSchema } from '@/features/auth/forms';

const schema = z
  .object({ newPassword: passwordSchema, confirm: z.string() })
  .refine((v) => v.newPassword === v.confirm, { path: ['confirm'], message: 'Passwords do not match.' });
type Values = z.infer<typeof schema>;

export const Route = createFileRoute('/_public/reset-password')({
  validateSearch: z.object({ token: z.string().optional() }),
  head: () => ({ meta: [{ title: 'Choose a new password · TrainerSphere' }] }),
  component: ResetPasswordPage,
});

function ResetPasswordPage() {
  const { token } = Route.useSearch();
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { newPassword: '', confirm: '' } });
  const reset = useMutation({
    mutationFn: (v: Values) => authApi.resetPassword({ token: token ?? '', newPassword: v.newPassword }),
    onSuccess: async () => {
      toast.success('Password updated. Log in with your new password.');
      await navigate({ to: '/login', replace: true });
    },
    onError: (err) => {
      const tokenError = err instanceof ApiError ? err.fieldErrors.token : undefined;
      const other = applyServerErrors(err, form.setError, ['newPassword']);
      setFormError(tokenError ? `${tokenError} Request a new link below.` : other);
    },
  });

  if (!token) {
    return (
      <AuthCard title="Reset link missing">
        <p className="text-sm text-muted">
          Open the link from your email again, or{' '}
          <Link to="/forgot-password" className="font-medium text-primary hover:underline">
            request a new one
          </Link>
          .
        </p>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title="Choose a new password"
      footer={
        <Link to="/forgot-password" className="font-medium text-primary hover:underline">
          Request a new link
        </Link>
      }
    >
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={form.handleSubmit((v) => {
          setFormError(null);
          reset.mutate(v);
        })}
      >
        <FormError message={formError} />
        <Field label="New password" error={form.formState.errors.newPassword?.message} hint="At least 8 characters.">
          <PasswordInput autoComplete="new-password" autoFocus {...form.register('newPassword')} />
        </Field>
        <Field label="Confirm password" error={form.formState.errors.confirm?.message}>
          <PasswordInput autoComplete="new-password" {...form.register('confirm')} />
        </Field>
        <Button type="submit" size="lg" loading={reset.isPending}>
          Update password
        </Button>
      </form>
    </AuthCard>
  );
}
