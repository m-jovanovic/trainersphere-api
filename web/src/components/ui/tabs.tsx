import { Tabs as T } from 'radix-ui';
import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export const Tabs = T.Root;

export function TabsList({ children, className, label }: { children: ReactNode; className?: string; label: string }) {
  return (
    <T.List aria-label={label} className={cn('-mx-1 flex gap-1 overflow-x-auto border-b border-border px-1', className)}>
      {children}
    </T.List>
  );
}

export function TabsTrigger({ value, children }: { value: string; children: ReactNode }) {
  return (
    <T.Trigger
      value={value}
      className="relative h-11 shrink-0 px-3 text-sm font-medium text-muted transition-colors hover:text-fg data-[state=active]:text-fg data-[state=active]:after:absolute data-[state=active]:after:inset-x-2 data-[state=active]:after:-bottom-px data-[state=active]:after:h-0.5 data-[state=active]:after:rounded-full data-[state=active]:after:bg-primary"
    >
      {children}
    </T.Trigger>
  );
}

export function TabsContent({ value, children, className }: { value: string; children: ReactNode; className?: string }) {
  return (
    <T.Content value={value} className={cn('pt-5 focus-visible:outline-none', className)}>
      {children}
    </T.Content>
  );
}
