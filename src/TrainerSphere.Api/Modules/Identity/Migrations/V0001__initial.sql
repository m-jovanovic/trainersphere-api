CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE identity.users (
    id uuid PRIMARY KEY,
    email text NOT NULL,
    password_hash text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    role text NOT NULL CHECK (role IN ('Trainer', 'Client')),
    trainer_id uuid NULL REFERENCES identity.users (id),
    units text NOT NULL CHECK (units IN ('Kg', 'Lb')),
    time_zone text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT users_email_unique UNIQUE (email),
    CONSTRAINT users_client_has_trainer CHECK ((role = 'Client') = (trainer_id IS NOT NULL))
);

CREATE INDEX users_trainer_id_idx ON identity.users (trainer_id) WHERE trainer_id IS NOT NULL;

CREATE TABLE identity.refresh_tokens (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
    family_id uuid NOT NULL,
    token_hash text NOT NULL UNIQUE,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz NULL,
    replaced_by_id uuid NULL
);

CREATE INDEX refresh_tokens_user_id_idx ON identity.refresh_tokens (user_id);
CREATE INDEX refresh_tokens_family_id_idx ON identity.refresh_tokens (family_id);

CREATE TABLE identity.invitations (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL REFERENCES identity.users (id),
    email text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    message text NULL,
    token_hash text NOT NULL UNIQUE,
    status text NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Cancelled')),
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    accepted_at timestamptz NULL,
    cancelled_at timestamptz NULL,
    client_id uuid NULL REFERENCES identity.users (id)
);

CREATE INDEX invitations_trainer_created_idx ON identity.invitations (trainer_id, created_at DESC);
CREATE INDEX invitations_email_idx ON identity.invitations (email);

CREATE TABLE identity.password_reset_tokens (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
    token_hash text NOT NULL UNIQUE,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    used_at timestamptz NULL
);

CREATE INDEX password_reset_tokens_user_id_idx ON identity.password_reset_tokens (user_id);

CREATE TABLE identity.outbox_messages (
    id uuid PRIMARY KEY,
    type text NOT NULL,
    content jsonb NOT NULL,
    occurred_at timestamptz NOT NULL,
    processed_at timestamptz NULL,
    attempts integer NOT NULL DEFAULT 0,
    error text NULL
);

CREATE INDEX outbox_messages_pending_idx ON identity.outbox_messages (occurred_at, id) WHERE processed_at IS NULL;

CREATE TABLE identity.outbox_message_consumers (
    message_id uuid NOT NULL,
    handler text NOT NULL,
    processed_at timestamptz NOT NULL,
    PRIMARY KEY (message_id, handler)
);

CREATE TABLE identity.inbox_messages (
    id uuid PRIMARY KEY,
    type text NOT NULL,
    content jsonb NOT NULL,
    occurred_at timestamptz NOT NULL,
    processed_at timestamptz NULL,
    attempts integer NOT NULL DEFAULT 0,
    error text NULL
);

CREATE INDEX inbox_messages_pending_idx ON identity.inbox_messages (occurred_at, id) WHERE processed_at IS NULL;

CREATE TABLE identity.inbox_message_consumers (
    message_id uuid NOT NULL,
    handler text NOT NULL,
    processed_at timestamptz NOT NULL,
    PRIMARY KEY (message_id, handler)
);
