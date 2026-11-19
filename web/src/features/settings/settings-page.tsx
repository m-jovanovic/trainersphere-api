import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { authApi } from '@/lib/api/endpoints';
import { useUser } from '@/lib/auth/hooks';
import { session } from '@/lib/auth/session';
import { browserTimeZone } from '@/lib/domain';
import { prefKeys, useBoolPref } from '@/lib/prefs';
import { useTheme, type ThemePref } from '@/lib/theme';
import { applyServerErrors, nameSchema, passwordSchema } from '@/features/auth/forms';
import { Button } from '@/components/ui/button';
import { Segmented } from '@/components/ui/chips';
import { Field, FormError } from '@/components/ui/field';
import { Input, NativeSelect } from '@/components/ui/input';
import { Card, CardBody, CardHeader, PageHeader } from '@/components/ui/misc';

const COMMON_ZONES = [
  'UTC',
  'Europe/London',
  'Europe/Dublin',
  'Europe/Berlin',
  'Europe/Amsterdam',
  'Europe/Paris',
  'Europe/Madrid',
  'Europe/Rome',
  'Europe/Stockholm',
  'Europe/Oslo',
  'Europe/Helsinki',
  'Europe/Athens',
  'Europe/Istanbul',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'America/Toronto',
  'America/Vancouver',
  'America/Mexico_City',
  'America/Sao_Paulo',
  'America/Argentina/Buenos_Aires',
  'Africa/Cairo',
  'Africa/Johannesburg',
  'Asia/Dubai',
  'Asia/Kolkata',
  'Asia/Bangkok',
  'Asia/Singapore',
  'Asia/Shanghai',
  'Asia/Tokyo',
  'Asia/Seoul',
  'Australia/Sydney',
  'Australia/Melbourne',
  'Pacific/Auckland',
];

function zoneOptions(current: string): string[] {
  return [...new Set([current, browserTimeZone(), ...COMMON_ZONES])].filter(Boolean);
}

export function SettingsPage() {
  return (
    <>
      <PageHeader title="Settings" />
      <div className="flex max-w-2xl flex-col gap-4 sm:gap-6">
        <ProfileCard />
        <PasswordCard />
        <AppearanceCard />
        <WorkoutPrefsCard />
      </div>
    </>
  );
}

const profileSchema = z.object({
  firstName: nameSchema('your first name'),
  lastName: nameSchema('your last name'),
  units: z.enum(['kg', 'lb']),
  timeZone: z.string().min(1, 'Pick a time zone.'),
});
type ProfileValues = z.infer<typeof profileSchema>;

function ProfileCard() {
  const user = useUser();
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, formState, setError } = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    values: { firstName: user.firstName, lastName: user.lastName, units: user.units, timeZone: user.timeZone },
  });
  const save = useMutation({
    // PUT is a full replace, so every member goes along, changed or not.
    mutationFn: (v: ProfileValues) => authApi.updateMe({ firstName: v.firstName.trim(), lastName: v.lastName.trim(), units: v.units, timeZone: v.timeZone }),
    onSuccess: (saved) => {
      session.updateUser(saved);
      toast.success('Profile saved');
    },
    onError: (err) => setFormError(applyServerErrors(err, setError, ['firstName', 'lastName', 'units', 'timeZone'])),
  });
  return (
    <Card>
      <CardHeader title="Profile" description={user.email} />
      <CardBody>
        <form noValidate className="flex flex-col gap-4" onSubmit={(e) => void handleSubmit((v) => save.mutate(v))(e)}>
          <FormError message={formError} />
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="First name" error={formState.errors.firstName?.message}>
              <Input autoComplete="given-name" {...register('firstName')} />
            </Field>
            <Field label="Last name" error={formState.errors.lastName?.message}>
              <Input autoComplete="family-name" {...register('lastName')} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Units" error={formState.errors.units?.message} hint="Weights and distances across the app.">
              <NativeSelect {...register('units')}>
                <option value="kg">Kilograms (kg)</option>
                <option value="lb">Pounds (lb)</option>
              </NativeSelect>
            </Field>
            <Field label="Time zone" error={formState.errors.timeZone?.message} hint="Decides which day a workout counts for.">
              <NativeSelect {...register('timeZone')}>
                {zoneOptions(user.timeZone).map((tz) => (
                  <option key={tz} value={tz}>
                    {tz.replace(/_/g, ' ')}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          </div>
          <div>
            <Button type="submit" loading={save.isPending} disabled={!formState.isDirty}>
              Save profile
            </Button>
          </div>
        </form>
      </CardBody>
    </Card>
  );
}

const passwordFormSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: passwordSchema,
    confirm: z.string(),
  })
  .refine((v) => v.confirm === v.newPassword, { path: ['confirm'], message: 'Passwords do not match.' });
type PasswordValues = z.infer<typeof passwordFormSchema>;

function PasswordCard() {
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, formState, setError, reset } = useForm<PasswordValues>({
    resolver: zodResolver(passwordFormSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirm: '' },
  });
  const change = useMutation({
    mutationFn: (v: PasswordValues) => authApi.changePassword({ currentPassword: v.currentPassword, newPassword: v.newPassword }),
    onSuccess: () => {
      reset();
      toast.success('Password updated');
    },
    onError: (err) => setFormError(applyServerErrors(err, setError, ['currentPassword', 'newPassword'])),
  });
  return (
    <Card>
      <CardHeader title="Change password" />
      <CardBody>
        <form noValidate className="flex flex-col gap-4" onSubmit={(e) => void handleSubmit((v) => change.mutate(v))(e)}>
          <FormError message={formError} />
          <Field label="Current password" error={formState.errors.currentPassword?.message}>
            <Input type="password" autoComplete="current-password" {...register('currentPassword')} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="New password" error={formState.errors.newPassword?.message} hint="At least 8 characters.">
              <Input type="password" autoComplete="new-password" {...register('newPassword')} />
            </Field>
            <Field label="Confirm new password" error={formState.errors.confirm?.message}>
              <Input type="password" autoComplete="new-password" {...register('confirm')} />
            </Field>
          </div>
          <div>
            <Button type="submit" loading={change.isPending}>
              Change password
            </Button>
          </div>
        </form>
      </CardBody>
    </Card>
  );
}

function AppearanceCard() {
  const [theme, setTheme] = useTheme();
  return (
    <Card>
      <CardHeader title="Appearance" />
      <CardBody>
        <Segmented<ThemePref>
          label="Theme"
          value={theme}
          onChange={setTheme}
          options={[
            { value: 'light', label: 'Light' },
            { value: 'dark', label: 'Dark' },
            { value: 'system', label: 'System' },
          ]}
        />
      </CardBody>
    </Card>
  );
}

function WorkoutPrefsCard() {
  const [sound, setSound] = useBoolPref(prefKeys.restSound, true);
  const [vibrate, setVibrate] = useBoolPref(prefKeys.restVibrate, true);
  return (
    <Card>
      <CardHeader title="Workout" description="Rest timer alerts on this device." />
      <CardBody className="flex flex-col gap-2">
        <PrefToggle label="Sound" description="Chime when the rest timer ends." checked={sound} onChange={setSound} />
        <PrefToggle label="Vibration" description="Vibrate when the rest timer ends, on devices that support it." checked={vibrate} onChange={setVibrate} />
      </CardBody>
    </Card>
  );
}

function PrefToggle({ label, description, checked, onChange }: { label: string; description: string; checked: boolean; onChange: (v: boolean) => void }) {
  const id = useId();
  return (
    <div className="flex min-h-11 items-center justify-between gap-4 rounded-lg border border-border px-3 py-2">
      <label htmlFor={id} className="min-w-0 flex-1 cursor-pointer">
        <span className="block text-sm font-medium">{label}</span>
        <span className="block text-xs text-muted">{description}</span>
      </label>
      <input id={id} type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="size-5 shrink-0 accent-primary" />
    </div>
  );
}
