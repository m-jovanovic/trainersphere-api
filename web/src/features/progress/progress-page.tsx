import { getRouteApi } from '@tanstack/react-router';
import { useUser } from '@/lib/auth/hooks';
import { PageHeader } from '@/components/ui/misc';
import { BodyMetricsCard } from './body-metrics';
import { ExerciseProgressCard, PersonalRecordsCard, TrainingSummaryCard } from './progress-panels';

const route = getRouteApi('/_authed/_shell/_client/progress');

export function ProgressPage() {
  const user = useUser();
  const { exercise } = route.useSearch();
  const navigate = route.useNavigate();
  return (
    <>
      <PageHeader title="Progress" description="Your training, records and body measurements." />
      <div className="flex flex-col gap-4 sm:gap-6">
        <TrainingSummaryCard clientId={user.id} />
        <ExerciseProgressCard
          clientId={user.id}
          exerciseId={exercise}
          onExerciseChange={(id) => void navigate({ search: { exercise: id }, replace: true })}
        />
        <PersonalRecordsCard clientId={user.id} />
        {/* Linked from Today as /progress#body. */}
        <section id="body" aria-label="Body measurements" className="scroll-mt-20">
          <BodyMetricsCard clientId={user.id} />
        </section>
      </div>
    </>
  );
}
