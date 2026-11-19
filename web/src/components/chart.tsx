import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface ChartFrameProps {
  /** Short description announced to screen readers in place of the graphic. */
  summary: string;
  /** Accessible fallback table rows: [label, value]. */
  rows: [string, string][];
  columns: [string, string];
  caption: string;
  children: ReactNode;
  className?: string;
}

/** Wraps a chart with a text alternative: the SVG is hidden and a data table is exposed instead. */
export function ChartFrame({ summary, rows, columns, caption, children, className }: ChartFrameProps) {
  return (
    <figure className={cn('m-0', className)}>
      <div aria-hidden className="h-60 w-full">
        {children}
      </div>
      <figcaption className="sr-only">{summary}</figcaption>
      <table className="sr-only">
        <caption>{caption}</caption>
        <thead>
          <tr>
            <th scope="col">{columns[0]}</th>
            <th scope="col">{columns[1]}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(([a, b], i) => (
            <tr key={i}>
              <td>{a}</td>
              <td>{b}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
