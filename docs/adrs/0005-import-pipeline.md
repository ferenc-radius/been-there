# ADR-0005: Import pipeline design

**Status:** Accepted  
**Decisions:** D, D2, D3

## Context

Users drag and drop GPX and KML files. The app must parse each file, extract geometry and telemetry, store the original in Drive, and persist normalised data in Postgres.

## Decision

### Parsers
| Format | Library |
|--------|---------|
| GPX (1.0 / 1.1) | **SharpGpx** NuGet package — covers track/route/waypoint semantics and Garmin extensions |
| KML | Custom thin parser using `System.Xml.Linq` (`XDocument`) — KML geometry is simple enough that a ~50-line parser beats an unmaintained dependency |

### Fields extracted and normalised
All formats are normalised to:
- `Route`: name, date, mode, total `distance_m`, `elev_gain_m`, `geom` (LineString), `centroid` (Point), `original_filename`, `drive_file_id`, `tags` (jsonb), `notes`
- `RoutePoint` (one row per GPS point): `route_id`, `idx`, `geom` (Point, SRID 4326), `recorded_at`, `elevation_m`, `hr_bpm` *(nullable)*, `cadence_rpm` *(nullable)*, `power_w` *(nullable)*

HR, cadence, and power are extracted from Garmin/TrainingPeaks GPX extensions when present. KML files will have null telemetry columns.

### Transaction boundary
```
parse file
  → upload to Drive (get fileId)
    → SaveChangesAsync() [Route + RoutePoints in one transaction]
      → enqueue duplicate detection job
```
On Drive failure: surface error to user, no DB write.  
On DB failure after Drive upload: orphaned Drive file — user can re-import to recover.

## Consequences

- SharpGpx must be added to `BeenThere.Infrastructure`.
- `RoutePoint` bulk inserts should use `AddRange` + single `SaveChangesAsync` (consider `Npgsql COPY` if import latency is unacceptable at scale).
- The duplicate detection job is enqueued after the transaction commits (see ADR-0008).
