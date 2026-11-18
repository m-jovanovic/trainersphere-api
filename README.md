# TrainerSphere

Coaching platform for personal trainers and their clients: invite clients, build workouts and programs from an
illustrated exercise library, schedule them, log sessions live, and follow progress and personal records.

[![CI](https://github.com/m-jovanovic/trainersphere-api/actions/workflows/ci.yml/badge.svg)](https://github.com/m-jovanovic/trainersphere-api/actions/workflows/ci.yml)

## Run it

```powershell
docker compose up --build
```

| Service | URL |
| --- | --- |
| Web app | http://localhost:8080 |
| API | http://localhost:5000 (docs at `/scalar`, OpenAPI at `/openapi/v1.json`) |
| Mailpit (all emails) | http://localhost:8025 |
| Postgres | `localhost:5433`, database/user/password `trainersphere` |

Demo data is seeded on startup. Every demo account uses the password `Demo123!`:

- Trainer: `coach@trainersphere.dev` (Maya Novak)
- Clients: `alex@trainersphere.dev`, `sam@trainersphere.dev`, `jordan@trainersphere.dev`
- A pending invitation for `riley@trainersphere.dev` (the email is in Mailpit)

The trainer also gets a full training history: five workouts (Lower A/B, Upper A/B, Full Body Conditioning), a
repeating four-week "Strong Start" program (one finished cycle, one in progress) for all three clients, about six
weeks of logged sessions with progressive weights, personal records, weekly body metrics, a few skipped and missed
assignments for varied adherence, and an assignment for every client today.

## Develop

Requirements: .NET 11 SDK, Docker, Node.js (for `web/`).

```powershell
docker compose up -d postgres mailpit          # database on 5433, SMTP on 1025
dotnet run --project src/TrainerSphere.Api     # http://localhost:5000, Development settings, demo seed
dotnet run --project src/TrainerSphere.Api -- --migrate   # apply migrations and exit
```

Tests (integration tests need Docker running; they start their own Postgres and Mailpit containers):

```powershell
dotnet test --solution TrainerSphere.slnx
```

The web app lives in `web/` and has its own README.

## Architecture

A modular monolith on ASP.NET Core (.NET 11, C# 15), organised in vertical slices.

```
src/TrainerSphere.Api              host and modules
  Host/                            startup, error handling, OpenAPI, health checks, startup tasks
  Modules/<Module>/
    Contracts/                     integration events: the only types other modules may use
    Domain/                        entities and rules, no data access or HTTP
    Features/<Slice>/              endpoint + request/response + handler + validator + SQL
    Data/                          module SQL helpers and repositories
    Migrations/V0001__name.sql     raw SQL, applied in order per module
src/TrainerSphere.SharedKernel     Result union, messaging, outbox/inbox, data access, migrations
test/TrainerSphere.UnitTests       domain, handlers, Result laws, architecture rules
test/TrainerSphere.IntegrationTests  the real API against Postgres in Docker (see its README)
```

Modules: **Identity** (accounts, JWT access tokens with rotating refresh cookies, invitations, password reset),
**Training** (profiles, exercise library, workouts, programs, assignments, sessions, progress, dashboards),
**Notifications** (emails over SMTP).

- **Errors as values.** Handlers return `Result<T>`, a C# 15 union of the success value and a closed set of
  errors. Endpoints map it to RFC 9457 problem details (`400` validation with JSON-path `errors`, `401`, `403`,
  `404`, `409`, `422`).
- **Data.** Postgres with Dapper and hand-written SQL, one schema per module. Each module owns its tables and never
  queries another module's schema; architecture tests enforce this.
- **Migrations.** An in-house runner applies `Migrations/*.sql` per module at startup (or with `--migrate`) under
  an advisory lock and refuses to start when an applied migration was edited.
- **Messaging.** Modules communicate through integration events written to the subscriber's inbox in the same
  transaction as the change. A background service processes inboxes and outboxes with retries and per-handler
  idempotency.
- **API docs.** OpenAPI document with Scalar UI; the HTTP contract the web app builds on is kept with the project
  documentation.

## Configuration

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:Database` | Postgres connection string |
| `Auth:SigningKey` | JWT signing key, at least 32 characters (required outside Development) |
| `Auth:RefreshCookieSecure` | `true` by default; only plain-http setups turn it off |
| `Database:MigrateOnStartup`, `Database:SeedDemoData` | startup tasks |
| `App:WebBaseUrl` | base URL used in email links |
| `Smtp:Host`, `Smtp:Port`, `Smtp:From`, `Smtp:EnableSsl`, `Smtp:UserName`, `Smtp:Password` | outgoing email |
| `Cors:Origins` | allowed browser origins when the UI is not served through the proxy |
| `OpenApi:Enabled` | expose `/openapi/v1.json` and `/scalar` (on in Development) |
