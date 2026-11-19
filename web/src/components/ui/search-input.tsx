import { Search, X } from 'lucide-react';
import { useState } from 'react';
import { useDebouncedCallback } from '@/lib/use-debounced';
import { cn } from '@/lib/utils';
import { Input } from './input';

interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  label: string;
  placeholder?: string;
  className?: string;
}

/** Search box that commits after a short pause so URL-driven queries do not fire per keystroke. */
export function SearchInput({ value, onChange, label, placeholder, className }: SearchInputProps) {
  const [text, setText] = useState(value);
  const [committed, setCommitted] = useState(value);
  // Follow external changes (back button, cleared filters) without clobbering typing.
  if (committed !== value) {
    setCommitted(value);
    setText(value);
  }
  const commit = useDebouncedCallback((v: string) => onChange(v.trim()), 250);
  return (
    <div className={cn('relative', className)}>
      <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
      <Input
        type="search"
        aria-label={label}
        placeholder={placeholder ?? label}
        value={text}
        onChange={(e) => {
          setText(e.target.value);
          commit(e.target.value);
        }}
        onKeyDown={(e) => {
          if (e.key === 'Escape' && text) {
            setText('');
            onChange('');
          }
        }}
        className="pr-10 pl-9 [&::-webkit-search-cancel-button]:hidden"
      />
      {text && (
        <button
          type="button"
          aria-label="Clear search"
          onClick={() => {
            setText('');
            onChange('');
          }}
          className="absolute top-1/2 right-1 flex size-9 -translate-y-1/2 items-center justify-center rounded-md text-muted hover:text-fg"
        >
          <X className="size-4" aria-hidden />
        </button>
      )}
    </div>
  );
}
