import { useQueryClient } from '@tanstack/react-query';
import { keys } from '@/lib/api/queries';

/** Everything that shows scheduled workouts: calendars, dashboards and client summaries. */
export function useInvalidateSchedule() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: keys.assignments });
    void qc.invalidateQueries({ queryKey: keys.dashboard });
    void qc.invalidateQueries({ queryKey: keys.clients });
  };
}
