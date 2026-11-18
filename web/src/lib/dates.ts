import { addDays, differenceInCalendarDays, format, formatDistanceToNowStrict, parseISO, startOfISOWeek } from 'date-fns';
import type { Iso, IsoDate } from '@/lib/api/types';

export function toIsoDate(d: Date): IsoDate {
  return format(d, 'yyyy-MM-dd');
}

export function todayIso(): IsoDate {
  return toIsoDate(new Date());
}

export function parseDate(d: IsoDate): Date {
  return parseISO(d);
}

export function addDaysIso(d: IsoDate, days: number): IsoDate {
  return toIsoDate(addDays(parseISO(d), days));
}

export function weekStartIso(d: IsoDate): IsoDate {
  return toIsoDate(startOfISOWeek(parseISO(d)));
}

export function formatDay(d: IsoDate, pattern = 'EEE d MMM'): string {
  return format(parseISO(d), pattern);
}

/** "Today", "Tomorrow", "Yesterday", else a short date. */
export function relativeDay(d: IsoDate, today: IsoDate = todayIso()): string {
  const diff = differenceInCalendarDays(parseISO(d), parseISO(today));
  if (diff === 0) return 'Today';
  if (diff === 1) return 'Tomorrow';
  if (diff === -1) return 'Yesterday';
  return formatDay(d, diff > -7 && diff < 7 ? 'EEEE' : 'EEE d MMM');
}

export function formatAgo(iso: Iso | null, never = 'Never'): string {
  if (!iso) return never;
  return formatDistanceToNowStrict(parseISO(iso), { addSuffix: true });
}

export function formatDateTime(iso: Iso, pattern = 'EEE d MMM, HH:mm'): string {
  return format(parseISO(iso), pattern);
}

/** Local calendar date of a timestamp. */
export function isoDateOf(iso: Iso): IsoDate {
  return toIsoDate(parseISO(iso));
}
