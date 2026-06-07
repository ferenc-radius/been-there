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

## Milestone 4 — Map UI ✅ COMPLETE

**Goal:** all user routes rendered on an interactive map with clustering and heatmap on the home page.

### Tasks
- [x] `wwwroot/js/map-interop.js` JS isolation module via `IJSObjectReference` (ADR-0007)
  - Exposed methods: `initMap`, `addRoutes`, `setTileProvider`, `zoomToRoute`, `toggleHeatmap`, `toggleClusters`
- [x] Blazor `MapComponent.razor` wrapping the interop module
- [x] Load all user route LineStrings as GeoJSON from a `/api/routes/geojson` endpoint
- [x] Leaflet polylines for each route
- [x] `leaflet.markercluster` for route centroid clustering
- [x] `leaflet.heat` heatmap layer toggle
- [x] OSM tile provider, preference saved to `user_preferences` (ADR-0007)
- [x] Home page: map + left sidebar with route list

### Done when
Home page shows all imported routes as polylines on an interactive Leaflet map, with route list in sidebar.

---

## Milestone 5 — Route detail, search, and analytics ✅ COMPLETE (partial)

**Goal:** users can drill into a route, filter the library, and see aggregate stats.

**Status:** Route detail accordion expansion, elevation profile, GPX download, and tag editing are complete. Raw lat/lng/radius filters removed in preparation for Milestone 5.5 place-name search. Analytics page deferred to post-MVP.

### Tasks
- [x] Route detail page: accordion inline expansion with name, date, distance, elevation gain, mode, tags, notes
- [x] Elevation profile chart: SVG with bucket-averaged smoothing, gridlines, and altitude labels (left axis in metres)
- [x] GPX download endpoint `/api/routes/{routeId}/download` with green button + download icon (streams from Drive; **no `driveFileId` in response**)
- [x] Tag editing on route detail: add/remove tags with real-time persistence; tag pills with delete button
- [x] Search / filter panel with Tag filter, Length range filters
- [ ] ~~Radius search (lat/lng/radius inputs)~~ — **removed; replaced by Milestone 5.5 place-name search**
- [ ] Analytics page: total km, km/year bar chart, most repeated routes (by start/end proximity) — **deferred post-MVP**
- [x] Stats pills per route: Distance, Elevation Gain, Max Elevation with icons

### Done when
User can filter the library by radius + length, open a route detail with elevation profile, and download the original GPX.

---

## Milestone 6 — Multi-user sharing, Ratings & Reviews, "Been-There Too"

**Goal:** support multi-user visibility (private-by-default sharing), allow users to rate and review routes (1–5 stars, signed-in users only), and provide a "been-there too" intersection feature showing other users who overlap a route (count + expandable detail).

### Tasks
- [ ] Data model: add `IsPublic` (bool, default false) to `Route`.
- [ ] Schema: new tables `RouteRating` and `RouteReview` with appropriate FKs, timestamps, and indexes; `RouteReview` has `IsFlagged` for moderation.
- [ ] Visibility: extend global query filter so listings return `r.IsPublic == true || r.UserId == currentUserId`.
- [ ] Services: add `IRouteSocialService` + `RouteSocialService` handling rating aggregation, review CRUD, and intersection analysis (use PostGIS `ST_Intersection` / `ST_Length` on geography for overlap metrics).
- [ ] API: add handlers for ratings/reviews/intersections (GET/POST endpoints, auth required for writes).
- [ ] UI: surface average rating and review count in `Routes.razor` and `MapComponent`; route detail modal shows reviews, rating form, and "been-there too" expandable list.
- [ ] Moderation: add review reporting endpoint that sets `IsFlagged = true` for admin triage.
- [ ] Performance: compute intersections or expensive metrics as background jobs and cache summaries; provide pagination for reviews.

### Done when
User can opt to share a route, other users can see shared routes, authenticated users can submit one rating (1–5) and textual reviews per route, and the "been-there too" panel shows how many and which users intersect that route with overlap metrics.

### Notes
- Routes remain private by default; sharing is opt-in via `IsPublic`.
- Reviews require login and display reviewer name; ratings are one-per-user per-route.

---

## Milestone 7 — Duplicate detection

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

## Milestone 5.5 — Search by place ✅ COMPLETE

**Goal:** users can type a place name (city, region, address) into the filter panel and find routes near that location, without having to know or paste raw coordinates.

**Licensing & Attribution:** Nominatim is AGPL-3.0; OpenStreetMap data is ODbL 1.0. Public Nominatim API use requires attribution. Add `© OpenStreetMap contributors` link in UI/footer and update `README.md`.

### Tasks
- [ ] Integrate a geocoding API (e.g. Nominatim/OpenStreetMap — free, no key required) to resolve a place name to `(lat, lng)` via a server-side HTTP call in `BeenThere.Infrastructure`
- [ ] `IGeocodingService` interface in `BeenThere.Core` with `Task<GeocodeResult?> GeocodeAsync(string query, CancellationToken ct)` — returns `Lat`, `Lng`, `DisplayName`
- [ ] `NominatimGeocodingService` implementation in `BeenThere.Infrastructure` — rate-limit aware, sets `User-Agent` header per Nominatim policy
- [ ] Add `PlaceName` + `RadiusKm` (default 10 km) fields to `RouteSearchFilter`; backend resolves place → coordinates before executing `IsWithinDistance`
- [ ] Replace lat/lng/radius raw inputs in the filter panel with a single **"Near place"** text input + radius selector (5 / 10 / 25 / 50 km)
- [ ] Show resolved place name as a dismissible badge below the input after geocoding
- [ ] Graceful error state when geocoding returns no results

### Done when
User types "Fontainebleau" + selects 25 km and the library shows only routes whose centroid is within 25 km of that location.

---

## Milestone 8 — Dockerfile, production Compose, and CI

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

---

## Milestone 2 — Decision Log

**Status:** Decisions locked via Planception interrogation (2026-06-06)

### Resolved Decisions

| ID | Decision | Answer | Evidence |
|---|---|---|---|
| Q1 | Token persistence | `AspNetUserTokens` via `UserManager` | ADR-0003 |
| Q2 | Token refresh | Auto-refresh + persist via `OnTokenReceived` | ADR-0003 (updated) |
| Q3 | Event hook | `OnTokenReceived` fires on sign-in + refresh | ADR-0003 (updated) |
| Q4 | DriveService ops | Three methods: CreateFolder, Upload, Download | ADR-0004 (expanded) |
| Q5 | Error handling | Domain exceptions (e.g. `DriveUploadException`) | ADR-0004 (expanded) |
| Q6 | File naming | `{routeId}_{sanitised-name}.{ext}` | ADR-0004 |
| Q7 | Download contract | Stream only; `driveFileId` never exposed | ADR-0004 |
| Q8 | SignIn/SignOut | Razor components wrapping `/signin`, `/signout` | ADR-0001 (implicit) |
| Q9 | Secrets injection | `.env` file (local) + env vars (prod) | ADR-0009 |
| Q10 | Testing | Unit (mocked) + integration (real PostGIS) | ADR-0011 (new) |

### Implementation Blocked By
None — all decisions are locked and non-blocking.

