import { zodResolver } from '@hookform/resolvers/zod';
import { Link, createFileRoute } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { MailCheck } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { authApi } from '@/lib/api/endpoints';
import { errorMessage } from '@/lib/api/http';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { AuthCard } from '@/features/auth/auth-layout';
import { emailSchema } from '@/features/auth/forms';

const schema = z.object({ email: emailSchema });

export const Route = createFileRoute('/_public/forgot-password')({
  head: () => ({ meta: [{ title: 'Reset password · TrainerSphere' }] }),
  component: ForgotPasswordPage,
});

function ForgotPasswordPage() {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: { email: '' } });
  const request = useMutation({ mutationFn: (email: string) => authApi.forgotPassword(email), onError: () => undefined });
  const back = (
    <Link to="/login" className="font-medium text-primary hover:underline">
      Back to log in
    </Link>
  );

  if (request.isSuccess) {
    return (
      <AuthCard title="Check your email" footer={back}>
        <div className="flex gap-3 text-sm text-muted">
          <MailCheck className="size-5 shrink-0 text-primary" aria-hidden />
          <p role="status">
            If an account exists for <strong className="text-fg">{form.getValues('email')}</strong>, we sent a link to reset the password. It is valid for one hour.
          </p>
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Forgot your password?" description="Enter your email and we will send you a reset link." footer={back}>
      <form noValidate className="flex flex-col gap-4" onSubmit={form.handleSubmit((v) => request.mutate(v.email))}>
        <FormError message={request.error ? errorMessage(request.error) : null} />
        <Field label="Email" error={form.formState.errors.email?.message}>
          <Input type="email" autoComplete="email" inputMode="email" autoFocus {...form.register('email')} />
        </Field>
        <Button type="submit" size="lg" loading={request.isPending}>
          Send reset link
        </Button>
      </form>
    </AuthCard>
  );
}
