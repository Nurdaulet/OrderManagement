# Order Management — Document Sync

Service for synchronising financial documents (invoices and acts of work) between the internal
**Order Management** system and an external system, with sync history and a Google Sheets log.

> **Project status — feature complete.**
> All mandatory requirements from the assignment are implemented: the domain model + EF Core
> mapping, the document synchronisation service (mock external JSON source) with full failure
> handling, all REST endpoints (sync, order documents, sync history, re-send to Google), the
> Google Sheets logger (CSV mock), and a unit-test suite. The code builds with **zero warnings**,
> runs (auto-migrates and seeds sample data on startup), serves Swagger UI, and all tests pass.

---

## Tech stack

| Concern            | Choice                                                        |
|--------------------|--------------------------------------------------------------|
| Runtime            | .NET 9 (`net9.0`, SDK pinned via `global.json`)              |
| Web framework      | ASP.NET Core Web API (controllers)                           |
| Architecture       | Clean Architecture (Domain / Application / Infrastructure / Api) |
| Persistence        | Entity Framework Core 9 + **SQLite**                         |
| API docs           | Swagger / OpenAPI (Swashbuckle)                              |
| Logging            | Serilog (Console + rolling File sinks)                       |
| Tests              | xUnit, FluentAssertions, Moq                                |

SQLite was chosen because it is zero-configuration and file-based, which keeps the assignment
self-contained (no database server to install) while still exercising real EF Core migrations and
relational behaviour. The provider is isolated behind `AddInfrastructure`, so swapping to
PostgreSQL or SQL Server later is a one-line change plus a connection string.

---

## Solution structure

```
OrderManagement.sln
├─ Directory.Build.props          # shared build settings (net9.0, nullable, warnings-as-errors)
├─ global.json                    # pins the .NET SDK
├─ src/
│  ├─ OrderManagement.Domain          # entities, enums — no dependencies
│  │  ├─ Entities/                     Order, ExternalDocument, SyncLog
│  │  └─ Enums/                        DocumentType, DocumentStatus, SyncStatus
│  ├─ OrderManagement.Application      # use cases, ports (interfaces), DTOs → depends on Domain
│  │  ├─ Abstractions/                 IExternalDocumentProvider, IApplicationDbContext
│  │  ├─ Common/                       Models (DTOs) + Exceptions
│  │  ├─ Features/                     Synchronization (sync + logs), Orders (documents)
│  │  └─ DependencyInjection.cs        # AddApplication()
│  ├─ OrderManagement.Infrastructure   # EF Core, external integrations → depends on Application
│  │  ├─ Persistence/
│  │  │  ├─ AppDbContext.cs            # DbSets + ApplyConfigurationsFromAssembly
│  │  │  ├─ AppDbContextFactory.cs     # design-time factory for `dotnet ef`
│  │  │  ├─ AppDbInitializer.cs        # migrate + seed sample orders on startup
│  │  │  ├─ Configurations/            IEntityTypeConfiguration per entity
│  │  │  └─ Migrations/                EF Core migrations (InitialCreate)
│  │  ├─ ExternalApi/                  JsonExternalDocumentProvider + external-documents.json
│  │  ├─ GoogleSheets/                 CsvGoogleSheetLogger (mock IGoogleSheetLogger)
│  │  └─ DependencyInjection.cs        # AddInfrastructure() — registers DbContext (SQLite)
│  └─ OrderManagement.Api              # composition root → depends on Application + Infrastructure
│     ├─ Controllers/                  Health, Sync, Orders
│     ├─ Contracts/                    request models (PaginationParameters)
│     ├─ Middleware/GlobalExceptionHandler.cs
│     ├─ Program.cs
│     ├─ appsettings.json
│     └─ appsettings.Development.json
└─ tests/
   └─ OrderManagement.Tests           # xUnit
```

Dependency direction (inward only): `Api → Infrastructure → Application → Domain`. The Domain has
no dependencies; the Application defines the abstractions that Infrastructure implements.

---

## Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) (the exact version is pinned in `global.json`).
- EF Core CLI tools (only needed to create/apply migrations):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## How to run

```bash
# from the repository root
dotnet run --project src/OrderManagement.Api
```

The API starts in the `Development` environment by default (see `launchSettings.json`). On startup
it **applies migrations and seeds sample orders** (`ORD-001`/`ORD-002`/`ORD-003`), so it is usable
immediately with no manual EF steps.

- Swagger UI:   <http://localhost:5216/swagger>
- Health check: <http://localhost:5216/api/health>

Sample requests are in [`requests.http`](requests.http) (works with the VS Code REST Client or
JetBrains Rider/VS HTTP client).

### API endpoints

| Method & route                          | Description                                              | Codes |
|-----------------------------------------|----------------------------------------------------------|-------|
| `POST /api/sync/documents`              | Runs a synchronisation; returns the `SyncResult`.        | 200   |
| `GET /api/orders/{orderNumber}/documents` | Documents for an order.                                | 200, 404 |
| `GET /api/sync/logs?page=&pageSize=`    | Sync history, newest first, paged (`pageSize` 1–100).    | 200, 400 |
| `POST /api/sync/logs/latest/send-to-google` | Re-sends the latest sync log to the Google Sheet logger. | 200, 404, 500 |
| `GET /api/health`                       | Liveness probe.                                          | 200   |

A typical first run against the seeded data returns
`received 7, created 6, updated 0, skipped 1` (the document for the non-existent `ORD-999` is the
skipped orphan). Errors are returned as RFC 7807 `ProblemDetails` (e.g. 404 for an unknown order,
400 for invalid paging).

---

## How to run the tests

```bash
dotnet test
```

Tooling: **xUnit + FluentAssertions + Moq**. `DocumentSyncServiceTests` mocks the external
dependencies (document provider, Google Sheet logger) with Moq and runs against a real **SQLite
in-memory** database, so the service's create/update/skip logic is exercised against EF Core without
mocking it (and without testing EF Core itself). Each test gets an isolated database.

Coverage includes: creating new documents, updating on `Status`/`Amount`/`ExternalUpdatedAt`
changes, skipping unchanged documents, skipping orphans (unknown order), multi-document counts,
`SyncLog` creation, the Google Sheet logger being called exactly once, synchronisation surviving a
logger failure, **no data persisted when `SaveChanges` fails** (rollback), empty source, in-batch
duplicate `ExternalId`, and cancellation. Plus two foundation smoke tests (`HealthControllerTests`,
`AppDbContextTests`).

> **Note on FluentAssertions:** pinned to **7.x** (Apache-2.0). Version 8+ moved to a commercial
> licence, so 7.x is used to keep the project free to build and run.

---

## Data model

Three entities (in `OrderManagement.Domain/Entities`), with enums in `Domain/Enums`:

| Entity             | Key             | Notes                                                                 |
|--------------------|-----------------|----------------------------------------------------------------------|
| `Order`            | `Id` (int)      | `OrderNumber` is a unique business key.                               |
| `ExternalDocument` | `Id` (int)      | `ExternalId` has a **unique index** (deduplication). Links to an order. |
| `SyncLog`          | `Id` (Guid)     | The id doubles as the Google Sheets `RunId`.                          |

Modelling decisions:

- **Enums as text** — `DocumentType`, `DocumentStatus` and `SyncStatus` are stored as strings
  (`HasConversion<string>()`) so the database stays human-readable and resilient to enum reordering,
  and the values line up with the external API payloads (`"Invoice"`, `"Signed"`, …).
- **Deduplication** — a unique index on `ExternalDocument.ExternalId` enforces "no duplicates" at
  the database level, not just in code (`IX_ExternalDocuments_ExternalId`, `unique: true`).
- **Money** — `Amount` is `decimal`. SQLite has no native decimal type, so it is stored as `TEXT`
  to preserve precision (the app does not sort/aggregate by amount).
- **Timestamps** — all dates use `DateTimeOffset` (timezone-aware; the external payloads are UTC `…Z`).
  SQLite cannot `ORDER BY`/compare `DateTimeOffset`, so a global `DateTimeOffsetToBinaryConverter`
  (configured in `ConfigureConventions`) stores them as order-preserving `INTEGER` values — this is
  what lets "sync logs ordered by `StartedAt`" run in the database.
- **Order relationship & orphans** — `ExternalDocument` keeps the raw `OrderNumber` from the
  external system plus an **optional internal `OrderId` FK** to `Order` (`onDelete: SetNull`). The
  relationship is optional so an orphan *could* be stored, but the **chosen orphan policy is to skip**
  documents whose order does not exist (counted in `DocumentsSkipped`); during sync the matching
  order is resolved and `OrderId` is set.

## How synchronisation works

`POST /api/sync/documents` invokes `DocumentSyncService` (Application layer), which:

1. reads documents from `IExternalDocumentProvider`. The mock implementation
   (`JsonExternalDocumentProvider`) reads `ExternalApi/external-documents.json` — configurable via
   `ExternalDocumentSource:FilePath`. Swap this single implementation for a real API client without
   touching callers.
2. pre-loads the referenced orders and existing documents (two queries, no N+1), then for each
   incoming document:
   - **no matching order** → skip (orphan policy);
   - **new `ExternalId`** → create;
   - **known `ExternalId`** → update only if `Status`, `Amount` or `ExternalUpdatedAt` changed,
     otherwise skip;
3. writes a `SyncLog` row and persists everything with a single `SaveChanges` (one transaction).

**Deduplication** is enforced two ways: by `ExternalId` lookup in the service, and by the unique
index on `ExternalDocument.ExternalId` at the database level (a re-run with the same data creates no
duplicates — everything is reported as skipped). Within a single batch, a repeated `ExternalId` is
also collapsed so it cannot violate the unique index.

The service uses `async`/`await` with `CancellationToken` throughout and an injected `TimeProvider`
for a testable clock.

### Failure handling

**Every synchronisation attempt records exactly one `SyncLog`**, even on failure:

- **External source unavailable / invalid payload** — the provider exception is caught; the run is
  recorded as `Failed` with `DocumentsReceived = 0` and the error message; the API returns `200`
  with `status: "Failed"`.
- **Partial processing** — if an unexpected error interrupts processing after some documents were
  handled, the run is recorded as `PartialSuccess` with the counts achieved so far and the error.
- **Google Sheets failure** — never rolls back saved documents: the logger is called after the
  commit, inside a `try/catch`, and the error is logged via Serilog.
- **Database failure while persisting** — logged via Serilog and surfaced as a `500` `ProblemDetails`
  (a dead database cannot record its own failure log).

Structured logging (Serilog) covers: run start, run finish (with status + counts), external-source
errors, processing errors, database errors and Google Sheet errors.

## Google Sheets logging

Every run is reported through the `IGoogleSheetLogger` abstraction (Application layer). The shipped
implementation is **`CsvGoogleSheetLogger`** (Infrastructure) — a mock that appends one row per run
to `Logs/sync-log.csv` instead of calling the real Google Sheets API:

- creates the `Logs` folder and `sync-log.csv` automatically, writing the header row once;
- **appends** (never overwrites); asynchronous file I/O; UTF-8 (no BOM);
- RFC 4180 escaping (fields with commas/quotes/newlines are quoted, quotes doubled);
- thread-safe (a `SemaphoreSlim` serialises writes; registered as a singleton).

Columns (per the assignment): `RunId, StartedAt, FinishedAt, Status, DocumentsReceived,
DocumentsCreated, DocumentsUpdated, DocumentsSkipped, ErrorMessage`. See
[`docs/sample-sync-log.csv`](docs/sample-sync-log.csv) for example output (location/name configurable
via the `GoogleSheetLogger` settings section). The `POST /api/sync/documents` response also reports a
`googleSheetStatus` (`"Sent"`/`"Failed"`) for the run's dispatch to this log.

The logger is invoked automatically at the end of every synchronisation (any status). A logging
failure **never breaks the sync**: documents are already committed, and the error is caught and
recorded via Serilog. To use real Google Sheets, replace the single DI registration of
`IGoogleSheetLogger` with an API/Apps-Script implementation — no caller changes required.

## Database & migrations

- **Database:** SQLite. Connection string in `appsettings.json` → `ConnectionStrings:DefaultConnection`
  (default `Data Source=ordermanagement.db`).
- The API **applies migrations and seeds sample orders on startup** (see `AppDbInitializer`), so no
  manual database setup is required to run it.
- The `DbContext` and a design-time factory (`AppDbContextFactory`) are in place; migrations live in
  `src/OrderManagement.Infrastructure/Persistence/Migrations`.
- To create/update the database manually instead (run from the repository root):
  ```bash
  dotnet ef database update \
    --project src/OrderManagement.Infrastructure \
    --startup-project src/OrderManagement.Api
  ```
- To add a further migration after changing the model:
  ```bash
  dotnet ef migrations add <Name> \
    --project src/OrderManagement.Infrastructure \
    --startup-project src/OrderManagement.Api \
    --output-dir Persistence/Migrations
  ```
- Inspect the wired-up context without touching the database:
  ```bash
  dotnet ef dbcontext info \
    --project src/OrderManagement.Infrastructure \
    --startup-project src/OrderManagement.Api
  ```

---

## Configuration & cross-cutting concerns

- **Dependency injection** — each layer exposes a single registration entry point
  (`AddApplication`, `AddInfrastructure`) composed in `Program.cs`.
- **Global exception handling** — `GlobalExceptionHandler` (an `IExceptionHandler`) converts any
  unhandled exception into an RFC 7807 `ProblemDetails` response and logs it.
- **Swagger / OpenAPI** — enabled in Development at `/swagger`.
- **Serilog** — two-stage initialisation (bootstrap logger + host logger). Configuration lives in
  `appsettings.json` under `Serilog`; logs go to the console and to a daily rolling file under
  `logs/` (git-ignored).
- **Secrets** — no secrets are committed. When the real Google integration is added, credentials /
  service-account JSON / webhook URLs must come from user-secrets or environment variables; the
  relevant patterns are already in `.gitignore`.

---

## Roadmap

Maps the assignment requirements to where they live (✅ done, ⬜ planned):

| Requirement                                     | Status | Location                                                  |
|-------------------------------------------------|:------:|-----------------------------------------------------------|
| `Order`, `ExternalDocument`, `SyncLog` entities | ✅     | `Domain/Entities`, `Domain/Enums`                         |
| EF Core mapping + initial migration             | ✅     | `Infrastructure/Persistence`                              |
| Unique constraint on `ExternalId` (dedup)       | ✅     | `Infrastructure/Persistence/Configurations`              |
| Mock external documents source                  | ✅     | `Infrastructure/ExternalApi`                              |
| Sync use case (create/update/skip, SyncLog)     | ✅     | `Application/Features/Synchronization`                    |
| `POST /api/sync/documents`                       | ✅     | `Api/Controllers/SyncController`                         |
| `GET /api/orders/{orderNumber}/documents`        | ✅     | `Api/Controllers/OrdersController`                       |
| `GET /api/sync/logs`                              | ✅     | `Api/Controllers/SyncController`                         |
| `IGoogleSheetLogger` + mock (CSV) implementation | ✅     | `Application/Abstractions` + `Infrastructure/GoogleSheets`|
| `POST /api/sync/logs/latest/send-to-google`      | ✅     | `Api/Controllers/SyncController`                         |
| Error → `SyncLog` handling (source/payload/partial) | ✅  | `Application/Features/Synchronization`                   |
| Business-scenario tests (≥ 5)                    | ✅     | `tests/OrderManagement.Tests`                            |

---

## Known limitations

- The Google Sheets logger is a CSV-file mock by design (swap the single DI registration for a real
  Google Sheets / Apps Script client).
- Sample orders are seeded on startup for demonstration; there is no order-management endpoint.
- No authentication (not required by the assignment).
- A failed `POST /api/sync/documents` returns `200` with `status: "Failed"` (the run completed and was
  recorded); a hard database outage during persistence surfaces as `500`.

---

## Before a production release

- Replace SQLite with a server database (PostgreSQL / SQL Server) — one provider line in
  `AddInfrastructure` plus a connection string; keep migrations.
- Replace the CSV mock with a real Google Sheets / Apps Script client behind the existing
  `IGoogleSheetLogger`, with credentials supplied via secrets/environment variables.
- Replace startup auto-migration/seeding with a controlled migration step in the deployment pipeline.
- Add resilience around the external source (timeouts, retries with backoff) and authentication/
  authorisation on the API.
- Add integration tests (`WebApplicationFactory`) over the HTTP endpoints in addition to the unit tests.

---

## AI tools used

- **Claude Code** — used throughout: scaffolding the Clean Architecture solution and cross-cutting
  infrastructure (DI, Serilog, Swagger, global exception handling, EF Core/SQLite), the domain model
  and EF mappings + migration, the synchronisation service and its failure handling, the REST
  endpoints, the Google Sheets (CSV) logger, the xUnit test suite, and this README. All generated
  code was reviewed and the solution was verified to build warning-free, run, and pass its tests.
