# School Admission System — Assumptions

## API Assumptions

### Hold lifecycle

- Hold details are readable without `X-Student-Id`, matching the assignment's GET example.
  The response excludes student identity. Confirmation, cancellation and student history
  require the previously approved trimmed, nonempty string identifier of at most 17 characters.
- Ownership mismatches on confirmation/cancellation return a generic 403; unknown hold IDs
  return 404. Ownership is checked even when the requested confirmation is already complete.
- Repeating confirmation as the owner returns 200 with the original confirmation timestamp
  retained in persistence. Repeating cancellation returns 409, consistently with other invalid
  cancellation states; cancellation never deletes history or reverses confirmed admission.
- GET responses represent overdue HELD records as EXPIRED without persisting changes. This
  keeps reads free of mutation locks while ensuring accurate UI status between background
  expiration cycles. An owner's expired confirmation/cancellation attempt persists EXPIRED and
  commits that transition before returning 409.
- Student history is newest first by creation time, then descending hold ID for deterministic
  ordering when timestamps match. All statuses are included, using a single database statement.
- A commit-time connection failure can leave the client uncertain about success. Transactions
  remain atomic; callers can retrieve the hold or their history to reconcile its current state.

## Deployment Assumptions

### Docker Compose

- The evaluator runs `docker compose up` from the repository root on a machine with Docker
  Desktop or Docker Engine and Docker Compose installed.
- No host installation of .NET SDK, Node.js, or PostgreSQL is required.
- The backend listens on internal port 8080 and is mapped to host port 8080 by default
  (`BACKEND_HOST_PORT=8080`). If port 8080 is occupied on the host (e.g. by an unrelated
  Oracle service), set `BACKEND_HOST_PORT=8081` in a local `.env` override. The internal
  container port remains 8080 regardless of the host mapping.
- The frontend is served by Nginx on host port 3000 by default (`FRONTEND_HOST_PORT=3000`).
- PostgreSQL 18 runs inside the Compose network and is not exposed to the host by default.
- A persistent named volume (`school-admission-compose-data`) stores database data.
- Data survives `docker compose down` followed by `docker compose up` without data loss.
- `docker compose down -v` must NOT be used when verifying persistence, as it removes the volume.

### Automatic Migrations

- The ASP.NET Core backend applies EF Core migrations automatically on startup via
  `ApplicationDbContext.Database.Migrate()` before accepting HTTP traffic.
- Migrations run after PostgreSQL reports healthy and before the backend health check passes.
- Migration failures stop backend startup clearly.
- The background hold expiration worker starts only after successful migration.
- Running multiple backend instances against the same database concurrently is not tested;
  EF Core migration locking serializes migration attempts, but the assignment does not
  require multi-instance deployment verification.

### Environment Configuration

- Database credentials are supplied via environment variables (`POSTGRES_USER`,
  `POSTGRES_PASSWORD`, `POSTGRES_DB`) and composed into the backend connection string.
- No credentials are hardcoded in Dockerfiles, source code, or committed configuration.
- `.env.example` documents all variables; a local `.env` override is copied from it and
  remains git-ignored.
- `HOLD_DURATION_SECONDS` defaults to 600 seconds when not set.

### Frontend

- The frontend uses same-origin API requests through the Nginx reverse proxy.
- Browser JavaScript never references Docker-internal service names (e.g. `backend`).
- The production build is served statically by Nginx; no Vite development server is used.
- `VITE_API_BASE_URL` is empty in production, so all API calls are relative to the same origin.

### Security

- The admin interface is an assignment-level demo interface without authentication.
- It is not protected by real authorization and should not be used in production as-is.
- No real credentials, private keys, or certificates are stored in the repository.
