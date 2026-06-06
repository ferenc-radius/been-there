# ADR-0007: Map interop and tile provider

**Status:** Accepted  
**Decisions:** F, F2

## Context

The map experience (Leaflet, polylines, clustering, heatmap) runs in the browser. Blazor Server has no direct DOM access, so a JS bridge is needed. Users should be able to switch between OSM and OpenTopoMap tile layers persistently.

## Decision

### JS interop
Use a **single `map-interop.js` JS isolation module** loaded via `IJSObjectReference`:

```csharp
_mapModule = await JS.InvokeAsync<IJSObjectReference>(
    "import", "./js/map-interop.js");
```

All Leaflet operations (init map, add polylines, add heatmap, cluster markers, fly-to) are exposed as named exports in this one module. Components call it via the shared reference — no global `window.*` pollution.

### Tile provider preference
The active tile layer (OSM / OpenTopoMap) is a **per-user persistent preference** stored in a `user_preferences jsonb` column on the `AspNetUsers`-extended user record:

```json
{ "tileProvider": "opentopomap", "defaultZoom": 12 }
```

The map loads the user's saved preference on init and writes back on toggle. The `jsonb` column is intentionally open for future preferences (units, default map centre, etc.) without schema migrations.

## Consequences

- `map-interop.js` is the single point of Leaflet API surface; changes to Leaflet version only touch this file.
- `user_preferences` jsonb should be validated with a C# record/DTO on read to avoid silent deserialization surprises.
- `IJSObjectReference` must be disposed in `IAsyncDisposable` on the host Blazor component.
