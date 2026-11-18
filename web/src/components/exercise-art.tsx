import { Dumbbell } from 'lucide-react';
import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';

// Bundled, trusted SVGs from the illustration set; loaded lazily per exercise and inlined so
// they pick up theme colours through CSS variables (--art-line, --art-fill, --art-accent).
const svgs = import.meta.glob<string>('/src/assets/exercises/*.svg', { query: '?raw', import: 'default' });

const cache = new Map<string, string>();

function loaderFor(file: string | null) {
  if (!file) return undefined;
  return svgs[`/src/assets/exercises/${file}`];
}

export function hasArt(file: string | null): boolean {
  return loaderFor(file) !== undefined;
}

interface ExerciseArtProps {
  illustration: string | null;
  name: string;
  className?: string;
  /** Decorative when the exercise name is already visible next to the art. */
  decorative?: boolean;
}

export function ExerciseArt({ illustration, name, className, decorative = true }: ExerciseArtProps) {
  const load = loaderFor(illustration);
  const [svg, setSvg] = useState<string | null>(() => (illustration ? (cache.get(illustration) ?? null) : null));

  useEffect(() => {
    if (!load || !illustration || cache.has(illustration)) return;
    let cancelled = false;
    void load().then((text) => {
      cache.set(illustration, text);
      if (!cancelled) setSvg(text);
    });
    return () => {
      cancelled = true;
    };
  }, [load, illustration]);

  const a11y = decorative ? { 'aria-hidden': true } : { role: 'img', 'aria-label': `${name} illustration` };
  const shown = svg ?? (illustration ? cache.get(illustration) : undefined);
  return (
    <div className={cn('exercise-art flex aspect-square items-center justify-center overflow-hidden rounded-lg bg-surface-2 text-muted', className)} {...a11y}>
      {shown ? (
        <div className="size-full [&>svg]:size-full" dangerouslySetInnerHTML={{ __html: shown }} />
      ) : (
        <Dumbbell className="size-1/3 opacity-40" aria-hidden />
      )}
    </div>
  );
}
