import { zodResolver } from '@hookform/resolvers/zod';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { authApi } from '@/lib/api/endpoints';
import { isApiError } from '@/lib/api/http';
import { session } from '@/lib/auth/session';
import { browserTimeZone } from '@/lib/domain';
import { Button } from '@/components/ui/button';
import { Field, FormError } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { AuthCard } from '@/features/auth/auth-layout';
import { PasswordInput } from '@/features/auth/password-input';
import { applyServerErrors, emailSchema, nameSchema, passwordSchema } from '@/features/auth/forms';

const schema = z.object({
  firstName: nameSchema('your first name'),
  lastName: nameSchema('your last name'),
  email: emailSchema,
  password: passwordSchema,
});
type Values = z.infer<typeof schema>;
const fields = ['firstName', 'lastName', 'email', 'password'] as const;

export const Route = createFileRoute('/_public/signup')({
  head: () => ({ meta: [{ title: 'Create trainer account · TrainerSphere' }] }),
  component: SignUpPage,
});

function SignUpPage() {
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { firstName: '', lastName: '', email: '', password: '' } });
  const signUp = useMutation({
    mutationFn: (v: Values) => authApi.signUp({ ...v, timeZone: browserTimeZone() }),
    onSuccess: async (res) => {
      session.set(res);
      await navigate({ to: '/', replace: true });
    },
    onError: (err) => {
      if (isApiError(err, 409)) {
        form.setError('email', { type: 'server', message: 'An account with this email already exists.' });
        return;
      }
      setFormError(applyServerErrors(err, form.setError, fields));
    },
  });
  const { errors } = form.formState;

  return (
    <AuthCard
      title="Create your trainer account"
      description="Manage clients, build programs and track progress in one place."
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-primary hover:underline">
            Log in
          </Link>
          <p className="mt-2 text-xs">Clients join through an invite from their trainer.</p>
        </>
      }
    >
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={form.handleSubmit((v) => {
          setFormError(null);
          signUp.mutate(v);
        })}
      >
        <FormError message={formError} />
        <div className="grid grid-cols-2 gap-3">
          <Field label="First name" error={errors.firstName?.message}>
            <Input autoComplete="given-name" autoFocus {...form.register('firstName')} />
          </Field>
          <Field label="Last name" error={errors.lastName?.message}>
            <Input autoComplete="family-name" {...form.register('lastName')} />
          </Field>
        </div>
        <Field label="Email" error={errors.email?.message}>
          <Input type="email" autoComplete="email" inputMode="email" {...form.register('email')} />
        </Field>
        <Field label="Password" error={errors.password?.message} hint="At least 8 characters.">
          <PasswordInput autoComplete="new-password" {...form.register('password')} />
        </Field>
        <Button type="submit" size="lg" loading={signUp.isPending}>
          Create account
        </Button>
      </form>
    </AuthCard>
  );
}
