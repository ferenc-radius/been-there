# Copilot instructions for this repository

## Architecture decisions

All resolved design decisions live in `docs/adrs/`. Read the relevant ADR before touching a subsystem:

| ADR | Topic |
|-----|-------|
| [0001](../docs/adrs/0001-solution-structure.md) | Multi-project layout (Web / Core / Infrastructure) |
| [0002](../docs/adrs/0002-multi-user-scope.md) | Multi-user scope |
| [0003](../docs/adrs/0003-auth-and-token-storage.md) | Google OAuth, ASP.NET Core Identity, EF Core Global Query Filters |
| [0004](../docs/adrs/0004-drive-storage-contract.md) | Google Drive storage, file naming, no local cache |
| [0005](../docs/adrs/0005-import-pipeline.md) | GPX (SharpGpx) + KML parser, transaction boundary |
| [0006](../docs/adrs/0006-geometry-model.md) | `RoutePoint` table, PostGIS indexes, SRID 4326 |
| [0007](../docs/adrs/0007-map-interop.md) | Leaflet JS isolation module, tile provider preference |
| [0008](../docs/adrs/0008-duplicate-detection.md) | Two-stage Hausdorff duplicate flagging, IHostedService background job |
| [0009](../docs/adrs/0009-docker-and-ci.md) | Docker Compose, .env secrets, GHCR image tagging |
| [0010](../docs/adrs/0010-import-help-ui.md) | In-app GPX export guidance (AllTrails, Locus Map) |

## High-level architecture

Three-project solution: `BeenThere.Web` → `BeenThere.Core` ← `BeenThere.Infrastructure`. Dependency direction is enforced; Web and Infrastructure must not reference each other.

Key data flow: drag-and-drop import → SharpGpx/KML parse → Drive upload → Postgres commit (`Route` + `RoutePoint` rows) → background duplicate detection job.

## Contract safety rules

- **Never expose `drive_file_id`** in a public-facing DTO or Blazor binding. Downloads go through `/api/routes/{routeId}/download`.
- **All user-owned entity queries** are filtered by EF Core Global Query Filter — never add a manual `WHERE user_id =` to compensate for a missing filter. Fix the filter instead.
- **`RouteSample` is a retired term** — the table and C# entity are called `RoutePoint`.
- **`.env` must never be committed** — only `.env.example` with placeholder values.

---

## Clean Code principles (Uncle Bob)

Apply these rules to every file touched. They are non-negotiable for code review.

### Naming
- Names must reveal intent. If you need a comment to explain a name, rename it.
- Classes and methods have one purpose — the name describes that purpose completely.
- No abbreviations (`dist` → `distanceMetres`; `calc` → `calculateElevationGain`).
- Boolean names start with `is`, `has`, `can`, or `should` (`isImported`, `hasDuplicates`).
- Avoid noise words: `Manager`, `Helper`, `Util`, `Data` tell you nothing — name the actual responsibility.

### Functions / methods
- **Do one thing.** If you can extract a meaningful named step, extract it.
- **No flag arguments.** A `bool` parameter is a signal the method does two things — split it.
- **Max 3 parameters.** Group related parameters into a named object.
- **Command–Query Separation.** A method either changes state *or* returns a value — never both.
- Keep methods short: aim for ≤ 20 lines; anything over 40 lines must be refactored.

### Classes
- **Single Responsibility Principle.** One reason to change.
- Prefer small, focused classes over large ones. A class with > 200 lines is a warning sign.
- Hide implementation details. Expose only what callers need.

### Comments
- **Good code is self-documenting.** Do not write comments that restate what the code does.
- Acceptable comments: *why* a non-obvious decision was made, public API doc-comments (`///`), and `// TODO:` with a tracking reference.
- Never commit commented-out code.

### Error handling
- Use exceptions for exceptional conditions — not flow control.
- Throw specific, meaningful exception types. Catch only what you can handle.
- Never swallow exceptions silently (`catch (Exception) { }`).

### Tests (when added)
- One assertion per test concept.
- Test names: `MethodName_Scenario_ExpectedResult`.
- Arrange / Act / Assert structure, separated by blank lines.

---

## C# code guidelines

### Language and style
- Target **C# 14 on .NET 10**. Use new language features (field-backed properties `field` keyword, improved pattern matching, `params` collections, `allows ref struct` constraints) where they genuinely improve clarity — do not use new syntax just to be clever.
- Use `var` when the type is obvious from the right-hand side; use explicit types for non-obvious declarations.
- Prefer `record` for immutable DTOs and value objects. Prefer `readonly struct` for small stack-allocated values.
- Use `required` properties and primary constructors to eliminate nullable init footguns.
- Null safety: enable `<Nullable>enable</Nullable>` in all projects. No `!` null-forgiveness operator without a comment explaining why it is safe.

### Async
- All I/O is `async`/`await`. No `.Result` or `.Wait()` — these cause deadlocks in Blazor Server.
- Propagate `CancellationToken` from the call site all the way to EF Core and HTTP clients.
- Use `ConfigureAwait(false)` in library/infrastructure code (not in Blazor components).

### Dependency Injection
- Register services with the narrowest lifetime that is correct (`Scoped` for DbContext; `Singleton` for background services; `Transient` for lightweight stateless services).
- Inject interfaces, never concrete infrastructure types, into `BeenThere.Core`.
- Never use `IServiceLocator` / `GetService<T>()` outside of startup code.

### EF Core
- Use `AsNoTracking()` on all read-only queries.
- Bulk inserts use `AddRangeAsync` + single `SaveChangesAsync` — never insert in a loop.
- Migrations are generated with `dotnet ef migrations add <PascalCaseName>` — name them descriptively (`AddRoutePointIndexes`, not `Update1`).
- Never call `Database.EnsureCreated()` — use `MigrateAsync()` on startup (ADR-0009).

### Project-layer rules
| Layer | May reference | Must NOT reference |
|-------|--------------|-------------------|
| `BeenThere.Core` | Nothing (pure domain) | Web, Infrastructure, EF Core, Drive SDK |
| `BeenThere.Infrastructure` | Core | Web, ASP.NET Core HTTP context |
| `BeenThere.Web` | Core, Infrastructure (via DI) | Direct EF Core queries, Drive SDK calls |

### Blazor Server specifics
- Components are scoped to the circuit — never store user state in `Singleton` services.
- Use `@inject` only for services registered for Blazor (`Scoped` or `Transient`).
- JS interop calls go through the `map-interop.js` module (`IJSObjectReference`) — no inline `<script>` blocks.
- Mark long-running event handlers with `async Task` and `await` properly; never `async void` except for Blazor lifecycle overrides.

### Formatting
- 4-space indentation, no tabs.
- One class per file; file name matches class name.
- Blank line between methods; no blank lines at the start/end of a method body.
- `using` directives: system namespaces first, then third-party, then project — each group separated by a blank line (enforced by `.editorconfig`).

---

## Build, test, and lint

No commands defined yet — update this section when the solution is scaffolded.

---

## Copilot behaviour rules

- **Ask before changing scope.** If a request is ambiguous or completing it requires touching code outside the stated scope, stop and ask — do not guess.
- **Surgical changes only.** Change exactly what was requested. Do not refactor, rename, reformat, or "improve" surrounding code unless explicitly asked.
- **No invented requirements.** Do not add features, fields, validations, or error cases that were not asked for — even if they seem obviously useful.
- **Cite evidence.** When making a claim about how the codebase works, quote the relevant file and line. Do not assert from memory.

### Dependency version policy

Always resolve the **latest stable version** before adding or updating any dependency. Never accept a pinned version from memory, docs examples, or copy-paste without first verifying it is current.

| Dependency type | How to verify latest |
|---|---|
| NuGet packages | Query `https://api.nuget.org/v3-flatcontainer/{package-id-lowercase}/index.json` — take the last entry with no pre-release suffix |
| Docker images | Query `https://registry.hub.docker.com/v2/repositories/{image}/tags/` — filter for numeric stable tags (no `master`, `rc`, `alpine` suffix unless explicitly requested) |
| npm packages | `npm view {package} version` or `https://registry.npmjs.org/{package}/latest` |

Rules:
- **`Directory.Packages.props`** is the single source of truth for all NuGet versions — never add `Version=` to a `<PackageReference>` in a `.csproj` file.
- When bumping a version, update `Directory.Packages.props` only. Do not touch individual `.csproj` files.
- **Docker image tags** must be pinned to a specific stable tag (e.g., `postgis/postgis:17-3.5`), never `latest` — `latest` is non-deterministic.
- After resolving a version, record it. Do not re-query the same package in the same session.

---

## Security rules

### Input validation
- Validate all user input at the boundary (Blazor component or API controller). Nothing from the outside world is trusted.
- Use FluentValidation in `BeenThere.Core` for domain-level validation rules. Do not scatter `if` guards across the call stack.
- File uploads: validate MIME type and extension; reject any file that is not `.gpx` or `.kml`/`.kmz` before parsing begins.
- Max file size enforced at the Blazor `InputFile` level and at Kestrel (`MultipartBodyLengthLimit`).

### Injection
- All database access goes through EF Core parameterised queries. Raw SQL (`FromSqlRaw`, `ExecuteSqlRaw`) is only permitted in migrations and must never interpolate user input.
- PostGIS geometry values are always passed as `NetTopologySuite` objects — never string-interpolated into SQL.

### Secrets
- No secrets, connection strings, client IDs, or tokens in source code or `appsettings.json` committed to the repository.
- Secrets come from environment variables or `.env` file only (see ADR-0009).
- Refresh tokens are stored in `AspNetUserTokens` (encrypted at rest by Identity) — never logged, never included in responses.

### Web security (Blazor Server)
- CSRF is mitigated by Blazor Server's SignalR circuit — do not bypass this by exposing raw form POSTs without antiforgery.
- Content Security Policy header must be set in production.
- `X-Content-Type-Options: nosniff` and `X-Frame-Options: DENY` headers required.

---

## Logging policy

- Use `ILogger<T>` via constructor injection everywhere. Never use `Console.WriteLine` or `Debug.WriteLine` in production paths.
- Use **structured logging** with named properties: `_logger.LogInformation("Route imported {RouteId} for user {UserId}", route.Id, userId)` — not string interpolation.
- **Never log:** refresh tokens, access tokens, passwords, full connection strings, Drive file IDs, or any PII (names, emails in message text).
- Log levels:
  - `Debug` — internal state useful during development only
  - `Information` — significant business events (import started, import completed, duplicate flagged)
  - `Warning` — recoverable anomalies (orphaned Drive file detected, duplicate threshold exceeded by margin)
  - `Error` — unhandled exceptions, Drive upload failures, DB commit failures
- Exceptions logged with `LogError(ex, "...")` include the full exception — do not log `ex.Message` only.

---

## Configuration pattern

- All app settings are bound to strongly-typed option classes via `IOptions<T>` — no raw `IConfiguration["key"]` access inside services.
- Option classes live in `BeenThere.Core` and carry no infrastructure dependencies.
- Example bindings to add at startup: `services.Configure<DuplicateDetectionOptions>(config.GetSection("DuplicateDetection"))`.
- Validate options eagerly on startup with `ValidateDataAnnotations().ValidateOnStart()` so misconfiguration fails fast.

---

## Validation

- Domain validation rules live in `BeenThere.Core` using **FluentValidation**.
- One `IValidator<T>` per command/DTO.
- Validation runs before any service call — never inside the repository or EF Core entity.
- Validation errors surface to the user as structured messages, not exceptions. Use `ValidationResult` to collect all failures before presenting them.
- Infrastructure constraints (e.g. DB unique index violations) are a last-resort safety net, not the primary validation strategy.

---

## Pagination

- Every query that returns a collection is paginated. There is no endpoint or service method that returns an unbounded list.
- Standard page shape: `{ items: [...], totalCount: int, page: int, pageSize: int }`.
- Default page size: 50. Maximum allowed: 200.
- Spatial/filtered queries apply the `LIMIT`/`OFFSET` after all `WHERE` predicates to keep index usage efficient.

---

## Performance rules

- **No N+1 queries.** If loading a route with points, use `.Include()` or a single join — never load points in a loop.
- Use `.Include()` only when the navigation property is actually needed by the caller — lazy loading is disabled.
- Spatial queries must use index-eligible predicates: `ST_DWithin` on geography columns (uses GIST) before any `ST_Distance` ordering.
- Expensive background work (duplicate detection) runs in `IHostedService` — never block a Blazor circuit waiting for it.
- Cache nothing in memory unless a benchmark justifies it. The current scale does not require caching.

---

## Error responses (API endpoints)

- All API error responses use **RFC 9457 `ProblemDetails`** shape:
  ```json
  { "type": "...", "title": "...", "status": 404, "detail": "Route not found." }
  ```
- Register `services.AddProblemDetails()` and use `Results.Problem(...)` / `TypedResults.Problem(...)` in minimal API handlers.
- Never return raw exception messages or stack traces to the client in production (`ASPNETCORE_ENVIRONMENT != Development`).
- Validation failures return `400` with a `ValidationProblemDetails` containing per-field error arrays.

---

## File and folder structure

```
src/
  BeenThere.Web/
    Components/           # Blazor components (.razor + .razor.cs code-behind)
      Shared/             # Layout, NavMenu, reusable UI components
      Pages/              # Routable pages (@page)
      Map/                # Map-specific components
      Import/             # Import flow components
    wwwroot/
      js/                 # map-interop.js and any other JS modules
      css/
    Controllers/          # Minimal API or controller endpoints (download, GeoJSON)
    Program.cs
  BeenThere.Core/
    Domain/               # Entity classes (Route, RoutePoint, etc.)
    Interfaces/           # IRouteRepository, IDriveService, IGpxParser, etc.
    Services/             # Pure domain services (no I/O)
    Validation/           # FluentValidation validators
    Options/              # IOptions<T> configuration classes
  BeenThere.Infrastructure/
    Data/                 # ApplicationDbContext, EF configurations, migrations
    Repositories/         # EF Core implementations of Core interfaces
    Drive/                # Google Drive wrapper
    Parsing/              # SharpGpxParser, XDocumentKmlParser
    BackgroundJobs/       # IHostedService implementations
tests/
  BeenThere.UnitTests/    # Pure unit tests (no DB, no HTTP)
  BeenThere.IntegrationTests/ # EF Core + Testcontainers/PostGIS tests
```

- Blazor components use **code-behind** (`.razor` + `.razor.cs`) for any logic beyond trivial bindings.
- Feature-based grouping inside `Components/` — not type-based (`Pages/`, `Modals/`, etc. mixed by feature).

---

## Git workflow

- Branch naming: `feature/<short-description>`, `fix/<short-description>`, `chore/<short-description>`.
- Commit messages follow **Conventional Commits**: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`.
  - Example: `feat: add GPX drag-and-drop import UI`
  - Breaking changes: append `!` after type and add `BREAKING CHANGE:` footer.
- Commits are atomic — one logical change per commit.
- Do not commit directly to `main`. All changes go through a PR.

## Pull request expectations

This is a private single-developer project — PR process is lightweight:
- PRs are optional for small changes; direct commits to `main` are acceptable for solo work.
- When a PR is opened: title follows Conventional Commits format and description covers what changed and why.
- CI must pass before merging.


