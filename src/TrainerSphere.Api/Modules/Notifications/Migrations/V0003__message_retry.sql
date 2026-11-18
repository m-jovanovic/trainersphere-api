-- Failed messages wait for next_attempt_at (exponential backoff) instead of being retried by the next batch.
ALTER TABLE notifications.outbox_messages ADD COLUMN next_attempt_at timestamptz NULL;
ALTER TABLE notifications.inbox_messages ADD COLUMN next_attempt_at timestamptz NULL;
