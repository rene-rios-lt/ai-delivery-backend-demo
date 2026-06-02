# service-request-api

.NET 9 REST API for managing service requests. Built with Clean Architecture — a domain model at the core, an application service layer above it, EF Core + PostgreSQL in infrastructure, and a thin ASP.NET Core controller layer at the edge.

---

## Tech stack

| Layer               | Technology                                                |
|---------------------|-----------------------------------------------------------|
| Runtime             | .NET 9 / C#                                               |
| Web framework       | ASP.NET Core                                              |
| ORM                 | Entity Framework Core                                     |
| Database            | PostgreSQL 16                                             |
| Unit testing        | xUnit + NSubstitute + FluentAssertions                    |
| Integration testing | xUnit + Testcontainers.PostgreSql + WebApplicationFactory |
| Container           | Docker (multi-stage build)                                |

---

## Architecture

The solution is organised into four projects in Clean Architecture layering order (inner → outer):

```
ServiceRequest.Domain           # Entities, enums — no dependencies
    └── ServiceRequest.Application  # DTOs (records), IServiceRequestRepository, ServiceRequestService
            └── ServiceRequest.Infrastructure  # EF Core AppDbContext, repository, DataSeeder
                    └── ServiceRequest.Api      # Controllers, middleware, Program.cs
```

### Domain model

`ServiceRequestEntity` is the core aggregate:

| Field         | Type            | Notes                                                    |
|---------------|-----------------|----------------------------------------------------------|
| `Id`          | `Guid`          | Auto-generated                                           |
| `Title`       | `string`        | Required                                                 |
| `Description` | `string?`       | Optional                                                 |
| `Status`      | `RequestStatus` | `Open` \| `InProgress` \| `Completed` — stored as string |
| `Priority`    | `Priority`      | `Low` \| `Medium` \| `High` — stored as string           |
| `Requester`   | `User`          | Navigation property — eagerly loaded                     |
| `Requestee`   | `User`          | Navigation property — eagerly loaded                     |
| `CreatedAt`   | `DateTime`      | UTC                                                      |
| `UpdatedAt`   | `DateTime`      | UTC                                                      |

### Key conventions

- **Primary constructor injection** throughout — not field injection.
- **DTOs are C# records** in `ServiceRequest.Application/DTOs/`. `ServiceRequestDto` intentionally omits `Status` and `Priority`.
- **`UpdateAsync` uses a mutation callback** — the service passes `Action<ServiceRequestEntity>` rather than a replacement object.
- **`Status` and `Priority` stored as strings** via `.HasConversion<string>()` — not as integers.
- **All repository reads eagerly load navigation properties** via the `WithIncludes` computed property.
- **`DataSeeder` is an `IHostedService`** that seeds on startup; integration tests remove it with `services.RemoveAll<IHostedService>()`.

---

## API endpoints

Base path: `/api/service-requests`

| Method   | Path           | Description                                                               |
|----------|----------------|---------------------------------------------------------------------------|
| `GET`    | `/`            | Return all service requests                                               |
| `GET`    | `/top-pending` | Return top 3 Open or InProgress requests                                  |
| `GET`    | `/{id}`        | Return a single request by GUID — `404` if not found                      |
| `POST`   | `/`            | Create a new service request — returns `201 Created` with location header |
| `PUT`    | `/{id}`        | Update title, description, status, priority — `404` if not found          |
| `DELETE` | `/{id}`        | Delete a request — `204 No Content` or `404`                              |

---

## Getting started

### Option A — Docker Compose (recommended)

Starts the API and a PostgreSQL 16 instance. The database is seeded with sample data on first run.

```bash
docker compose up
```

The API is available at `http://localhost:5001`.

> The database schema is created via `EnsureCreated()` on startup — no migrations to run.

### Option B — Local development

#### Prerequisites

- .NET 9 SDK
- PostgreSQL 16 running locally (or point at any reachable instance)

#### 1. Configure the connection string

The default connection string in `appsettings.json` points to a local PostgreSQL instance:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=servicerequestdb;Username=postgres;Password=postgres"
}
```

Override via environment variable (double-underscore separator for nested keys):

```bash
export ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<pass>"
```

#### 2. Build

```bash
dotnet build ServiceRequest.slnx
```

#### 3. Run

```bash
cd src/ServiceRequest.Api
dotnet run
```

The API starts on `http://localhost:5000` (HTTP) and `https://localhost:5001` (HTTPS) by default.

---

## Commands

| Command                                                | Description                                          |
|--------------------------------------------------------|------------------------------------------------------|
| `docker compose up`                                    | Start API + PostgreSQL (full local stack)            |
| `dotnet build ServiceRequest.slnx`                     | Build entire solution                                |
| `dotnet test ServiceRequest.slnx`                      | Run all tests (unit + integration — Docker required) |
| `dotnet test tests/ServiceRequest.Application.Tests`   | Unit tests only (no Docker required)                 |
| `dotnet test tests/ServiceRequest.Api.Tests`           | Integration tests only (Docker required)             |
| `dotnet test --filter "FullyQualifiedName~<TestName>"` | Run a single test by name                            |

---

## Testing

### Unit tests — `ServiceRequest.Application.Tests`

Test `ServiceRequestService` in isolation. No database or Docker required.

- **Mocking**: NSubstitute
- **Assertions**: FluentAssertions

```bash
dotnet test tests/ServiceRequest.Application.Tests
```

### Integration tests — `ServiceRequest.Api.Tests`

Test the full HTTP stack (routing, controllers, EF Core, PostgreSQL).

- **Infrastructure**: Testcontainers.PostgreSql — spins up a real PostgreSQL container per test run
- **Host**: `WebApplicationFactory<Program>`
- **Schema**: `db.Database.EnsureCreated()` — no migrations
- **DataSeeder removed**: `services.RemoveAll<IHostedService>()` prevents seed race conditions

```bash
dotnet test tests/ServiceRequest.Api.Tests
```

> **Docker must be running** for integration tests. If Docker is unavailable, run unit tests only.

---

## Project structure

```
src/
├── ServiceRequest.Domain/
│   ├── Entities/
│   │   ├── ServiceRequestEntity.cs
│   │   └── User.cs
│   └── Enums/
│       ├── RequestStatus.cs    # Open | InProgress | Completed
│       └── Priority.cs         # Low | Medium | High
├── ServiceRequest.Application/
│   ├── DTOs/                   # C# records: ServiceRequestDto, CreateServiceRequestDto, UpdateServiceRequestDto
│   ├── Interfaces/
│   │   └── IServiceRequestRepository.cs
│   └── Services/
│       └── ServiceRequestService.cs
├── ServiceRequest.Infrastructure/
│   ├── AppDbContext.cs
│   ├── ServiceRequestRepository.cs
│   ├── DataSeeder.cs           # IHostedService — seeds data on startup
│   └── DependencyInjection.cs  # Extension method called from Program.cs
└── ServiceRequest.Api/
    ├── Controllers/
    │   └── ServiceRequestsController.cs
    ├── Middleware/
    ├── Program.cs
    └── appsettings.json
tests/
├── ServiceRequest.Application.Tests/   # Unit tests
└── ServiceRequest.Api.Tests/           # Integration tests
```

---

## Docker

Build and run the API image standalone (without Compose):

```bash
docker build -t service-request-api .
docker run -p 5001:8080 \
  -e ConnectionStrings__DefaultConnection="<your-connection-string>" \
  service-request-api
```

In production, the image is pushed to ECR and run on ECS Fargate — see [service-request-infra](../service-request-infra).
