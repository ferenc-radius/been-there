# ADR-0001: Multi-project solution structure

**Status:** Accepted  
**Decisions:** A

## Context

The BeenThere codebase needs to accommodate Blazor Server UI, EF Core + PostGIS data access, Google Drive integration, and GPX/KML parsing in a single C# solution. A flat single-project layout would colocate all concerns without enforced boundaries.

## Decision

Use a three-project layout:

| Project | Responsibility |
|---------|----------------|
| `BeenThere.Web` | Blazor Server UI, controllers, JS interop |
| `BeenThere.Core` | Domain models, interfaces, service contracts |
| `BeenThere.Infrastructure` | EF Core DbContext, repositories, Drive client, parsers |

Dependency direction: `Web → Core ← Infrastructure`. Neither `Web` nor `Infrastructure` may reference each other.

## Consequences

- Domain logic in `Core` is testable without Blazor or EF references.
- Adding a future API project requires only a reference to `Core`.
- Slightly more initial scaffolding cost (~15 minutes), paid back immediately when Drive, PostGIS, and Leaflet interop coexist.
