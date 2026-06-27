# Order Management — Document Sync

Service for synchronising financial documents (invoices and acts of work) between the internal
**Order Management** system and an external system, with sync history and a Google Sheets log.

> **Project status — Phase 2: domain model + persistence.**
> The production-ready solution skeleton, cross-cutting infrastructure, the **domain model**
> (`Order`, `ExternalDocument`, `SyncLog` + enums) and the **EF Core mapping with an initial
> migration** are in place. The remaining **business logic** (the actual document synchronisation,
> the mock external source, the Google Sheets integration and the business endpoints) is **not
> implemented yet** and is tracked in the [Roadmap](#roadmap). The current code builds with
> **zero warnings**, runs, exposes a Health endpoint and Swagger UI, and has passing tests.

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
| Tests              | xUnit                                                        |

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
│  │  ├─ Abstractions/                 (ports, e.g. IGoogleSheetLogger — next phase)
│  │  ├─ Common/
│  │  ├─ Features/
│  │  └─ DependencyInjection.cs        # AddApplication()
│  ├─ OrderManagement.Infrastructure   # EF Core, external integrations → depends on Application
│  │  ├─ Persistence/
│  │  │  ├─ AppDbContext.cs            # DbSets + ApplyConfigurationsFromAssembly
│  │  │  ├─ AppDbContextFactory.cs     # design-time factory for `dotnet ef`
│  │  │  ├─ Configurations/            IEntityTypeConfiguration per entity
│  │  │  └─ Migrations/                EF Core migrations (InitialCreate)
│  │  ├─ ExternalApi/                  (mock external documents client — next phase)
│  │  ├─ GoogleSheets/                 (IGoogleSheetLogger impl — next phase)
│  │  └─ DependencyInjection.cs        # AddInfrastructure() — registers DbContext (SQLite)
│  └─ OrderManagement.Api              # composition root → depends on Application + Infrastructure
│     ├─ Controllers/HealthController.cs
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

The API starts in the `Development` environment by default (see `launchSettings.json`):

- Swagger UI:   <http://localhost:5216/swagger>
- Health check: <http://localhost:5216/api/health>

Sample requests are in [`requests.http`](requests.http) (works with the VS Code REST Client or
JetBrains Rider/VS HTTP client).

---

## How to run the tests

```bash
dotnet test
```

Current tests are foundation smoke tests:

1. `HealthControllerTests` — the Health endpoint returns a `Healthy` status.
2. `AppDbContextTests` — the EF Core SQLite provider can create and connect to a database.

The business-scenario tests required by the assignment (dedup, status update, orphan handling,
SyncLog on error, Google Sheets dispatch, etc.) are added in Phase 2 alongside the logic they cover.

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
- **Order relationship & orphans** — `ExternalDocument` keeps the raw `OrderNumber` from the
  external system plus an **optional internal `OrderId` FK** to `Order` (`onDelete: SetNull`).
  Making the relationship optional means a document that references an unknown order can still be
  stored (an *orphan*) without violating a foreign key. The concrete orphan-handling policy
  (resolve / auto-create / leave orphan) is a business rule and will be decided and documented when
  the synchronisation logic is implemented.

## Database & migrations

- **Database:** SQLite. Connection string in `appsettings.json` → `ConnectionStrings:DefaultConnection`
  (default `Data Source=ordermanagement.db`).
- The `DbContext` and a design-time factory (`AppDbContextFactory`) are in place; migrations live in
  `src/OrderManagement.Infrastructure/Persistence/Migrations`.
- **Create the database** from the committed migration (run from the repository root):
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
| Mock external documents source                  | ⬜     | `Infrastructure/ExternalApi`                              |
| `IGoogleSheetLogger` + mock implementation      | ⬜     | `Application/Abstractions` + `Infrastructure/GoogleSheets`|
| Sync use case (create/update/skip, SyncLog)     | ⬜     | `Application/Features`                                    |
| `POST /api/sync/documents`                       | ⬜     | `Api/Controllers`                                        |
| `GET /api/orders/{orderNumber}/documents`        | ⬜     | `Api/Controllers`                                        |
| `GET /api/sync/logs`                              | ⬜     | `Api/Controllers`                                        |
| `POST /api/sync/logs/latest/send-to-google`      | ⬜     | `Api/Controllers`                                        |
| Business-scenario tests (≥ 5)                    | ⬜     | `tests/OrderManagement.Tests`                            |

---

## Known limitations (current phase)

- No synchronisation logic, external source, Google Sheets integration, or business endpoints yet —
  Health only. The domain model and database schema are in place.
- The orphan-handling policy is intentionally not yet decided (see [Data model](#data-model)).
- No authentication (not required by the assignment).

---

## AI tools used

- **Claude Code** — used to scaffold the Clean Architecture solution, wire up the cross-cutting
  infrastructure (DI, Serilog, Swagger, global exception handling, EF Core/SQLite), design the
  domain model and EF Core mappings, generate the initial migration, pin net9-compatible package
  versions, and produce this README. All generated code was reviewed and the solution was verified
  to build warning-free, apply its migration, and pass its tests.
