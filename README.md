# BeenThere

A multi-user route tracking and storage application built with ASP.NET Core 10 Blazor Server and PostGIS. Routes are stored in Google Drive for per-user privacy and scalability.

## Features

- **Multi-user route management**: Each user has their own route collection
- **Google Drive integration**: Routes stored in user's Drive with per-folder OAuth isolation
- **PostGIS geospatial queries**: Efficient geometry storage and duplicate detection
- **Route import**: Support for GPX and KML formats
- **Map visualization**: Interactive map rendering with route details

## Technology Stack

- **Backend**: ASP.NET Core 10, Entity Framework Core, PostGIS
- **Frontend**: Blazor Server
- **Storage**: Google Drive (appDataFolder), PostgreSQL with PostGIS
- **Authentication**: Google OAuth 2.0
- **Testing**: xUnit, Moq

## Prerequisites

- .NET 10 SDK
- PostgreSQL with PostGIS extension
- Google Cloud Project (for OAuth credentials)
- Docker (optional, for containerized PostgreSQL)

## Setup

### 1. Environment Configuration

Copy `.env.example` to `.env` and fill in your values:

```bash
cp .env.example .env
```

### 2. Google OAuth Setup

#### Create Google Cloud Project & Get Credentials

1. **Create a Google Cloud Project**
   - Go to [Google Cloud Console](https://console.cloud.google.com/)
   - Click **Select a Project** → **New Project**
   - Enter project name (e.g., `BeenThere`) and click **Create**
   - Wait for the project to be created and select it

2. **Enable Google Drive API**
   - Go to **APIs & Services** → **Library**
   - Search for **Google Drive API**
   - Click on it and press **Enable**

3. **Create OAuth 2.0 Credentials**
   - Go to **APIs & Services** → **Credentials**
   - Click **+ Create Credentials** → **OAuth 2.0 Client ID**
   - Select application type: **Web application**
   - Under "Authorized redirect URIs", add:
     ```
     http://localhost:5155/signin-google
     https://localhost:7180/signin-google
     https://yourdomain.com/signin-google
     ```
     (Include both localhost ports for development + your production domain)
   - Click **Create**

4. **Copy Credentials to `.env`**
   - View the created credential
   - Copy **Client ID** and **Client Secret**
   - Add to `.env`:
     ```
     GOOGLE_CLIENT_ID=your-client-id-here
     GOOGLE_CLIENT_SECRET=your-client-secret-here
     ```

⚠️ **Security**: Never commit `.env` with real secrets to version control. Use environment variables in production.

### 3. Database Setup

#### Option A: Docker (Recommended for Development)

```bash
docker-compose up -d
# Applies migrations automatically on first run
```

#### Option B: Local PostgreSQL

```bash
# Create database and enable PostGIS
createdb beenthere
psql -d beenthere -c "CREATE EXTENSION postgis;"

# Run migrations
cd src/BeenThere.Web
dotnet ef database update
```

### 4. Run the Application

```bash
cd src/BeenThere.Web
dotnet run
```

Access at `https://localhost:7180` (HTTPS) or `http://localhost:5155` (HTTP).

## Testing

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Project

```bash
# Infrastructure unit tests
dotnet test tests/BeenThere.Infrastructure.Tests/

# Integration tests
dotnet test tests/BeenThere.Integration.Tests/
```

## Architecture

### Project Structure

```
src/
├── BeenThere.Core/              # Domain models, interfaces, exceptions
├── BeenThere.Infrastructure/    # EF Core, Drive service, parsers
└── BeenThere.Web/               # Blazor Server, API endpoints

tests/
├── BeenThere.Infrastructure.Tests/
└── BeenThere.Integration.Tests/

docs/
└── adrs/                        # Architecture Decision Records
```

### Key Design Decisions

- **ADR-0003: Auth & Token Storage** - OAuth tokens stored in AspNetUserTokens table, Drive service retrieves refresh tokens on demand
- **ADR-0004: Drive Storage Contract** - Internal driveFileId never exposed to client; user sees only routeId
- **ADR-0011: Testing Strategy** - Unit tests mock Drive API; integration tests use real PostGIS; E2E tests deferred

See `docs/adrs/` for full decision records.

## Deployment

### Production Checklist

- [ ] Set `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` via environment variables or secrets manager
- [ ] Configure `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Use HTTPS with valid certificate
- [ ] Update Google Cloud redirect URIs to production domain
- [ ] Set up database backups
- [ ] Enable structured logging (ELK or Application Insights)
- [ ] Run `dotnet ef database update` before deploying new migrations

### Docker Build

```bash
docker build -f src/BeenThere.Web/Dockerfile -t beenthere:latest .
docker run -e GOOGLE_CLIENT_ID=<id> -e GOOGLE_CLIENT_SECRET=<secret> -p 443:8080 beenthere:latest
```

## Troubleshooting

### OAuth Sign-In Fails
- Verify Client ID/Secret in `.env`
- Check redirect URI matches Google Cloud Console configuration
- Ensure Google Drive API is enabled in Cloud Project

### Database Connection Error
- Verify PostgreSQL is running: `psql -U postgres -d beenthere -c "\dt"`
- Check connection string in `appsettings.json` matches database name and port
- Run migrations: `dotnet ef database update`

### PostGIS Extension Not Found
- Install PostGIS: `CREATE EXTENSION postgis;`
- Verify with: `SELECT postgis_version();`

## Contributing

1. Create a feature branch: `git checkout -b feature/my-feature`
2. Make changes and add tests
3. Run full test suite: `dotnet test`
4. Commit with descriptive message
5. Push and open a pull request

## License

See [LICENSE](LICENSE) file for details.

## Support

For issues, questions, or feature requests, please open an issue on GitHub.
