import { addDays, format, parseISO, startOfISOWeek } from 'date-fns';
import type { IsoDate } from '@/lib/api/types';

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
