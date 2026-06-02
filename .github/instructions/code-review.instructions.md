---
applyTo: '**/*.cs'
---

# Code Review Instructions — service-request-api

Review at **Senior Engineer** level. Enforce the patterns in `copilot-instructions.md`. Do not suggest alternatives to established patterns.

---

## 🔴 Block — Reject the PR

**Clean Architecture layering**
- Code in `ServiceRequest.Domain` imports or references any `Application`, `Infrastructure`, or `Api` namespace. Dependencies only point inward.
- Code in `ServiceRequest.Application` imports or references any `Infrastructure` or `Api` namespace.
- A controller or middleware in `ServiceRequest.Api` calls `AppDbContext` directly — all data access must go through the service layer.
- A service class references `AppDbContext` directly — services may only depend on `IServiceRequestRepository`.

**Dependency injection**
- Field injection or constructor-body assignment is used instead of primary constructor syntax. Correct: `public class Foo(IBar dep)`.

**Repository pattern**
- `UpdateAsync` is called by passing a replacement entity instead of a mutation callback (`Action<ServiceRequestEntity>`). Correct: `repository.UpdateAsync(id, e => { e.Title = ...; })`.
- Any query accesses `db.ServiceRequests` directly instead of going through the `WithIncludes` computed property. Navigation properties (`Requester`, `Requestee`) must always be eagerly loaded.

**EF Core**
- `Status` or `Priority` is stored as an integer (e.g., `HasConversion<int>()` or no conversion). Both enums must use `.HasConversion<string>()`.

**Service registration**
- Infrastructure services (DbContext, repository, DataSeeder) are registered directly in `Program.cs` instead of inside `AddInfrastructure`. They must go through `DependencyInjection.cs`.
- Application-layer services (e.g., `ServiceRequestService`) are registered inside `AddInfrastructure` instead of directly in `Program.cs` via `builder.Services.AddScoped<>()`.

**`public partial class Program {}`**
- Removed from the bottom of `Program.cs`. This declaration is required for `WebApplicationFactory<Program>` to compile across assembly boundaries in `ServiceRequest.Api.Tests`.

**DTOs**
- A new DTO is defined as a `class` instead of a `record`. All DTOs in `ServiceRequest.Application/DTOs/` must be C# records.
- `Status` or `Priority` is added to `ServiceRequestDto` without updating all four layers (entity → DTO record → `ToDto` mapping in service → TypeScript type in the UI) in the same PR. These fields are intentionally absent from the read DTO.

**Tests**
- Integration tests do not call `services.RemoveAll<IHostedService>()` in `WebApplicationFactory` configuration. Without this, `DataSeeder` runs concurrently and causes race conditions.
- An integration test asserts on an absolute count or assumes an empty database (e.g., `Count == 0`). The factory is shared via `IClassFixture` — seed data accumulates across tests in the same run.
- Integration tests call `db.Database.MigrateAsync()` instead of `db.Database.EnsureCreated()`. Tests use `EnsureCreated`; `MigrateAsync` is for production startup.
- Unit tests use Moq (`new Mock<>()`) instead of NSubstitute (`Substitute.For<>()`).
- Unit test assertions use `Assert.Equal` / `Assert.True` instead of FluentAssertions (`.Should()`).

---

## 🟡 Require — Must Be Present Before Merge

- Every new `ServiceRequestService` method must have at least one unit test in `ServiceRequest.Application.Tests`.
- Every new API endpoint must have at least one integration test in `ServiceRequest.Api.Tests`.
- New integration tests must use `factory.SeedRequestAsync(requesterId, requesteeId)` and `factory.RequesterId` / `factory.RequesteeId` for data setup — not hardcoded GUIDs.
- New DTOs must be C# `record` types placed in `ServiceRequest.Application/DTOs/`.
- New repository methods added to `IServiceRequestRepository` must have a corresponding implementation in `ServiceRequestRepository` in the same PR.

---

## 🔵 Flag — Warn, Do Not Block

- New controller action returns `IActionResult` instead of a typed `ActionResult<T>`. Typed results enable accurate Swagger schema generation.
- New public controller action is missing XML doc comments (`/// <summary>`). Swagger uses these for endpoint descriptions.
- New async service or repository method omits a `CancellationToken ct = default` parameter.
- `ExceptionHandlingMiddleware` is referenced or instantiated without first adding `app.UseMiddleware<ExceptionHandlingMiddleware>()` to `Program.cs`. The middleware exists but is not currently registered.
- `any` or `object` used in a new method signature without an inline comment explaining why a typed alternative is not feasible.
