# ADR-0008: Duplicate route detection

**Status:** Accepted  
**Decisions:** G, G2, G3

## Context

Users may import the same route multiple times (re-walked, exported from different devices). The app must flag likely duplicates without blocking the import flow or running expensive geometry comparisons on all route pairs.

## Decision

### Algorithm: two-stage spatial comparison
1. **Pre-filter (cheap):** `ST_DWithin(centroid, <new centroid>, 500m)` AND `distance_m` within ±10% of the new route.  
2. **Hausdorff check (precise):** compute `ST_HausdorffDistance(a.geom, b.geom)` between the new route's `LineString` and each pre-filter candidate.

Routes where Hausdorff distance ≤ threshold are flagged `is_potential_duplicate = true`.

### Threshold
Default: **100 metres**, stored as a configurable app setting (`DuplicateDetection:HausdorffThresholdMetres`). Adjust after observing false positive/negative rates on real tracks.

### Trigger
Detection runs as a **background job** triggered automatically after each successful import:
- Import handler enqueues the new `routeId` to an `IHostedService` `Channel<Guid>`.
- The hosted service dequeues and runs the two-stage check.
- Results surface as a badge/notification in the UI (Blazor component polls or uses SignalR).
- No Redis or Hangfire needed for MVP.

## Consequences

- `is_potential_duplicate` is a soft flag only — no routes are deleted or merged automatically.
- Manual merge is explicitly out of scope for this release.
- The 100 m threshold may need tuning; the configurable setting makes this a runtime change, not a deployment.
- At large scale (>10 k routes) the pre-filter must use the `routes_centroid_idx` GIST index (see ADR-0006) to stay fast.
