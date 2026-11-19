import type { ComponentProps, ReactNode } from 'react';
import { cn } from '@/lib/utils';

export function Card({ className, ...props }: ComponentProps<'section'>) {
  return <section className={cn('rounded-card border border-border bg-surface', className)} {...props} />;
}

export function CardHeader({ title, action, description, className }: { title: ReactNode; action?: ReactNode; description?: ReactNode; className?: string }) {
  return (
    <div className={cn('flex items-start justify-between gap-3 px-4 pt-4 sm:px-5', className)}>
      <div className="min-w-0">
        <h2 className="text-base font-semibold">{title}</h2>
        {description && <p className="mt-0.5 text-sm text-muted">{description}</p>}
      </div>
      {action}
    </div>
  );
}

export function CardBody({ className, ...props }: ComponentProps<'div'>) {
  return <div className={cn('p-4 sm:p-5', className)} {...props} />;
}

const badgeTones = {
  neutral: 'bg-surface-2 text-muted border-border',
  primary: 'bg-primary-soft text-primary-soft-fg border-transparent',
  pr: 'bg-pr-soft text-pr border-transparent',
  danger: 'bg-danger-soft text-danger border-transparent',
  // Amber belongs to personal records; warnings use a neutral outline.
  warning: 'bg-surface text-fg border-border-strong',
};

export function Badge({ tone = 'neutral', className, ...props }: ComponentProps<'span'> & { tone?: keyof typeof badgeTones }) {
  return (
    <span
      className={cn('inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium whitespace-nowrap [&_svg]:size-3', badgeTones[tone], className)}
      {...props}
    />
  );
}

export function Skeleton({ className, ...props }: ComponentProps<'div'>) {
  return <div aria-hidden className={cn('animate-pulse rounded-md bg-surface-2', className)} {...props} />;
}

export function EmptyState({ icon, title, description, action, className }: { icon?: ReactNode; title: string; description?: ReactNode; action?: ReactNode; className?: string }) {
  return (
    <div className={cn('flex flex-col items-center justify-center gap-2 px-6 py-10 text-center', className)}>
      {icon && <div className="mb-1 text-muted [&_svg]:size-8">{icon}</div>}
      <p className="font-medium">{title}</p>
      {description && <p className="max-w-sm text-sm text-muted">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function PageHeader({ title, description, actions, back }: { title: ReactNode; description?: ReactNode; actions?: ReactNode; back?: ReactNode }) {
  return (
    <header className="mb-5 flex flex-col gap-3 sm:mb-6 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        {back}
        <h1 className="truncate text-2xl font-semibold tracking-tight">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </header>
  );
}

export function Avatar({ name, className }: { name: { firstName: string; lastName: string }; className?: string }) {
  const text = `${name.firstName.charAt(0)}${name.lastName.charAt(0)}`.toUpperCase();
  // Stable hue per person so the same client is recognisable across screens.
  const hue = Array.from(`${name.firstName}${name.lastName}`).reduce((h, c) => (h * 31 + c.charCodeAt(0)) % 360, 7);
  return (
    <span
      aria-hidden
      className={cn('inline-flex size-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold text-white', className)}
      style={{ backgroundColor: `oklch(0.55 0.12 ${hue})` }}
    >
      {text}
    </span>
  );
}

export function Stat({ label, value, hint, className }: { label: string; value: ReactNode; hint?: ReactNode; className?: string }) {
  return (
    <div className={cn('flex flex-col gap-0.5', className)}>
      <span className="text-xs font-medium text-muted uppercase tracking-wide">{label}</span>
      <span className="tabular text-2xl font-semibold">{value}</span>
      {hint && <span className="text-xs text-muted">{hint}</span>}
    </div>
  );
}

export function VisuallyHidden({ children }: { children: ReactNode }) {
  return <span className="sr-only">{children}</span>;
}
