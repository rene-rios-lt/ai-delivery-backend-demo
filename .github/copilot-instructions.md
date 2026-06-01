# Copilot Instructions — service-request-api

.NET 9 / C# REST API following Clean Architecture. PostgreSQL via EF Core. Runs locally with Docker Compose.

---

## Commands

```bash
# Start full local stack (API on :5001, PostgreSQL on :5432)
docker compose up

# Build
dotnet build ServiceRequest.slnx

# Run all tests (both unit and integration)
dotnet test ServiceRequest.slnx

# Run only unit tests — no Docker required
dotnet test tests/ServiceRequest.Application.Tests

# Run only integration tests — Docker must be running (Testcontainers)
dotnet test tests/ServiceRequest.Api.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~GetAll_Returns200"

# Add an EF Core migration (run from repo root)
# Requires dotnet-ef: dotnet tool install --global dotnet-ef
dotnet ef migrations add <MigrationName> \
  --project src/ServiceRequest.Infrastructure \
  --startup-project src/ServiceRequest.Api

# Apply migrations manually
dotnet ef database update \
  --project src/ServiceRequest.Infrastructure \
  --startup-project src/ServiceRequest.Api
```

> `ServiceRequest.slnx` is the new .NET solution format (`.slnx`, not `.sln`). Use `dotnet` CLI — older tooling may not recognize it.

---

## Architecture

Four projects in Clean Architecture layering order (inner → outer). Dependencies only point inward.

```
ServiceRequest.Domain          ← no dependencies
ServiceRequest.Application     ← depends on Domain only
ServiceRequest.Infrastructure  ← depends on Application + Domain
ServiceRequest.Api             ← depends on Application + Infrastructure
```

| Project | Responsibilities |
|---|---|
| **Domain** | `ServiceRequestEntity`, `User`, `RequestStatus` enum, `Priority` enum |
| **Application** | DTOs (C# records), `IServiceRequestRepository` interface, `ServiceRequestService` |
| **Infrastructure** | `AppDbContext`, `ServiceRequestRepository`, `DataSeeder`, `DependencyInjection.cs` |
| **Api** | Controllers, `ExceptionHandlingMiddleware`, `Program.cs` |

`DependencyInjection.cs` in Infrastructure exposes `AddInfrastructure(IConfiguration)` — called from `Program.cs` to register all Infrastructure services (DbContext, repository, DataSeeder).

> **Registration split**: Application-layer services (e.g. `ServiceRequestService`) are registered **directly in `Program.cs`** via `builder.Services.AddScoped<ServiceRequestService>()`. Only Infrastructure services go through `AddInfrastructure`. Follow this split when adding new services.

> **`public partial class Program {}`** at the bottom of `Program.cs` must not be removed — it is required for `WebApplicationFactory<Program>` in `ServiceRequest.Api.Tests` to compile across assembly boundaries.

---

## API Endpoints

```
GET    /api/service-requests              — all requests, ordered by CreatedAt desc
GET    /api/service-requests/top-pending  — top 3 Open or InProgress, ordered by CreatedAt asc (oldest first)
GET    /api/service-requests/{id}         — single request by GUID; 404 if not found
POST   /api/service-requests              — create; returns 201 with Location header
PUT    /api/service-requests/{id}         — partial update; returns 200 or 404
DELETE /api/service-requests/{id}         — returns 204 or 404
GET    /health                            — health check; returns { "status": "healthy" }
```

Swagger UI: `/swagger` (Development only).

---

## Domain Model

```csharp
// Enums — stored as strings in DB (not integers)
enum RequestStatus { Open, InProgress, Completed }
enum Priority      { Low, Medium, High }
```

`ServiceRequestEntity` has two `User` navigation properties: `Requester` (who raised the request) and `Requestee` (who is assigned).

---

## DTOs

All DTOs are C# `record` types in `ServiceRequest.Application/DTOs/`.

| DTO | Purpose | Notable |
|---|---|---|
| `ServiceRequestDto` | Read response | Omits `Status` and `Priority` — intentional |
| `CreateServiceRequestDto` | POST body | `Title` is `[Required]`; `Priority` is non-nullable (required, defaults to `Low` if omitted); `Status` defaults to `Open` on the entity |
| `UpdateServiceRequestDto` | PUT body | All fields nullable — only non-null fields are applied |

> `ServiceRequestDto` deliberately excludes `Status` and `Priority`. Adding either field requires changes in all four layers: entity → DTO record → `ToDto` mapping in service → TypeScript type in the UI.

---

## Key Conventions

**Dependency injection**
- Use **primary constructor injection** throughout: `public class Foo(IBar dep)` — never field injection or constructor body assignment.

**Repository pattern**
- `IServiceRequestRepository` is the only data abstraction — service classes depend on this interface, never on `AppDbContext` directly.
- **`UpdateAsync` takes a mutation callback**: `repository.UpdateAsync(id, entity => { entity.Title = ...; })`. Pass an `Action<ServiceRequestEntity>` — do not replace the entity.
- **All reads use `WithIncludes`**: every repository query goes through the `WithIncludes` computed property (`db.ServiceRequests.Include(r => r.Requester).Include(r => r.Requestee)`). Never query `db.ServiceRequests` directly.

**EF Core**
- `Status` and `Priority` stored as strings via `.HasConversion<string>()` — never as integers.
- Schema uses `EnsureCreated()` in tests and `MigrateAsync()` in production (via `DataSeeder`).
- Tables: `service_requests`, `users`.

**DataSeeder**
- Implemented as `IHostedService`; runs `MigrateAsync()` then seeds on startup if the `users` table is empty.
- Seeds **5 users** and **43 requests** (15 Open, 15 InProgress, 13 Completed) using Bogus with fixed seed `42` — results are deterministic.
- Integration tests remove it with `services.RemoveAll<IHostedService>()` to prevent race conditions.

**Error handling**
- `ExceptionHandlingMiddleware` exists in `ServiceRequest.Api/Middleware/` but is **not currently registered** in `Program.cs`. Unhandled exceptions will return ASP.NET Core's default problem details response. Do not assume the middleware is active unless you first add `app.UseMiddleware<ExceptionHandlingMiddleware>()` to `Program.cs`.

**CORS**
- Configured with `AllowAnyOrigin / AllowAnyHeader / AllowAnyMethod` — intentionally permissive for local development.

---

## Testing

### Unit tests — `ServiceRequest.Application.Tests`

- Framework: **xUnit**
- Mocking: **NSubstitute** (`Substitute.For<IServiceRequestRepository>()`)
- Assertions: **FluentAssertions**
- No Docker required; no database.
- Pattern: construct `ServiceRequestService` with a substituted repository, arrange return values with `.Returns(...)`, assert with `.Should()`.

### Integration tests — `ServiceRequest.Api.Tests`

- Framework: **xUnit** + **Testcontainers.PostgreSql** + **WebApplicationFactory\<Program\>**
- **Docker must be running** — Testcontainers spins up `postgres:16-alpine` automatically.
- Schema created with `db.Database.EnsureCreated()` — no migrations run in tests.
- `ServiceRequestApiFactory` is shared across all tests via `IClassFixture` — the same container is reused. **Seed data accumulates across tests; never assume an empty database.**
- Factory seeds two users (`requester` / `requestee`) in `InitializeAsync()`. Use `factory.RequesterId` / `factory.RequesteeId` in test setups.
- Use `factory.SeedRequestAsync(requesterId, requesteeId)` to insert test data.

### Adding new tests

- New unit tests go in `ServiceRequest.Application.Tests/ServiceRequestServiceTests.cs` (or a new file in the same project).
- New integration tests go in `ServiceRequest.Api.Tests/ServiceRequestsControllerTests.cs` (or a new file sharing the same factory).
- Follow the existing `MakeUser()` / `MakeEntity()` factory helper pattern in unit tests.
