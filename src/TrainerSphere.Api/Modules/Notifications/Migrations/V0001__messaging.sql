CREATE SCHEMA IF NOT EXISTS notifications;

CREATE TABLE notifications.outbox_messages (
    id uuid PRIMARY KEY,
    type text NOT NULL,
    content jsonb NOT NULL,
    occurred_at timestamptz NOT NULL,
    processed_at timestamptz NULL,
    attempts integer NOT NULL DEFAULT 0,
    error text NULL
);

CREATE INDEX outbox_messages_pending_idx ON notifications.outbox_messages (occurred_at, id) WHERE processed_at IS NULL;

CREATE TABLE notifications.outbox_message_consumers (
    message_id uuid NOT NULL,
    handler text NOT NULL,
    processed_at timestamptz NOT NULL,
    PRIMARY KEY (message_id, handler)
);

CREATE TABLE notifications.inbox_messages (
    id uuid PRIMARY KEY,
    type text NOT NULL,
    content jsonb NOT NULL,
    occurred_at timestamptz NOT NULL,
    processed_at timestamptz NULL,
    attempts integer NOT NULL DEFAULT 0,
    error text NULL
);

CREATE INDEX inbox_messages_pending_idx ON notifications.inbox_messages (occurred_at, id) WHERE processed_at IS NULL;

CREATE TABLE notifications.inbox_message_consumers (
    message_id uuid NOT NULL,
    handler text NOT NULL,
    processed_at timestamptz NOT NULL,
    PRIMARY KEY (message_id, handler)
);
