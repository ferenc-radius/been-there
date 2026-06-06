# ADR-0002: Multi-user scope

**Status:** Accepted  
**Decisions:** A1

## Context

The original MVP specification targeted a single personal Google account. During design review the scope was expanded to support multiple independent users on the same deployment.

## Decision

BeenThere supports multiple users. Every user-owned entity carries a `user_id` foreign key. All data access is scoped per-user at the EF Core layer (see ADR-0003). Google Drive storage is inherently per-OAuth-user via `appDataFolder`, so no additional isolation is needed at the Drive level.

## Consequences

- ASP.NET Core Identity is required (see ADR-0003).
- All route queries must carry a user filter; omitting it must be a conscious, auditable decision.
- The Docker Compose deployment remains single-instance; there is no multi-tenancy isolation beyond the DB row-level filter.
