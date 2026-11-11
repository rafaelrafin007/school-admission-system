# AI Usage Disclosure

This project was developed with the assistance of AI coding agents. The sections below
identify which agents contributed, which areas they touched, and the mistakes that were
encountered and corrected during development.

## AI Agents Involved

- **Codex** — Contributed to early backend foundation work (Tasks 01–02), including Git
  hygiene and initial project structure.
- **OpenCode** — Contributed to PostgreSQL persistence and EF Core migrations (Task 03),
  class management API (Task 04), and atomic reservation creation with concurrency
  protection (Task 05).
- **DeepSeek** — Contributed to hold confirmation, cancellation, and student history
  (Task 06), automatic hold expiration BackgroundService (Task 07), the React frontend
  (Task 08), and the initial Docker Compose deployment (Task 09).
- **Nemotron** — Performed the final engineering audit (Task 10), including the
  `FRONTEND_API_UPSTREAM` Nginx configuration refinement, documentation rewrite, and
  verification of builds, tests, and Docker deployment.

## Areas Receiving AI Assistance

- Backend C# code: ASP.NET Core Web API, Entity Framework Core 10, Npgsql, domain
  entities, application services, infrastructure repositories, controllers, background
  expiration worker, and automatic migration startup.
- Frontend TypeScript/React code: Vite, TanStack Query, Axios, React Hook Form, Zod,
  Tailwind CSS, React Router, component pages, API client, and Vitest tests.
- Docker Compose deployment files: `docker-compose.yml`, backend and frontend
  `Dockerfile`s, `nginx.conf`, `.dockerignore` files, and `.env.example`.
- Documentation: `README.md`, `ASSUMPTIONS.md`, `AI_USAGE.md`, and `backend/DATABASE.md`.

## Mistakes Encountered and Corrections

**Automatic migration startup broke test hosts.** The initial implementation called
`context.Database.Migrate()` unconditionally in `Program.Main`. Integration tests that
supply dummy connection strings without credentials (for JSON serialization, health
endpoint, and configuration tests) failed because the API could not connect to a
database it did not need.

**Correction:** Migration is now gated behind an explicit
`SCHOOL_ADMISSION_SKIP_MIGRATIONS=true` environment variable. Test fixtures that use
dummy connection strings set this variable. Production Docker Compose does not set it,
so invalid credentials or an unavailable database prevent backend startup — migrations
are never silently skipped in production.

**Initial Nginx configuration hardcoded the backend upstream.** The frontend Nginx config
contained `proxy_pass http://backend:8080`, making the upstream non-configurable without
editing the file.

**Correction:** The upstream is now `${FRONTEND_API_UPSTREAM}`, substituted at container
startup via `envsubst` with substitution restricted to that one variable. Nginx request
variables (`$host`, `$uri`, `$scheme`) are preserved. `docker-compose.yml` defaults it
to `http://backend:8080`.

## Verification

All AI-generated changes were reviewed and verified against the actual repository:

- Backend unit tests: 47 passed, 0 failed.
- Backend integration tests: 92 passed, 0 failed (against real PostgreSQL 18).
- Frontend tests: 20 passed, 0 failed.
- Backend build: 0 warnings, 0 errors.
- Frontend production build: successful.
- Docker Compose: all three services built and became healthy; API endpoints verified
  through the Nginx proxy; database persistence confirmed after `docker compose down`
  followed by `docker compose up`; invalid PostgreSQL password correctly prevented
  backend startup.

No confidential recruitment materials, recruiter emails, or real credentials were
included in the repository.
