import { Link, useRouter, type ErrorComponentProps } from '@tanstack/react-router';
import { Loader2, SearchX, TriangleAlert, WifiOff } from 'lucide-react';
import { ApiError, NetworkError, errorMessage } from '@/lib/api/http';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/misc';

export function PageSpinner() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center" role="status" aria-label="Loading">
      <Loader2 className="size-6 animate-spin text-muted" aria-hidden />
    </div>
  );
}

export function PageError({ error, reset }: ErrorComponentProps) {
  const router = useRouter();
  if (error instanceof ApiError && error.status === 404) return <NotFound />;
  const offline = error instanceof NetworkError;
  return (
    <EmptyState
      className="min-h-[40vh]"
      icon={offline ? <WifiOff /> : <TriangleAlert />}
      title={offline ? 'You are offline' : 'This page could not load'}
      description={offline ? 'Reconnect to load this page. Screens you opened before still work.' : errorMessage(error)}
      action={
        <Button
          variant="secondary"
          onClick={() => {
            reset();
            void router.invalidate();
          }}
        >
          Try again
        </Button>
      }
    />
  );
}

/** Inline error for a failed query inside a page that otherwise rendered. */
export function QueryError({ error, onRetry, className }: { error: unknown; onRetry?: () => void; className?: string }) {
  const offline = error instanceof NetworkError;
  return (
    <EmptyState
      className={className}
      icon={offline ? <WifiOff /> : <TriangleAlert />}
      title={offline ? 'You are offline' : 'Could not load this'}
      description={offline ? 'Reconnect and try again.' : errorMessage(error)}
      action={
        onRetry && (
          <Button variant="secondary" size="sm" onClick={onRetry}>
            Try again
          </Button>
        )
      }
    />
  );
}

export function NotFound() {
  return (
    <EmptyState
      className="min-h-[40vh]"
      icon={<SearchX />}
      title="Not found"
      description="This page does not exist or you no longer have access to it."
      action={
        <Button asChild variant="secondary">
          <Link to="/">Go home</Link>
        </Button>
      }
    />
  );
}
