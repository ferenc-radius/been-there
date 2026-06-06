# ADR-0006: Geometry and data model

**Status:** Accepted  
**Decisions:** E, E2

## Context

Route geometry must support spatial queries (radius search, proximity), ordered point retrieval (elevation profiles), and telemetry analytics (average HR, max power). The original spec proposed a separate `RouteSample` table for proximity; that name was unclear.

## Decision

### Table: `route_points`
Single unified table replacing the original `RouteSample` spec term:

```sql
CREATE TABLE route_points (
    id          bigserial PRIMARY KEY,
    route_id    uuid        NOT NULL REFERENCES routes(id) ON DELETE CASCADE,
    idx         int         NOT NULL,           -- zero-based order within track
    geom        geometry(Point, 4326) NOT NULL,
    recorded_at timestamptz,
    elevation_m double precision,
    hr_bpm      smallint,                       -- nullable
    cadence_rpm smallint,                       -- nullable
    power_w     smallint                        -- nullable
);
```

### Table: `routes` (core fields)
```sql
id uuid PK, user_id text, name text, date timestamptz,
mode text, distance_m double precision, elev_gain_m double precision,
geom geometry(LineString,4326), centroid geometry(Point,4326),
drive_file_id text, original_filename text,
tags jsonb, notes text,
is_potential_duplicate boolean DEFAULT false,
created_at timestamptz, updated_at timestamptz
```

### Indexes
| Index | Type | Purpose |
|-------|------|---------|
| `route_points_geom_idx` | GIST on `geom` | `ST_DWithin` proximity / radius queries |
| `route_points_route_idx` | btree on `(route_id, idx)` | Ordered point retrieval for elevation profile |
| `routes_centroid_idx` | GIST on `centroid` | Fast centroid proximity pre-filter |
| `routes_distance_idx` | btree on `distance_m` | Length filter |

### SRID
All geometry stored in **SRID 4326** (WGS 84). Use `::geography` cast for metric distance calculations in PostGIS.

## Consequences

- `RouteSample` terminology is retired; use `RoutePoint` everywhere in C# and SQL.
- Bulk insert of `RoutePoint` rows is the import bottleneck; monitor and optimise with `COPY` if needed.
- Telemetry columns are nullable — KML imports will always have null HR/cadence/power.
