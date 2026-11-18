-- Profiles projected from Identity events. Ids equal the Identity user ids.
CREATE TABLE training.trainers (
    id uuid PRIMARY KEY,
    email text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    time_zone text NOT NULL,
    units text NOT NULL CHECK (units IN ('Kg', 'Lb')),
    registered_at timestamptz NOT NULL,
    profile_updated_at timestamptz NOT NULL
);

-- No foreign key to trainers: both are filled asynchronously from Identity events.
CREATE TABLE training.clients (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL,
    email text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    time_zone text NOT NULL,
    units text NOT NULL CHECK (units IN ('Kg', 'Lb')),
    status text NOT NULL CHECK (status IN ('Active', 'Archived')),
    joined_at timestamptz NOT NULL,
    archived_at timestamptz NULL,
    goals text NULL,
    trainer_notes text NULL,
    profile_updated_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX clients_trainer_idx ON training.clients (trainer_id);

-- Library exercises have a slug and no trainer; custom exercises belong to a trainer and have no slug.
CREATE TABLE training.exercises (
    id uuid PRIMARY KEY,
    trainer_id uuid NULL,
    slug text NULL,
    name text NOT NULL,
    category text NOT NULL,
    movement_pattern text NOT NULL,
    primary_muscles text[] NOT NULL,
    secondary_muscles text[] NOT NULL,
    equipment text[] NOT NULL,
    unilateral boolean NOT NULL,
    cue text NULL,
    illustration text NULL,
    archived boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT exercises_library_has_slug CHECK ((trainer_id IS NULL) = (slug IS NOT NULL))
);

CREATE UNIQUE INDEX exercises_slug_unique ON training.exercises (slug) WHERE slug IS NOT NULL;
CREATE UNIQUE INDEX exercises_custom_name_unique ON training.exercises (trainer_id, lower(name)) WHERE trainer_id IS NOT NULL AND NOT archived;
CREATE INDEX exercises_trainer_idx ON training.exercises (trainer_id) WHERE trainer_id IS NOT NULL;

CREATE TABLE training.workouts (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL,
    name text NOT NULL,
    description text NULL,
    archived boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX workouts_trainer_idx ON training.workouts (trainer_id);

CREATE TABLE training.workout_exercises (
    id uuid PRIMARY KEY,
    workout_id uuid NOT NULL REFERENCES training.workouts (id) ON DELETE CASCADE,
    position integer NOT NULL,
    exercise_id uuid NOT NULL REFERENCES training.exercises (id),
    sets integer NOT NULL,
    goal_type text NOT NULL CHECK (goal_type IN ('Reps', 'Time', 'Distance')),
    goal_min integer NOT NULL,
    goal_max integer NOT NULL,
    load_type text NOT NULL CHECK (load_type IN ('None', 'Bodyweight', 'Weight', 'Percent1Rm', 'Rpe')),
    load_value numeric(7, 2) NULL,
    rest_seconds integer NOT NULL,
    tempo text NULL,
    superset_group text NULL,
    notes text NULL,
    CONSTRAINT workout_exercises_position_unique UNIQUE (workout_id, position)
);

CREATE TABLE training.programs (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL,
    name text NOT NULL,
    description text NULL,
    weeks integer NOT NULL CHECK (weeks BETWEEN 1 AND 52),
    archived boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX programs_trainer_idx ON training.programs (trainer_id);

CREATE TABLE training.program_slots (
    id uuid PRIMARY KEY,
    program_id uuid NOT NULL REFERENCES training.programs (id) ON DELETE CASCADE,
    week integer NOT NULL CHECK (week >= 1),
    day integer NOT NULL CHECK (day BETWEEN 1 AND 7),
    ordinal integer NOT NULL,
    workout_id uuid NOT NULL REFERENCES training.workouts (id)
);

CREATE INDEX program_slots_program_idx ON training.program_slots (program_id);

-- Removing a program assignment keeps the row (removed_at) so its completed and skipped history keeps its link.
CREATE TABLE training.program_assignments (
    id uuid PRIMARY KEY,
    program_id uuid NOT NULL REFERENCES training.programs (id),
    program_name text NOT NULL,
    trainer_id uuid NOT NULL,
    client_id uuid NOT NULL REFERENCES training.clients (id),
    start_date date NOT NULL,
    end_date date NOT NULL,
    created_at timestamptz NOT NULL,
    removed_at timestamptz NULL
);

CREATE INDEX program_assignments_client_idx ON training.program_assignments (client_id, created_at);

-- 'workout' is the snapshot (name, description, exercises) taken when the assignment was created.
CREATE TABLE training.assignments (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES training.clients (id),
    trainer_id uuid NOT NULL,
    date date NOT NULL,
    status text NOT NULL CHECK (status IN ('Scheduled', 'Completed', 'Skipped')),
    workout_id uuid NOT NULL REFERENCES training.workouts (id),
    workout jsonb NOT NULL,
    program_assignment_id uuid NULL REFERENCES training.program_assignments (id),
    program_name text NULL,
    notes text NULL,
    session_id uuid NULL,
    completed_at timestamptz NULL,
    skipped_at timestamptz NULL,
    skip_reason text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX assignments_client_date_idx ON training.assignments (client_id, date);
CREATE INDEX assignments_trainer_date_idx ON training.assignments (trainer_id, date);
CREATE INDEX assignments_program_assignment_idx ON training.assignments (program_assignment_id) WHERE program_assignment_id IS NOT NULL;

-- 'prescription' is the assignment's exercise snapshot at start (empty for ad-hoc sessions).
CREATE TABLE training.sessions (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES training.clients (id),
    trainer_id uuid NOT NULL,
    assignment_id uuid NULL REFERENCES training.assignments (id) ON DELETE SET NULL,
    workout_name text NOT NULL,
    status text NOT NULL CHECK (status IN ('InProgress', 'Completed')),
    started_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    notes text NULL,
    prescription jsonb NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT sessions_completed_has_time CHECK ((status = 'Completed') = (completed_at IS NOT NULL))
);

CREATE UNIQUE INDEX sessions_assignment_unique ON training.sessions (assignment_id) WHERE assignment_id IS NOT NULL;
CREATE INDEX sessions_client_started_idx ON training.sessions (client_id, started_at DESC);
CREATE INDEX sessions_trainer_started_idx ON training.sessions (trainer_id, started_at DESC);

CREATE TABLE training.session_sets (
    id uuid PRIMARY KEY,
    session_id uuid NOT NULL REFERENCES training.sessions (id) ON DELETE CASCADE,
    exercise_id uuid NOT NULL REFERENCES training.exercises (id),
    position integer NOT NULL,
    set_number integer NOT NULL,
    weight_kg numeric(7, 2) NULL,
    reps integer NULL,
    duration_seconds integer NULL,
    distance_meters numeric(10, 2) NULL,
    rpe numeric(3, 1) NULL,
    completed boolean NOT NULL,
    notes text NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX session_sets_session_idx ON training.session_sets (session_id, position, set_number);
CREATE INDEX session_sets_exercise_idx ON training.session_sets (exercise_id);

CREATE TABLE training.personal_records (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES training.clients (id),
    trainer_id uuid NOT NULL,
    exercise_id uuid NOT NULL REFERENCES training.exercises (id),
    type text NOT NULL CHECK (type IN ('Estimated1Rm', 'MaxWeight', 'MaxReps', 'MaxVolume')),
    value numeric(12, 2) NOT NULL,
    previous_value numeric(12, 2) NOT NULL,
    weight_kg numeric(7, 2) NULL,
    reps integer NULL,
    session_id uuid NOT NULL REFERENCES training.sessions (id),
    achieved_at timestamptz NOT NULL,
    CONSTRAINT personal_records_once_per_session UNIQUE (session_id, exercise_id, type)
);

CREATE INDEX personal_records_client_idx ON training.personal_records (client_id, achieved_at DESC);
CREATE INDEX personal_records_trainer_idx ON training.personal_records (trainer_id, achieved_at DESC);

CREATE TABLE training.body_metrics (
    client_id uuid NOT NULL REFERENCES training.clients (id),
    date date NOT NULL,
    weight_kg numeric(5, 2) NULL,
    body_fat_percent numeric(4, 2) NULL,
    waist_cm numeric(5, 2) NULL,
    chest_cm numeric(5, 2) NULL,
    hips_cm numeric(5, 2) NULL,
    arm_cm numeric(5, 2) NULL,
    thigh_cm numeric(5, 2) NULL,
    resting_heart_rate integer NULL,
    notes text NULL,
    recorded_by text NOT NULL CHECK (recorded_by IN ('Trainer', 'Client')),
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (client_id, date)
);
