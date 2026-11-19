import { useBlocker } from '@tanstack/react-router';
import { ConfirmDialog } from '@/components/ui/dialog';

/** Asks before leaving a page with unsaved edits (in-app navigation and tab close). */
export function UnsavedChangesGuard({ when }: { when: boolean }) {
  const blocker = useBlocker({ shouldBlockFn: () => when, enableBeforeUnload: () => when, withResolver: true });
  return (
    <ConfirmDialog
      open={blocker.status === 'blocked'}
      onOpenChange={(o) => !o && blocker.reset?.()}
      title="Discard unsaved changes?"
      description="You have edits that are not saved yet. Leaving this page throws them away."
      confirmLabel="Discard changes"
      onConfirm={() => blocker.proceed?.()}
    />
  );
}
