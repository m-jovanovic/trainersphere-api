import { DropdownMenu as M } from 'radix-ui';
import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export function Menu({ trigger, children, align = 'end' }: { trigger: ReactNode; children: ReactNode; align?: 'start' | 'end' }) {
  return (
    <M.Root>
      <M.Trigger asChild>{trigger}</M.Trigger>
      <M.Portal>
        <M.Content
          align={align}
          sideOffset={6}
          className="z-50 min-w-48 rounded-xl border border-border bg-surface p-1 shadow-lg"
        >
          {children}
        </M.Content>
      </M.Portal>
    </M.Root>
  );
}

export function MenuItem({ children, onSelect, tone, icon }: { children: ReactNode; onSelect: () => void; tone?: 'danger'; icon?: ReactNode }) {
  return (
    <M.Item
      onSelect={onSelect}
      className={cn(
        'flex h-10 cursor-default items-center gap-2 rounded-lg px-3 text-sm outline-none select-none data-[highlighted]:bg-surface-2 [&_svg]:size-4',
        tone === 'danger' && 'text-danger',
      )}
    >
      {icon}
      {children}
    </M.Item>
  );
}

export function MenuLabel({ children }: { children: ReactNode }) {
  return <M.Label className="px-3 py-1.5 text-xs text-muted">{children}</M.Label>;
}

export const MenuSeparator = () => <M.Separator className="my-1 h-px bg-border" />;
