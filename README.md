# Order Management — Document Sync

Service for synchronising financial documents (invoices and acts of work) between the internal
**Order Management** system and an external system, with sync history and a Google Sheets log.

> **Project status — Phase 1: foundation.**
> This commit contains the production-ready **solution skeleton and cross-cutting infrastructure**
> only. Business logic (the actual document synchronisation, deduplication, Google Sheets
> integration and the domain entities) is **not implemented yet** and is tracked in the
> [Roadmap](#roadmap). The current code builds with **zero warnings**, runs, exposes a Health
> endpoint and Swagger UI, and has passing tests.

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
│  │  ├─ Entities/                     (added in Phase 2)
│  │  └─ Enums/
│  ├─ OrderManagement.Application      # use cases, ports (interfaces), DTOs → depends on Domain
│  │  ├─ Abstractions/                 (ports, e.g. IGoogleSheetLogger — Phase 2)
│  │  ├─ Common/
│  │  ├─ Features/
│  │  └─ DependencyInjection.cs        # AddApplication()
│  ├─ OrderManagement.Infrastructure   # EF Core, external integrations → depends on Application
│  │  ├─ Persistence/
│  │  │  ├─ AppDbContext.cs
│  │  │  ├─ AppDbContextFactory.cs     # design-time factory for `dotnet ef`
│  │  │  └─ Configurations/            (IEntityTypeConfiguration — Phase 2)
│  │  ├─ ExternalApi/                  (mock external documents client — Phase 2)
│  │  ├─ GoogleSheets/                 (IGoogleSheetLogger impl — Phase 2)
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

## Database & migrations

- **Database:** SQLite. Connection string in `appsettings.json` → `ConnectionStrings:DefaultConnection`
  (default `Data Source=ordermanagement.db`).
- The `DbContext` and a design-time factory (`AppDbContextFactory`) are in place, so EF Core
  migration tooling is fully wired. Verify it with:
  ```bash
  dotnet ef dbcontext info \
    --project src/OrderManagement.Infrastructure \
    --startup-project src/OrderManagement.Api
  ```
- No migration exists yet because there are no entities. Once the domain entities are added
  (Phase 2), create and apply the first migration:
  ```bash
  dotnet ef migrations add InitialCreate \
    --project src/OrderManagement.Infrastructure \
    --startup-project src/OrderManagement.Api

  dotnet ef database update \
    --project src/OrderManagement.Infrastructure \
    --startup-project src/OrderManagement.Api
  ```
  Migrations are generated into the **Infrastructure** project.

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

Maps the remaining assignment requirements to where they will live:

| Requirement                                   | Location (planned)                                            |
|-----------------------------------------------|---------------------------------------------------------------|
| `Order`, `ExternalDocument`, `SyncLog` entities | `Domain/Entities`, `Domain/Enums`                            |
| Unique constraint on `ExternalId` (dedup)     | `Infrastructure/Persistence/Configurations`                   |
| Mock external documents source                | `Infrastructure/ExternalApi`                                  |
| `IGoogleSheetLogger` + mock implementation    | `Application/Abstractions` + `Infrastructure/GoogleSheets`    |
| Sync use case (create/update/skip, SyncLog)   | `Application/Features`                                        |
| `POST /api/sync/documents`                     | `Api/Controllers`                                            |
| `GET /api/orders/{orderNumber}/documents`      | `Api/Controllers`                                            |
| `GET /api/sync/logs`                            | `Api/Controllers`                                            |
| `POST /api/sync/logs/latest/send-to-google`    | `Api/Controllers`                                            |
| Business-scenario tests (≥ 5)                  | `tests/OrderManagement.Tests`                                |

---

## Known limitations (current phase)

- No domain model, synchronisation logic, or business endpoints yet — Health only.
- No migration committed yet (intentional: there is no schema to migrate).
- No authentication (not required by the assignment).

---

## AI tools used

- **Claude Code** — used to scaffold the Clean Architecture solution, wire up the cross-cutting
  infrastructure (DI, Serilog, Swagger, global exception handling, EF Core/SQLite), pin
  net9-compatible package versions, and produce this README. All generated code was reviewed and
  the solution was verified to build warning-free and pass its tests.
