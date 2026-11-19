CREATE TABLE notifications.recipients (
    user_id uuid PRIMARY KEY,
    email text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    role text NOT NULL CHECK (role IN ('Trainer', 'Client')),
    trainer_id uuid NULL,
    updated_at timestamptz NOT NULL
);

CREATE TABLE notifications.email_log (
    id uuid PRIMARY KEY,
    source_event_id uuid NOT NULL,
    kind text NOT NULL,
    recipient text NOT NULL,
    subject text NOT NULL,
    sent_at timestamptz NOT NULL
);

CREATE INDEX email_log_recipient_idx ON notifications.email_log (recipient, sent_at DESC);
