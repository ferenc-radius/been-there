# ADR-0009: Docker Compose deployment and CI

**Status:** Accepted  
**Decisions:** H, H2, H3

## Context

BeenThere must run on a local machine or any VM without provider lock-in. CI should build and publish a Docker image automatically.

## Decision

### Database migrations
Run `context.Database.MigrateAsync()` in `Program.cs` on startup. This is safe because the deployment is always single-instance — the concurrent-migration race condition that warrants a separate migration step does not apply.

### Docker Compose services
| Service | Image |
|---------|-------|
| `db` | `postgis/postgis:15-3.3` |
| `beenthere` | Built from repo `Dockerfile` (multi-stage: `sdk` build → `aspnet` runtime) |

Optional: `nginx` (TLS termination), `adminer` (DB admin UI).

### Secret injection
Secrets are injected via a `.env` file on the host:
- `.env` is in **both `.gitignore` and `.dockerignore`** — never committed, never in the image.
- `.env.example` is committed with placeholder values as the canonical reference.
- `docker-compose.yml` references it via `env_file: .env`.

Required variables (see `.env.example`):
```
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
ConnectionStrings__Default=
ASPNETCORE_ENVIRONMENT=Production
```

### CI image tagging (GitHub Actions → GHCR)
| Trigger | Tags pushed |
|---------|-------------|
| Push to `main` | `ghcr.io/<owner>/beenthere:latest` |
| Push of git tag `v*.*.*` | `ghcr.io/<owner>/beenthere:v1.2.3` + `latest` |

`latest` makes `docker compose pull` trivial for day-to-day updates; semver tags provide pinned rollback points.

## Consequences

- `.env.example` must be kept in sync when new required environment variables are added.
- The `Dockerfile` must not `COPY .env` — `.dockerignore` enforces this.
- For production TLS, add an `nginx` service or terminate TLS at the host/load-balancer layer outside Compose.
