# BeenThere — Implementation Plan

> Last updated: 2026-06-06  
> Status: Ready to execute — all architectural decisions resolved (ADR-0001 through ADR-0010)

---

## Overview

BeenThere is a multi-user Blazor Server web app that imports GPX/KML routes, stores originals in Google Drive `appDataFolder`, indexes route geometry and metadata in PostgreSQL/PostGIS, and lets each user search and visualise their routes on a Leaflet map.

**Solution layout (ADR-0001)**
```
BeenThere.sln
├── src/
│   ├── BeenThere.Web/           # Blazor Server, controllers, JS interop
│   ├── BeenThere.Core/          # Domain models, interfaces, service contracts
│   └── BeenThere.Infrastructure/ # EF Core, PostGIS, Drive API, parsers
├── tests/
├── docs/
│   └── adrs/                    # Architecture Decision Records
├── docker-compose.yml
├── Dockerfile
├── .env.example
└── .github/workflows/
```

---

## Milestone 1 — Solution scaffold

**Goal:** runnable skeleton with database connectivity and user sign-in — including the local dev environment from day one.

### Tasks

#### Dev environment (do this first)
> `docs/` (ADRs + this plan) already exists at repo root — no action needed.

- [ ] `.env.example` at repo root with placeholder values for `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `ConnectionStrings__Default`
- [ ] Copy `.env.example` → `.env` locally; add `.env` to `.gitignore` and `.dockerignore`
- [ ] `docker-compose.yml` at repo root with **`db` service only** for local development:
  ```yaml
  services:
    db:
      image: postgis/postgis:15-3.3
      environment:
        POSTGRES_USER: beenthere
        POSTGRES_PASSWORD: ${DB_PASSWORD}
        POSTGRES_DB: beenthere_db
      ports:
        - "5432:5432"
      volumes:
        - db_data:/var/lib/postgresql/data
      restart: unless-stopped
  volumes:
    db_data:
  ```
- [ ] `docker compose up -d db` — PostGIS running before writing a line of app code

#### Solution
- [ ] `dotnet new sln -n BeenThere`
- [ ] Create three projects: `BeenThere.Web` (Blazor Server), `BeenThere.Core` (class library), `BeenThere.Infrastructure` (class library)
- [ ] Add project references: `Web → Core`, `Infrastructure → Core`
- [ ] NuGet packages:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `NetTopologySuite`
  - `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`
  - `Google.Apis.Auth.AspNetCore3`
  - `Google.Apis.Drive.v3`
  - `SharpGpx`
- [ ] `ApplicationDbContext : IdentityDbContext` with `AspNetUserTokens` for OAuth token storage (ADR-0003)
- [ ] Domain models in `BeenThere.Core`:
  - `Route` (`Id`, `UserId`, `Name`, `Date`, `Mode`, `DistanceM`, `ElevGainM`, `DriveFileId`, `Tags`, `Notes`, `DuplicateOfId`, `CreatedAt`, `UpdatedAt`)
  - `RoutePoint` (`Id`, `RouteId`, `Idx`, `Geom`, `RecordedAt`, `ElevationM`, `HrBpm`, `CadenceRpm`, `PowerW`) (ADR-0006)
  - `UserPreferences` (`UserId`, `TileProvider`, `ImportHelpDismissed`) stored as jsonb column (ADR-0007, ADR-0010)
- [ ] EF Core Global Query Filters on `Route` and `RoutePoint` keyed to `IHttpContextAccessor` current user ID (ADR-0003)
- [ ] PostGIS indexes (ADR-0006):
  - GIST on `route_points.geom`
  - Btree composite on `(route_id, idx)`
  - GIST on `routes.centroid`
  - Btree on `routes.distance_m`, `routes.date`
- [ ] `MigrateAsync()` on application startup in `Program.cs` (ADR-0009)
- [ ] Basic Blazor layout shell (nav, empty pages: Home/Map, Import, Profile)
- [ ] Health check endpoint `/health`

### Done when
`docker compose up -d db` starts PostGIS, `dotnet run` connects, runs migrations, and renders the empty shell.

---

## Milestone 2 — Google OAuth + Drive storage

**Goal:** users can sign in with Google and the app can read/write their Drive `appDataFolder`.

### Tasks
- [ ] Configure Google OAuth in `Program.cs` (ADR-0003):
  ```csharp
  builder.Services.AddAuthentication()
      .AddGoogle(o => {
          o.ClientId = config["GOOGLE_CLIENT_ID"];
          o.ClientSecret = config["GOOGLE_CLIENT_SECRET"];
          o.SaveTokens = true;
          o.Scope.Add("https://www.googleapis.com/auth/drive.appdata");
      });
  ```
- [ ] Persist refresh token to `AspNetUserTokens` on `OnTokenValidated` event
- [ ] `IDriveService` interface in `BeenThere.Core`; `DriveService` implementation in `BeenThere.Infrastructure`
- [ ] Drive folder-per-user creation on first upload: `appDataFolder/{userId}/` (ADR-0004)
- [ ] File naming convention: `{routeId}_{sanitised-name}.{ext}` (ADR-0004)
- [ ] `driveFileId` is internal — downloads MUST route through `/api/routes/{routeId}/download` (ADR-0004 — **contract safety rule**)
- [ ] Token auto-refresh: rely on Google client library; persist refreshed token back to `AspNetUserTokens` (ADR-0003)
- [ ] Sign-in / Sign-out Blazor components
- [ ] `.env.example` with `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `ConnectionStrings__Default` placeholder values

### Done when
User can sign in with their Google account and the app can successfully create a folder in Drive `appDataFolder`.

---

## Milestone 3 — Import pipeline

**Goal:** drag-and-drop GPX/KML import stores files in Drive and writes route geometry to PostGIS.

### Tasks
- [ ] Import Blazor page with drag-and-drop file input (`InputFile` component)
- [ ] Collapsible **"How do I get my GPX files?"** help section (ADR-0010):
  - AllTrails steps (primary)
  - Locus Map 4 steps (secondary)
  - Collapse state persisted in `user_preferences.importHelpDismissed`
- [ ] `IGpxParser` in `BeenThere.Core`; `SharpGpxParser` in `BeenThere.Infrastructure` (ADR-0005)
- [ ] `IKmlParser` in `BeenThere.Core`; `XDocumentKmlParser` in `BeenThere.Infrastructure` (ADR-0005)
- [ ] Parsed output normalised to `ParsedRoute` DTO:
  - Metadata: `Name`, `Date`, `Mode`, `DistanceM`, `ElevGainM`
  - Points: `List<RoutePointData>` (`Idx`, `Lon`, `Lat`, `ElevationM`, `RecordedAt`, `HrBpm`, `CadenceRpm`, `PowerW`)
- [ ] Import transaction boundary (ADR-0005):
  1. Parse file in memory
  2. Upload original to Drive → capture `fileId`
  3. Single `SaveChangesAsync()` writing `Route` + bulk-insert `RoutePoint` records
  4. Enqueue duplicate detection job (fire-and-forget)
- [ ] `Route.Centroid` computed as `ST_Centroid(geom)` during import
- [ ] Import progress/error feedback in UI

### Done when
A GPX drag-drop creates a `Route` row, `RoutePoint` rows in PostGIS, and an original file in Drive `appDataFolder`.

---

## Milestone 4 — Map UI

**Goal:** all user routes rendered on an interactive map with clustering and heatmap.

### Tasks
- [ ] `wwwroot/js/map-interop.js` JS isolation module via `IJSObjectReference` (ADR-0007)
  - Exposed methods: `initMap`, `addRoutes`, `setTileProvider`, `zoomToRoute`, `toggleHeatmap`, `toggleClusters`
- [ ] Blazor `MapComponent.razor` wrapping the interop module
- [ ] Load all user route LineStrings as GeoJSON from a `/api/routes/geojson` endpoint
- [ ] Leaflet polylines for each route
- [ ] `leaflet.markercluster` for route centroid clustering
- [ ] `leaflet.heat` heatmap layer toggle
- [ ] OSM / OpenTopoMap tile provider toggle, preference saved to `user_preferences` (ADR-0007)
- [ ] Route click → opens route detail panel

### Done when
Map shows all imported routes as polylines, clusters centroid markers, and the tile toggle works.

---

## Milestone 5 — Route detail, search, and analytics

**Goal:** users can drill into a route, filter the library, and see aggregate stats.

### Tasks
- [ ] Route detail page: name, date, distance, elevation gain, mode, tags, notes
- [ ] Elevation profile chart (rendered from `RoutePoint.ElevationM` series) — lightweight SVG or JS chart
- [ ] GPX download endpoint `/api/routes/{routeId}/download` (streams from Drive; **no `driveFileId` in response**)
- [ ] Search / filter panel (ADR-0006 spatial queries):
  - Radius search: `ST_DWithin` on `centroid` geography
  - Length range: btree on `distance_m`
  - Elevation gain range
  - Tags (jsonb `@>`)
  - Date last walked range
- [ ] Analytics page: total km, km/year bar chart, most repeated routes (by start/end proximity)
- [ ] Tag editing on route detail

### Done when
User can filter the library by radius + length, open a route detail with elevation profile, and download the original GPX.

---

## Milestone 6 — Duplicate detection

**Goal:** background job flags likely duplicates for each newly imported route.

### Tasks
- [ ] `IDuplicateDetectionService` interface in `BeenThere.Core`
- [ ] `IHostedService` background queue consumer in `BeenThere.Infrastructure` (ADR-0008)
- [ ] Detection algorithm (ADR-0008):
  1. Pre-filter: `ST_DWithin` centroid within 500 m AND `distance_m` within ±10%
  2. Per candidate: compute Hausdorff distance on full LineStrings
  3. Flag pair if Hausdorff ≤ `DuplicateThresholdM` (default `100`, configurable in `appsettings.json`)
- [ ] `Route.DuplicateOfId` FK (nullable) set when flagged
- [ ] Duplicate badge shown on map/library for flagged routes
- [ ] `appsettings.json` key: `"DuplicateDetection": { "ThresholdMetres": 100 }`

### Done when
Importing a route that is within 100 m Hausdorff of an existing route results in a duplicate flag visible in the UI.

---

## Milestone 7 — Dockerfile, production Compose, and CI

**Goal:** app is containerised and a GitHub Actions workflow builds and publishes the image.

> `.env.example`, `.gitignore`, `.dockerignore`, and the dev `docker-compose.yml` (`db` service) are already created in Milestone 1. This milestone adds the app image and production-ready compose.

### Tasks
- [ ] `Dockerfile` (multi-stage: `sdk` build → `aspnet` runtime image)
- [ ] Extend `docker-compose.yml` with the `beenthere` app service:
  - `depends_on: db`, env via `env_file: .env`
  - (optional) `adminer` service for DB admin
- [ ] `MigrateAsync()` on startup handles schema creation (ADR-0009)
- [ ] GitHub Actions workflow `.github/workflows/ci.yml`:
  - Trigger: push to `main`, pull requests, git tag push (`v*`)
  - Steps: restore → build → test → docker build → push to GHCR
  - Image tags: `latest` on main merge; `v{semver}` on tag push (ADR-0009)
- [ ] `README.md` quickstart: `docker compose up -d`

### Done when
`docker compose up -d` brings up PostGIS + app, migrations run automatically, and the app is reachable on port 5000.

---

## Key constraints (from ADRs)

| # | Rule |
|---|------|
| ADR-0003 | Google refresh token stored in `AspNetUserTokens`; never in a cookie or JS-readable store |
| ADR-0004 | `driveFileId` is **never** exposed in any public DTO, API response, or URL |
| ADR-0006 | `RouteSample` terminology is retired — always use `RoutePoint` |
| ADR-0008 | Hausdorff threshold is an app setting, not a magic number |
| ADR-0009 | One container instance only; `MigrateAsync()` is safe for this topology |

---

## Technology versions (target)

| Component | Version |
|-----------|---------|
| .NET / ASP.NET Core | **10.0** |
| C# | **14** |
| Blazor | Server-side (included in .NET 10) |
| EF Core | 10.x |
| Npgsql | 9.x |
| PostgreSQL | 15 |
| PostGIS | 3.3 |
| Leaflet | 1.9.x |
| leaflet.markercluster | 1.5.x |
| leaflet.heat | 0.2.x |
| SharpGpx | latest stable |

---

## Open questions / deferred

- Manual duplicate merge UI — **deferred post-MVP**
- TLS termination — handled by nginx reverse proxy or external proxy; out of scope for Milestone 7
- Garmin Connect / Komoot / Strava import instructions — can be added to ADR-0010 accordion without structural changes
