import { zodResolver } from '@hookform/resolvers/zod';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { authApi } from '@/lib/api/endpoints';
import { session } from '@/lib/auth/session';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { AuthCard } from '@/features/auth/auth-layout';
import { PasswordInput } from '@/features/auth/password-input';
import { applyServerErrors, emailSchema, safeRedirect } from '@/features/auth/forms';

const schema = z.object({ email: emailSchema, password: z.string().min(1, 'Enter your password.') });
type Values = z.infer<typeof schema>;

export const Route = createFileRoute('/_public/login')({
  validateSearch: z.object({ redirect: z.string().optional() }),
  head: () => ({ meta: [{ title: 'Log in · TrainerSphere' }] }),
  component: LoginPage,
});

function LoginPage() {
  const { redirect } = Route.useSearch();
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { email: '', password: '' } });
  const login = useMutation({
    mutationFn: authApi.login,
    onSuccess: async (res) => {
      session.set(res);
      await navigate({ to: safeRedirect(redirect), replace: true });
    },
    onError: (err) => setFormError(applyServerErrors(err, form.setError, ['email', 'password'])),
  });
  const { errors } = form.formState;

  return (
    <AuthCard
      title="Log in"
      description="Welcome back."
      footer={
        <>
          New trainer?{' '}
          <Link to="/signup" className="font-medium text-primary hover:underline">
            Create an account
          </Link>
        </>
      }
    >
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={form.handleSubmit((v) => {
          setFormError(null);
          login.mutate(v);
        })}
      >
        <FormError message={formError} />
        <Field label="Email" error={errors.email?.message}>
          <Input type="email" autoComplete="email" inputMode="email" autoFocus {...form.register('email')} />
        </Field>
        <Field label="Password" error={errors.password?.message}>
          <PasswordInput autoComplete="current-password" {...form.register('password')} />
        </Field>
        <div className="-mt-1 text-right">
          <Link to="/forgot-password" className="text-sm text-primary hover:underline">
            Forgot password?
          </Link>
        </div>
        <Button type="submit" size="lg" loading={login.isPending}>
          Log in
        </Button>
      </form>
    </AuthCard>
  );
}
