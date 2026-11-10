# Construction Estimator

A slice of a construction cost-estimating tool. A project has estimates; an estimate is a list of
lines priced from an effective-dated rate catalogue. An estimate moves through the
Draft → Submitted → Approved/Rejected lifecycle, with role- and data-based authorisation throughout.

- **Backend:** ASP.NET Core Web API on .NET 10 (LTS), EF Core 10, SQL Server, JWT bearer auth, Swagger
- **Frontend:** Angular 22 (standalone components, signals, Reactive Forms, Angular Material)
- **Docs:** [Design and assumptions](docs/DESIGN.md) · [Requirements traceability](docs/REQUIREMENTS.md) ·
  [ADRs](docs/ADR) · [AI use](docs/AI-USE.md)

## Architecture

```
backend/src
  ConstructionEstimator.Domain          entities, value objects, state machine, pricing — no dependencies
  ConstructionEstimator.Application     use cases, authorisation rules, DTOs, ports (interfaces)
  ConstructionEstimator.Infrastructure  EF Core + SQL Server, migrations, queries, seed data
  ConstructionEstimator.API             controllers, JWT, policies, ProblemDetails, logging, Swagger
backend/tests
  ConstructionEstimator.UnitTests          domain rules + application authorisation (64 tests)
  ConstructionEstimator.ArchitectureTests  layer rules, project-file rules, migration drift (24 tests)
frontend/construction-estimator-ui      Angular SPA
```

Dependencies point inward (API → Application → Domain; Infrastructure → Application). Architecture
tests fail if that is broken. See [DESIGN.md §3](docs/DESIGN.md#3-architecture) for the diagram.

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0.1xx |
| Node.js | 22 LTS or newer (npm 10+) |
| SQL Server | Docker Desktop (compose file provided) **or** SQL Server / LocalDB on Windows |

## Run it

### 1. Database (SQL Server)

```bash
docker compose up -d          # SQL Server 2022 on localhost:1433, sa / Estimator_Dev_P@ssw0rd
```

**No Docker, on Windows?** Use LocalDB (ships with Visual Studio) by overriding the connection string:

```powershell
$env:ConnectionStrings__Default = "Server=(localdb)\MSSQLLocalDB;Database=ConstructionEstimator;Trusted_Connection=True;TrustServerCertificate=True"
```

In Development the API **applies EF Core migrations and seeds data on startup**
(`Database:MigrateAndSeedOnStartup`), so no manual step is needed.

To create the schema by hand instead, run [`docs/database/InitialCreate.sql`](docs/database/InitialCreate.sql)
(the script generated from the migration). To use the `dotnet ef` CLI, first add the design-time package,
which was left out because it wasn't available offline when this was built:

```bash
dotnet tool install --global dotnet-ef
cd backend
dotnet add src/ConstructionEstimator.API package Microsoft.EntityFrameworkCore.Design --version 10.0.1
dotnet ef database update --project src/ConstructionEstimator.Infrastructure --startup-project src/ConstructionEstimator.API
```

### 2. API

```bash
cd backend
dotnet run --project src/ConstructionEstimator.API      # http://localhost:5080, Swagger at /swagger
```

### 3. UI

```bash
cd frontend/construction-estimator-ui
npm install
npm start                                               # http://localhost:4200 (proxies /api → :5080)
```

Open http://localhost:4200 and pick a user.

### Quick look without SQL Server

```bash
cd backend
dotnet run --project src/ConstructionEstimator.API -- --Database:Provider=InMemory
```

This uses EF's in-memory provider. Data is lost on restart, and **database constraints and concurrency
tokens are not enforced** in this mode. It is for a quick look only; the real persistence is SQL Server.

### 50 000-line estimate (R19)

Run `dotnet run --project src/ConstructionEstimator.API -- --Seed:LargeEstimateLines=50000` on a **fresh** database. This adds INF-003
`EST-0002`, visible to `estimator2`, `reviewer2` and `manager1`. Drop the database first to re-seed:
`docker compose down -v`.

## Test credentials (development only)

There is no registration or password; the brief asks for a user picker. The API still issues a signed
JWT and validates it on every request.

| User | Role | Assigned projects | Try this |
|---|---|---|---|
| estimator1@example.com | Estimator | Residential, Commercial | Edit RES `EST-0001`, then submit it. No labour columns. Cannot see Infrastructure. |
| estimator2@example.com | Estimator | Commercial, Infrastructure | COM `EST-0002` has a future pricing date, so submit fails (R9). Reopen the rejected COM `EST-0003`. |
| reviewer1@example.com | Reviewer | Residential, Commercial | Approve RES `EST-0002`, then approve again (idempotent). COM `EST-0001` was created by reviewer1, so they cannot approve it (R16). |
| reviewer2@example.com | Reviewer | Commercial, Infrastructure | Approve COM `EST-0001` (allowed, they didn't create it). |
| viewer1@example.com | Viewer | Residential | Read only. |
| manager1@example.com | Project Manager | All three | Sees labour cost. Corrects the description of approved RES `EST-0003` (R11). |

Catalogue: `CONC-C30`, `STEEL-REBAR`, `LABOUR-GEN`, `BRICK-STD`, `EXCAVATION`. `CONC-C30`,
`STEEL-REBAR` and `LABOUR-GEN` have a second rate from 1 Jul 2026, so a March estimate and an August
estimate price concrete at 1 450.00 and 1 512.50 respectively (R2).

## Tests

```bash
cd backend && dotnet test                                    # 88 tests: domain, application, architecture
cd frontend/construction-estimator-ui && npx ng test --watch=false   # 5 tests: validators, auth interceptor
```

To see the structural enforcement fail, add
`<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.1" />` to
`ConstructionEstimator.Domain.csproj` and run `dotnet test`.

## API

Swagger UI: http://localhost:5080/swagger (click **Authorize** and paste `accessToken` from `/api/auth/login`).

| Method | Route | Who | Notes |
|---|---|---|---|
| POST | `/api/auth/login` | anyone | `{ "email": "…" }` → JWT |
| GET | `/api/auth/users` | anyone (Development only) | user picker |
| GET | `/api/projects` | any role | assigned projects with estimate summaries (R18) |
| POST | `/api/projects/{projectId}/estimates` | Estimator | create Draft |
| GET | `/api/estimates/{id}` | assigned users | header, DB-computed totals, caller's capabilities |
| GET | `/api/estimates/{id}/lines?page=&pageSize=` | assigned users | paged (default 100, max 500) — R19 |
| GET | `/api/estimates/{id}/history` | assigned users | audit trail (R13) |
| POST | `/api/estimates/{id}/lines` | Estimator | add, or adjust the existing line for that item (R7): 201 / 200 |
| PUT | `/api/estimates/{id}/lines/{lineId}` | Estimator | Draft only |
| DELETE | `/api/estimates/{id}/lines/{lineId}` | Estimator | Draft only |
| POST | `/api/estimates/{id}/submit` | Estimator | R9 |
| POST | `/api/estimates/{id}/approve` | Reviewer | idempotent (R12), not own (R16) |
| POST | `/api/estimates/{id}/reject` | Reviewer | `{ "reason": "…" }` |
| POST | `/api/estimates/{id}/reopen` | Estimator | Rejected → Draft |
| PUT | `/api/estimates/{id}/description` | Project Manager | any status, audited (R11) |
| GET | `/api/catalogue?pricingDate=&search=` | any role | priced on date (R2); labour only for Reviewer/PM (R17) |

Errors are RFC 7807 ProblemDetails with a stable `code` and the `traceId`:
400 invalid input · 401 no/invalid token · 403 role or R16 · 404 missing *or not assigned* ·
409 invalid transition / locked / concurrent change · 422 business rule (R9, no rate on date).

## Known limitations

- On SQL Server, startup (migration and seeding) has been verified; the end-to-end HTTP checks were run
  against the in-memory provider only.
- No integration tests against SQL Server (constraints and concurrency are verified present, not
  exercised). No CI pipeline.
- Development-only auth: no passwords, token in `sessionStorage`, symmetric dev signing key in
  `appsettings.Development.json`.
- Changing an estimate's pricing date (which means re-pricing every line) is not built.
- No `If-Match`/ETag lost-update protection across browser sessions. Optimistic concurrency protects
  against races within a request.
- Line edits are not audited individually. Lines are listed in insertion order only (no sort or search).
- More in [DESIGN.md §5](docs/DESIGN.md#5-scope).

## Time spent

| Phase | Time (IST, 10 Nov 2026) |
|---|---|
| Analysis, `DESIGN.md`, ADRs (committed before any code) | 13:42 – 13:48 |
| Backend, tests, frontend, docs (AI-assisted, see [AI-USE.md](docs/AI-USE.md)) | 13:48 – 14:22 |
| First local run on SQL Server and fixes (package audit, seeder transaction) | 15:30 – 16:00 |

Commit timestamps reflect these phases. Additional review time is not included.
