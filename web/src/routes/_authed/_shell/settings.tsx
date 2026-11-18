import { createFileRoute } from '@tanstack/react-router';
import { SettingsPage } from '@/features/settings/settings-page';

export const Route = createFileRoute('/_authed/_shell/settings')({
  head: () => ({ meta: [{ title: 'Settings · TrainerSphere' }] }),
  component: SettingsPage,
});
