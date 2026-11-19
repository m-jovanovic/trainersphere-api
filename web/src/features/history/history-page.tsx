import { Link } from '@tanstack/react-router';
import { Play } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/ui/misc';
import { SessionList } from './session-list';

export function HistoryPage() {
  return (
    <>
      <PageHeader title="History" description="Every workout you have logged." />
      <SessionList
        emptyAction={
          <Button asChild>
            <Link to="/">
              <Play aria-hidden />
              Start your first workout
            </Link>
          </Button>
        }
      />
    </>
  );
}
