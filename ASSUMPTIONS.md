# School Admission System — Assumptions

These assumptions document decisions where the assignment requirements were unclear or
allowed multiple valid approaches.

## API

- **GET hold details require no `X-Student-Id`.** The assignment's GET example shows hold
  details readable without a student header. Confirmation, cancellation, and history
  require ownership validation via `X-Student-Id` (trimmed, nonempty, max 17 characters).
- **Ownership is checked before idempotency.** Repeating confirmation as the owner
  returns 200 with the original timestamp. Repeating cancellation returns 409. This
  prevents a caller from confirming another student's hold simply because it is already
  confirmed.
- **GET responses represent overdue HELD records as EXPIRED without persisting changes.**
  This keeps read operations free of row locks while displaying accurate status between
  background expiration cycles.
- **Student history is newest first by creation time, then descending hold ID.**
  Provides deterministic ordering when timestamps match. All statuses are included in a
  single database query.

## Deployment

- **Backend internal port is 8080, host port defaults to 8080.** `BACKEND_HOST_PORT`
  allows a local override (e.g. `8081` if Oracle occupies host port 8080). The internal
  container port never changes.
- **Frontend is served by Nginx on host port 3000.** Browser JavaScript uses same-origin
  requests and never references Docker-internal service names.
- **PostgreSQL 18 runs inside the Compose network and is not exposed to the host.**
  Reduces attack surface; only backend and frontend ports are published.
- **Persistent named volume `school-admission-compose-data`.** Data survives
  `docker compose down` followed by `docker compose up`. `docker compose down -v`
  destroys the volume and must not be used for persistence verification.
- **Automatic EF Core migrations on backend startup.** The backend waits for PostgreSQL
  to be healthy, applies migrations, then accepts traffic. This removes the need for a
  manual `dotnet ef database update` step during evaluator setup.
- **`SCHOOL_ADMISSION_SKIP_MIGRATIONS=true` is a test-only opt-out.** Test fixtures that
  supply dummy connection strings without credentials set this variable so the API can
  start without a database. Production Compose does not set it; invalid credentials or an
  unavailable database prevent backend startup.
- **`FRONTEND_API_UPSTREAM` configures the Nginx upstream at container startup.**
  Defaults to `http://backend:8080`. Substitution is restricted to this one variable so
  Nginx request variables (`$host`, `$uri`, `$scheme`) are preserved.

## Security

- **The admin interface is a demo without authentication.** It is not protected by real
  authorization and must not be used in production as-is.
- **No credentials are hardcoded.** Database credentials are supplied through environment
  variables and `.env` is git-ignored.
