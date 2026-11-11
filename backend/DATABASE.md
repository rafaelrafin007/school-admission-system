# Database development

The persistence model uses PostgreSQL, EF Core 10.0.14, and the Npgsql EF provider
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
checks without requiring PostgreSQL. The six PostgreSQL tests are explicitly skipped
when `SCHOOL_ADMISSION_TEST_CONNECTION` is unset.

To run all tests, point the connection variable at a disposable PostgreSQL database,
then run from `backend/`:

```powershell
$env:SCHOOL_ADMISSION_TEST_CONNECTION = $env:ConnectionStrings__DefaultConnection
dotnet test
```

The PostgreSQL fixture creates a unique `admission_test_<guid>` schema, applies the
real migration, checks database constraints and EF round trips, and drops only its own
schema. Its database user needs permission to create schemas. It never drops a database
or clears existing tables. A failed connection fails the tests; it is not treated as a pass.

Use a disposable Docker PostgreSQL instance for these checks; no Compose configuration
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
- `VARCHAR(17)` limits student identifier length; it does not impose an exact-length or
  digits-only rule. No such additional business rule is introduced here.
- Seat totals remain derived. Locking, authoritative database-time reads, expiration,
  and admission state transitions belong to later tasks.
