# Database development

The supported and tested database target is PostgreSQL 18. The persistence model uses EF Core 10.0.14 and the Npgsql EF provider
10.0.3. The repository's local `dotnet-ef` tool is pinned to 10.0.14.

## Configuration

The API requires `ConnectionStrings__DefaultConnection`. No connection string or
password is stored in application settings. A local connection has this format:

```text
Host=localhost;Port=5433;Database=school_admission;Username=<local-user>;Password=<local-password>
```

In PowerShell, enter the full connection string without putting it in shell history:

```powershell
$secureConnection = Read-Host 'Local PostgreSQL connection string' -AsSecureString
$env:ConnectionStrings__DefaultConnection = [System.Net.NetworkCredential]::new('', $secureConnection).Password
```

This sets a process-scoped environment variable. Do not paste real credentials into
tracked files. Missing or blank configuration stops API startup with a clear error.
The `/health` endpoint is liveness only; it does not check database connectivity.

`HOLD_DURATION_SECONDS` configures reservation duration in seconds, defaulting to 600
when absent. It must be a positive 32-bit integer; blank, nonnumeric, zero, negative,
and out-of-range values fail API startup. For example, set
`$env:HOLD_DURATION_SECONDS = '600'` before starting the API.

Automatic expiration has separate settings:

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `HOLD_EXPIRATION_INTERVAL_SECONDS` | `5` | Delay between completed processing cycles, including failed cycles |
| `HOLD_EXPIRATION_BATCH_SIZE` | `50` | Maximum candidate classes per cycle |

Both settings must be positive 32-bit integers. Missing values use the defaults;
blank, nonnumeric, fractional, zero, negative and out-of-range values fail startup.
Long intervals are split into cancellation-aware delays to stay within runtime timer
limits. These settings do not change the 600-second default reservation lifetime.

## Migrations

Run these commands from `backend/` against your intended development database:

```powershell
dotnet tool restore
dotnet ef database update --project src/SchoolAdmission.Infrastructure --startup-project src/SchoolAdmission.Infrastructure
```

The Infrastructure design-time factory reads the same environment variable, so API
does not need the EF Design package. To generate a later migration, set that variable
and run:

```powershell
dotnet ef migrations add <MigrationName> --project src/SchoolAdmission.Infrastructure --startup-project src/SchoolAdmission.Infrastructure --output-dir Persistence/Migrations
```

Generating migrations does not require a database connection. Applying them does.
The API does not automatically migrate on startup.

## Verification

`dotnet test` runs the domain, EF model, configuration, JSON serialization, and health
checks without requiring PostgreSQL. PostgreSQL schema, class API, and hold API/concurrency tests are explicitly skipped
when `SCHOOL_ADMISSION_TEST_CONNECTION` is unset.

To run all tests, point the connection variable at a disposable PostgreSQL database,
then run from `backend/`:

```powershell
$env:SCHOOL_ADMISSION_TEST_CONNECTION = $env:ConnectionStrings__DefaultConnection
dotnet test
```

The PostgreSQL fixture creates a unique `admission_test_<guid>` schema, applies the
real migration, checks database constraints and EF round trips, and drops only its own
schema. Its database user needs permission to create schemas. Use only a disposable
database: schema removal uses CASCADE, which also removes dependent objects. Cleanup is
best-effort; a process/machine crash or database outage can leave a test schema behind.
If migration and cleanup both fail, the original exception is preserved with the cleanup
exception attached in its Data dictionary. A failed connection fails the tests; it is not
treated as a pass.

Observer/migration contexts do not pool connections per schema. Hold API fixtures own
their bounded Npgsql data source and dispose it after requests finish, preventing idle
connections from accumulating across isolated tests.

Use a disposable Docker PostgreSQL 18 instance for these checks; no Compose configuration
is introduced in this task. Remove only the verification container you created.

## Schema decisions

- The two business tables are `admission_classes` and `admission_holds`.
- IDs are supplied strings; timestamps are supplied UTC `DateTime` values mapped to
  `TIMESTAMPTZ`. There are no local-clock or transaction-start-time defaults.
- Hold status uses an explicit uppercase database conversion. API enum JSON is uppercase
  and does not accept integer enum values.
- The unique index covers `(class_id, student_id)` only for `HELD` and `CONFIRMED` rows.
  A deadline passing does not remove a row from that index; its status must change.
- Additional indexes support class occupancy, student history, and global held-record
  expiration scans. Class deletion is restricted while any hold references it.
- `X-Student-Id` is required, trimmed, and must contain 1 to 17 characters after trimming.
  Identifiers stay strings; leading zeros and nondigit characters are preserved. No
  exact-length or digits-only rule is imposed. Supply a single header value.
- Class GET endpoints derive seat statistics in one grouped SQL statement using
  `statement_timestamp()` to exclude expired HELD rows, even before a status transition.
  No counters are stored.

## Atomic hold creation

`POST /classes/{classId}/holds` has no request body. The operation runs in one explicit
READ COMMITTED transaction: lock the class row with SELECT FOR UPDATE, read
`clock_timestamp()` after acquiring the lock, expire this class's overdue HELD rows,
check duplicate applications, count occupied seats, insert a HELD row, and commit.
The same database instant supplies creation time, the deadline, and expiration/count
predicates. Attempts rejected before commit roll back the whole transaction, including any
lazy expiration updates. A connection failure or cancellation during commit can leave the
caller uncertain whether the transaction committed; atomicity does not guarantee response delivery.
Only the active student/class unique-index violation is mapped
to a duplicate conflict; unexpected database errors receive a generic server response.

All future confirmation, cancellation, and expiration writers must follow the same
class-first locking protocol. The row lock is held by PostgreSQL, so it is shared across
backend instances. The tests cover 100 simultaneously released requests and same-student
contention through HTTP with independent request scopes and pooled connections. The
test pool is bounded to avoid exhausting PostgreSQL connections. This is not a claim
that the optional separate-process multi-instance deployment bonus is complete.

## Hold lifecycle

- `GET /holds/{holdId}` requires no student header and returns only holdId, classId,
  status and expiresAt; missing holds return 404.
- `GET /me/holds` requires the validated student header and returns only that student's
  history, newest first (ID descending breaks creation-time ties), with className included.
  Each GET uses one untracked SQL projection and statement_timestamp(); overdue HELD rows
  appear as EXPIRED without changing stored data or acquiring row locks.
- `POST /holds/{holdId}/confirm` and `DELETE /holds/{holdId}` require ownership.
  Unknown holds return 404, another student's hold returns a generic 403, and invalid
  states return 409. Missing/invalid/multiple student header values return 400.
- Confirmation returns 200 with only holdId and status. An owner repeating confirmation
  receives success without changing confirmed_at. Ownership is checked before idempotency.
- Cancellation returns 204 with no body, stores CANCELLED and cancelled_at, and preserves
  history. Confirmed, expired and already cancelled applications cannot be cancelled (409).
- Each mutation starts READ COMMITTED, resolves the immutable class ID without a hold
  lock, locks the class first and the hold second with SELECT FOR UPDATE, then reads
  clock_timestamp(). HELD rows at or beyond the deadline are persisted as EXPIRED and
  committed before returning 409. Successful changes also commit before returning.
- Domain Confirm, Cancel and Expire methods keep setters private and enforce UTC and
  expiration boundaries. CONFIRMED and CANCELLED timestamps are set only on their first
  valid transitions. Expiration does not alter those timestamps.

Lifecycle concurrency tests use independent HTTP scopes, PostgreSQL lock-wait observation,
and separate contexts to inspect persisted state. A test-only transaction interceptor pauses
after SQL updates and before commit to verify request-cancellation rollback. These tests
also verify cancellation while waiting for a class lock and rollback of creation's lazy
expiration plus insert. Cleanup is best-effort as described above.

## Automatic hold expiration

The API registers one `HoldExpirationWorker` through Infrastructure. Its BackgroundService
starts an immediate cycle, then waits for the configured interval after each cycle.
Cycles never overlap within one worker. Each cycle creates an asynchronous DI scope for
the processor and DbContext, and disposes that scope before waiting.

Candidate discovery reads at most the configured number of distinct class IDs, oldest
deadline first, without acquiring hold row locks:

```sql
SELECT class_id
FROM admission_holds
WHERE status = 'HELD' AND expires_at <= statement_timestamp()
GROUP BY class_id
ORDER BY MIN(expires_at), class_id
LIMIT @batchSize;
```

Each candidate is processed in its own READ COMMITTED transaction. The processor locks
the class with a parameterized SELECT FOR UPDATE, obtains clock_timestamp() after the
lock, and bulk-updates only that class's HELD rows whose expires_at is at or before that
instant. The UPDATE acquires hold row locks after the class lock. It commits before
moving to the next class. Missing/stale candidates are safe no-ops.

The bulk predicate implements the same state and deadline rule as AdmissionHold.Expire.
It avoids materializing individual holds and changes only status, retaining all history
and timestamps. Work is bounded by candidate classes, not a separate hold-row limit; a
class's eligible holds are updated together. There is one discovery query plus a fixed
set of lock/time/update statements per selected class, with no per-hold queries.

Multiple application instances may discover the same class. PostgreSQL serializes their
class transactions; each processor rechecks eligibility using fresh time after acquiring
the lock, so a later processor safely updates zero already-processed rows. The same lock
order coordinates with creation, confirmation and cancellation. Tests exercise independent
processors/contexts, not a deployment of two separate backend processes.

A failed class transaction is disposed and the current cycle stops. Previously committed
classes stay committed. The worker logs the failure and waits for the next normal interval
before rediscovering remaining work. Shutdown cancels database operations and delays without
logging expected cancellation as an error. Scope/transaction cleanup is best-effort during
process crashes or database outages; uncommitted work is rolled back by PostgreSQL when the
connection ends, and later cycles rediscover eligible rows.

Cycle counts and duration are structured Debug logs. Errors include exception type and,
for class processing, class ID; worker logs omit provider messages, student IDs and credentials.

Background expiration complements lazy expiration during reservation creation and lifecycle
operations. Reads still derive effective EXPIRED status without writes. Therefore read-side
seat counts and confirmation deadlines do not depend on how quickly a background cycle runs.
Worker-enabled tests verify automatic expiration without student requests, retry after an
injected transient failure, bounded cycles, competing processors and shutdown during an
actual PostgreSQL lock wait. API-only regression fixtures disable this one hosted worker
to test lazy expiration deterministically; they retain their original assertions.
