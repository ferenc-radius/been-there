# ADR-0011: Testing strategy

**Status:** Accepted  
**Decisions:** T, T2, T3

## Context

BeenThere has multiple integration points (Google Drive, PostGIS, Blazor Server), each with distinct failure modes. Testing must catch logic errors early (unit tests) and verify end-to-end flows (integration tests) without being slow or flaky.

## Decision

### Unit tests
- **Target:** `DriveService`, parsers (GPX, KML), domain services (duplicate detection), repository query logic.
- **Mocking:** External dependencies (Google Drive client, HTTP calls) are mocked; in-memory test data for services.
- **Framework:** xUnit with Moq for mocks.
- **Coverage goal:** >80% for public interfaces; edge cases and error paths prioritized over line coverage.

### Integration tests
- **Target:** End-to-end flows: Google OAuth sign-in → token persistence → Drive folder creation; GPX import → Route + RoutePoint rows in PostGIS; duplicate detection algorithm with real geometry.
- **Database:** Real PostGIS instance (via Docker Compose in test setup or GitHub Actions CI).
- **Approach:** 
  - Create a test-scoped `ApplicationDbContext` with test data seeding.
  - Each test class sets up its own test data and tears down after (no cross-test pollution).
  - Use `IAsyncLifetime` to manage Docker Compose lifecycle in CI.
- **Framework:** xUnit with testcontainers or custom Docker-based setup.
- **Coverage goal:** One happy-path test per major user story (import, search, map rendering) plus one error-path test per story.

### Deferred testing
- **E2E / UI tests:** Deferred to post-MVP. Manual QA + user feedback in early beta sufficient for Blazor Server components.
- **Performance tests:** Deferred until scale testing is needed (e.g., >10 k routes).
- **Load testing:** Out of scope for MVP single-instance deployment.

### Test project structure
```
tests/
├── BeenThere.Core.Tests/           # Unit tests for domain models, interfaces
├── BeenThere.Infrastructure.Tests/  # Unit tests for parsers, Drive client (mocked)
├── BeenThere.Web.Tests/             # Unit tests for Blazor components, if any
└── BeenThere.Integration.Tests/     # Integration tests (real DB, real Drive if test credentials available)
```

## Consequences

- Each milestone adds unit + integration tests; test suite grows with features.
- Integration tests require a PostGIS instance; CI uses Docker to spin up and tear down after each test run.
- Drive integration tests may use a test Google service account (credentials stored securely in CI secrets); alternatively, mock Drive entirely and defer Drive integration to manual QA.
- Developers must run `dotnet test` before pushing; CI enforces this with pre-merge checks.
