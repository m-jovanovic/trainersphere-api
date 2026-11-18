CREATE TABLE training.invitations (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL,
    email text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    message text NULL,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL
);

CREATE INDEX invitations_trainer_idx ON training.invitations (trainer_id, created_at DESC, id DESC);
