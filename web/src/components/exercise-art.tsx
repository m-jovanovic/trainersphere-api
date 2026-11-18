import { Dumbbell } from 'lucide-react';
import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';

// Bundled, trusted SVGs from the illustration set, loaded lazily per exercise and inlined (an <img>
// cannot see page CSS variables) so they follow the theme through the --ex-* variables in styles.css.
const svgs = import.meta.glob<string>('/src/assets/exercises/*.svg', { query: '?raw', import: 'default' });

const cache = new Map<string, string>();

/** `illustration` is always the detail file; the thumbnail variant swaps `.svg` for `-sm.svg`. */
function fileFor(illustration: string | null, variant: 'sm' | 'detail'): string | null {
  if (!illustration) return null;
  return variant === 'sm' ? illustration.replace(/\.svg$/, '-sm.svg') : illustration;
}

function loaderFor(file: string | null) {
  if (!file) return undefined;
  return svgs[`/src/assets/exercises/${file}`];
}

interface ExerciseArtProps {
  illustration: string | null;
  name: string;
  /** `sm` is drawn for 32-64px (thicker lines, primary muscles only); `detail` for 96px and up. */
  variant?: 'sm' | 'detail';
  className?: string;
  /** Decorative when the exercise name is already visible next to the art. */
  decorative?: boolean;
}

export function ExerciseArt({ illustration, name, variant = 'detail', className, decorative = true }: ExerciseArtProps) {
  const requested = fileFor(illustration, variant);
  // Fall back to the detail drawing if a thumbnail is missing.
  const file = loaderFor(requested) ? requested : fileFor(illustration, 'detail');
  const load = loaderFor(file);
  const [loaded, setLoaded] = useState<{ file: string; svg: string } | null>(null);

  useEffect(() => {
    if (!load || !file || cache.has(file)) return;
    let cancelled = false;
    void load().then((text) => {
      cache.set(file, text);
      if (!cancelled) setLoaded({ file, svg: text });
    });
    return () => {
      cancelled = true;
    };
  }, [load, file]);

  const svg = file ? (cache.get(file) ?? (loaded?.file === file ? loaded.svg : undefined)) : undefined;
  const a11y = decorative ? { 'aria-hidden': true } : { role: 'img', 'aria-label': `${name} illustration` };
  return (
    <div className={cn('exercise-art flex aspect-square items-center justify-center overflow-hidden rounded-lg bg-surface-2 text-muted', className)} {...a11y}>
      {svg ? (
        // The SVGs carry a <title>; it is hidden with the wrapper so names are not announced twice.
        <div className="size-full [&>svg]:size-full" dangerouslySetInnerHTML={{ __html: svg }} />
      ) : (
        <Dumbbell className="size-1/3 opacity-40" aria-hidden />
      )}
    </div>
  );
}
