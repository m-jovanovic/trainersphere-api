import { Minus, Plus } from 'lucide-react';
import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';

interface NumberStepperProps {
  label: string;
  value: number | null;
  onChange: (value: number | null) => void;
  step: number;
  min?: number;
  max?: number;
  /** Shown after the number, e.g. "kg". */
  suffix?: string;
  size?: 'md' | 'lg';
  disabled?: boolean;
  className?: string;
}

const decimals = (n: number) => (String(n).split('.')[1] ?? '').length;

/** Big-target numeric input with -/+ buttons; typing is allowed for jumps. */
export function NumberStepper({ label, value, onChange, step, min = 0, max = 10_000, suffix, size = 'md', disabled, className }: NumberStepperProps) {
  const [text, setText] = useState(value === null ? '' : String(value));
  useEffect(() => setText(value === null ? '' : String(value)), [value]);

  const clamp = (n: number) => Math.min(max, Math.max(min, Number(n.toFixed(Math.max(2, decimals(step))))));
  const bump = (dir: 1 | -1) => onChange(clamp((value ?? 0) + dir * step));
  const commit = () => {
    const normalized = text.replace(',', '.').trim();
    if (normalized === '') return onChange(null);
    const n = Number(normalized);
    if (Number.isFinite(n)) onChange(clamp(n));
    else setText(value === null ? '' : String(value));
  };

  const btn = cn(
    'flex shrink-0 items-center justify-center rounded-lg bg-surface-2 text-fg active:bg-surface-3 disabled:opacity-40',
    size === 'lg' ? 'size-14 [&_svg]:size-6' : 'size-11 [&_svg]:size-5',
  );
  return (
    <div className={cn('flex items-center gap-1.5', className)} role="group" aria-label={label}>
      <button type="button" className={btn} onClick={() => bump(-1)} disabled={disabled || (value ?? 0) <= min} aria-label={`Decrease ${label}`}>
        <Minus aria-hidden />
      </button>
      <div className="relative min-w-0 flex-1">
        <input
          inputMode="decimal"
          aria-label={label}
          value={text}
          disabled={disabled}
          onChange={(e) => setText(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') e.currentTarget.blur();
            if (e.key === 'ArrowUp') {
              e.preventDefault();
              bump(1);
            }
            if (e.key === 'ArrowDown') {
              e.preventDefault();
              bump(-1);
            }
          }}
          onFocus={(e) => e.currentTarget.select()}
          className={cn(
            'tabular w-full rounded-lg border border-border-strong bg-surface text-center font-semibold text-fg focus-visible:outline-2 focus-visible:outline-offset-0 focus-visible:outline-ring',
            size === 'lg' ? 'h-14 text-2xl' : 'h-11 text-lg',
            suffix && 'pr-8',
          )}
        />
        {suffix && <span className="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2 text-xs text-muted">{suffix}</span>}
      </div>
      <button type="button" className={btn} onClick={() => bump(1)} disabled={disabled || (value ?? 0) >= max} aria-label={`Increase ${label}`}>
        <Plus aria-hidden />
      </button>
    </div>
  );
}
