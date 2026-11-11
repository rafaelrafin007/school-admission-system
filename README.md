# School Admission System

A complete school admission application with an ASP.NET Core backend, PostgreSQL database,
and React frontend, deployed via Docker Compose.

Built as a recruitment assignment for OnnoRokom Projukti Limited (Assistant Software Engineer,
.NET).

## Features

- Class management (create, list, view classes with seat statistics)
- Atomic seat reservation (hold creation) with PostgreSQL row-level locking
- Hold confirmation and cancellation with student ownership validation
- Student application history (newest first)
- Automatic hold expiration via background worker
- Concurrency-safe reservation creation (class-first → hold-second locking order)
- Duplicate application prevention via partial unique index
- React frontend with TanStack Query, React Hook Form, Zod validation, Tailwind CSS
- Docker Compose deployment with automatic EF Core migrations and persistent storage

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core Web API, EF Core 10, Npgsql |
| Database | PostgreSQL 18 |
| Frontend | React 19, TypeScript, Vite, React Router, TanStack Query, Axios, React Hook Form, Zod, Tailwind CSS |
| Deployment | Docker, Docker Compose, Nginx |
| Testing | xUnit (backend), Vitest + React Testing Library (frontend) |

## Architecture

```text
Browser
   |
   v
Frontend container (React + Nginx)
   | same-origin reverse proxy
   v
Backend container (ASP.NET Core .NET 10)
   |
   v
PostgreSQL 18 container
   |
   v
Persistent named Docker volume
```

Browser requests to `/classes`, `/holds`, `/me`, and `/health` are forwarded by Nginx to
the backend on the same origin. No Docker-internal hostnames appear in browser JavaScript.

## Folder Structure

```text
school-admission-system/
├── docker-compose.yml          # Compose orchestration
├── .env.example                # Environment variable template
├── .dockerignore               # Root Docker ignore
├── README.md                   # This file
├── ASSUMPTIONS.md              # Design and deployment assumptions
├── AI_USAGE.md                 # AI assistance disclosure
├── backend/
│   ├── Dockerfile              # Multi-stage .NET 10 build
│   ├── .dockerignore
│   ├── SchoolAdmission.sln
│   ├── DATABASE.md             # Database development notes
│   ├── src/                    # Source projects (Api, Application, Domain, Infrastructure)
│   └── tests/                  # Unit and integration tests
└── frontend/
    ├── Dockerfile              # Multi-stage Node 22 + Nginx build
    ├── .dockerignore
    ├── nginx.conf              # Nginx reverse proxy + SPA fallback (envsubst template)
    ├── package.json
    ├── index.html
    ├── vite.config.ts
    ├── tsconfig.json
    ├── src/                    # React source
    └── public/                 # Static assets
```

## Prerequisites

- Docker Desktop or Docker Engine
- Docker Compose (v2)
- No .NET SDK, Node.js, or PostgreSQL installation required on the host

## Quick Start (Docker Compose)

```powershell
# 1. Clone the repository
git clone https://github.com/rafaelrafin007/school-admission-system.git
cd school-admission-system

# 2. Copy environment template
Copy-Item .env.example .env

# 3. Review and adjust .env if needed (e.g. port conflicts, password)
#    Notepad .env

# 4. Start the complete application
docker compose up -d --build

# 5. Wait for all services to become healthy
docker compose ps
```

## URLs

| Service | URL |
|---------|-----|
| Frontend (browser) | http://localhost:3000 |
| Backend API (direct) | http://localhost:8080 |
| Backend health | http://localhost:8080/health |

If port 8080 is occupied on your host (e.g. by an unrelated Oracle service), set
`BACKEND_HOST_PORT=8081` in `.env`. The internal container port remains 8080; only the
host mapping changes. The frontend always proxies API calls internally, so browser URLs
are unaffected.

## Environment Configuration

Copy `.env.example` to `.env` and adjust:

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_DB` | `school_admission` | Database name |
| `POSTGRES_USER` | `school_admission` | Database user |
| `POSTGRES_PASSWORD` | `change-this-local-password` | Database password (change before use) |
| `BACKEND_HOST_PORT` | `8080` | Host port for backend API |
| `FRONTEND_HOST_PORT` | `3000` | Host port for frontend |
| `FRONTEND_API_UPSTREAM` | `http://backend:8080` | Internal Nginx upstream URL for backend API |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |
| `HOLD_DURATION_SECONDS` | `600` | Reservation lifetime in seconds |
| `HOLD_EXPIRATION_INTERVAL_SECONDS` | `5` | Expiration worker interval |
| `HOLD_EXPIRATION_BATCH_SIZE` | `50` | Expiration batch size |

`FRONTEND_API_UPSTREAM` is substituted into the Nginx configuration at container startup.
Do not set it to a `localhost` URL — browser JavaScript cannot reach Docker-internal
hostnames from outside the container network.

## API Endpoints

All backend endpoints are accessible through the frontend at the same origin:

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Health check |
| `GET` | `/classes` | List all classes with seat statistics |
| `POST` | `/classes` | Create a new class (body: `{ name, totalSeats }`) |
| `GET` | `/classes/{classId}` | Get a class by ID |
| `POST` | `/classes/{classId}/holds` | Create a seat reservation (header: `X-Student-Id`) |
| `GET` | `/holds/{holdId}` | Get hold details (no student header required) |
| `POST` | `/holds/{holdId}/confirm` | Confirm a hold (header: `X-Student-Id`) |
| `DELETE` | `/holds/{holdId}` | Cancel a hold (header: `X-Student-Id`) |
| `GET` | `/me/holds` | Get student's application history (header: `X-Student-Id`) |

## Database Migrations

EF Core migrations are applied automatically when the backend starts. The backend waits
for PostgreSQL to report healthy, runs `Database.Migrate()`, and only then accepts HTTP
traffic. Migration failures stop backend startup clearly. No manual `dotnet ef database
update` is required.

## Persistent Storage

PostgreSQL data is stored in the named Docker volume `school-admission-compose-data`.
Data survives `docker compose down` followed by `docker compose up`.

Do NOT use `docker compose down -v` when verifying persistence, as it removes the volume
and all data.

## Testing

### Frontend

```powershell
cd frontend
npm ci
npm test
npm run build
```

### Backend

Unit tests run without PostgreSQL. Integration tests require a real PostgreSQL connection
via the `SCHOOL_ADMISSION_TEST_CONNECTION` environment variable.

```powershell
cd backend
dotnet restore
dotnet build
dotnet test
```

PostgreSQL-dependent integration tests are skipped when the connection variable is unset.

## Logs and Troubleshooting

```powershell
# View all service logs
docker compose logs

# View logs for a specific service
docker compose logs backend
docker compose logs db
docker compose logs frontend

# Check service health
docker compose ps

# Full rebuild and restart
docker compose up -d --build
```

## Shutdown

```powershell
docker compose down
```

Data is preserved in the named volume. To remove containers and images:

```powershell
docker compose down --rmi all
```

> Never use `docker compose down -v` unless you intend to delete all database data.

## Security Notes

- The admin interface is an assignment-level demo without authentication.
- It is not protected by real authorization and should not be used in production as-is.
- Change `POSTGRES_PASSWORD` from the default before any non-evaluation use.
- No real credentials, private keys, or certificates are stored in the repository.
- See `AI_USAGE.md` for AI assistance disclosure and `ASSUMPTIONS.md` for design decisions.

## License

This project is a recruitment assignment submission.
