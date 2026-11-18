import { ToggleGroup } from 'radix-ui';
import { cn } from '@/lib/utils';

interface Option<V extends string> {
  value: V;
  label: string;
}

/** Single-select segmented control. */
export function Segmented<V extends string>({ value, onChange, options, label, className }: { value: V; onChange: (v: V) => void; options: Option<V>[]; label: string; className?: string }) {
  return (
    <ToggleGroup.Root
      type="single"
      value={value}
      onValueChange={(v) => v && onChange(v as V)}
      aria-label={label}
      className={cn('inline-flex rounded-lg border border-border bg-surface-2 p-0.5', className)}
    >
      {options.map((o) => (
        <ToggleGroup.Item
          key={o.value}
          value={o.value}
          className="h-9 rounded-md px-3 text-sm font-medium text-muted data-[state=on]:bg-surface data-[state=on]:text-fg data-[state=on]:shadow-sm"
        >
          {o.label}
        </ToggleGroup.Item>
      ))}
    </ToggleGroup.Root>
  );
}

/** Multi-select chips, used for filters and taxonomy pickers. */
export function ChipGroup({ value, onChange, options, label, className }: { value: string[]; onChange: (v: string[]) => void; options: Option<string>[]; label: string; className?: string }) {
  return (
    <ToggleGroup.Root type="multiple" value={value} onValueChange={onChange} aria-label={label} className={cn('flex flex-wrap gap-1.5', className)}>
      {options.map((o) => (
        <ToggleGroup.Item
          key={o.value}
          value={o.value}
          className="h-9 rounded-full border border-border px-3 text-sm text-muted transition-colors hover:border-border-strong hover:text-fg data-[state=on]:border-primary data-[state=on]:bg-primary-soft data-[state=on]:text-primary-soft-fg"
        >
          {o.label}
        </ToggleGroup.Item>
      ))}
    </ToggleGroup.Root>
  );
}
