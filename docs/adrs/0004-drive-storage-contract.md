# ADR-0004: Google Drive storage contract

**Status:** Accepted  
**Decisions:** C, C2, C3

## Context

GPX/KML originals must be stored durably so they can be re-downloaded or re-parsed. Google Drive `appDataFolder` is the designated store, invisible to the user's Drive UI.

## Decision

### Upload timing
**Drive-first, then Postgres commit.** The import flow is:
1. Upload file to Drive → receive `fileId`.
2. Commit `Route` row (with `drive_file_id`) in a single `SaveChangesAsync()`.

A failed upload aborts the import with no orphan DB row. A failed DB commit after a successful upload leaves an orphaned Drive file, which is recoverable by re-importing.

### Folder and file naming
- Create **one Drive folder per user** inside `appDataFolder`, named by `userId`. Cache the folder's Drive `fileId` in the user record after first creation.
- Name each file: `{routeId}_{sanitised-route-name}.{ext}` (e.g. `3fa85f64_mont-blanc-tour.gpx`).
- Store the user's original filename separately in `Route.original_filename` for display and `Content-Disposition` headers.

> ⚠️ **Contract safety:** `drive_file_id` is an infrastructure detail. Downloads must be served via `/api/routes/{routeId}/download` — the app resolves the Drive file server-side. Never expose `drive_file_id` in a public DTO.

### Caching
**No local file cache.** All derived data (geometry, stats, elevation) is in Postgres. Raw files are streamed directly from Drive on download. Revisit if download latency becomes noticeable in practice.

## Consequences

- Drive folder creation must be idempotent (search before create).
- The `Drive API` wrapper lives in `BeenThere.Infrastructure`.
- Re-import is the supported recovery path for orphaned Drive files.
