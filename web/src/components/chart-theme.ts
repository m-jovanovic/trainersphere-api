/** Shared Recharts styling; colours come from theme variables so charts follow light/dark. */
export const chartTheme = {
  grid: 'var(--border)',
  axis: 'var(--muted)',
  primary: 'var(--primary)',
  secondary: 'var(--pr)',
  tick: { fontSize: 12, fill: 'var(--muted)' },
  tooltip: {
    contentStyle: {
      background: 'var(--surface)',
      border: '1px solid var(--border)',
      borderRadius: 8,
      color: 'var(--fg)',
      fontSize: 13,
      boxShadow: '0 4px 12px rgb(0 0 0 / 0.08)',
    },
    labelStyle: { color: 'var(--muted)', marginBottom: 2 },
    cursor: { stroke: 'var(--border-strong)', fill: 'var(--surface-2)' },
  },
} as const;

const reducedMotion = typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

/** Spread onto Recharts series: short entrance, none for reduced-motion users. */
export const chartAnimation = { isAnimationActive: !reducedMotion, animationDuration: 400 } as const;
