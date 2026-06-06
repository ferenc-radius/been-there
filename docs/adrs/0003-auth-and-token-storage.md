# ADR-0003: Authentication and token storage

**Status:** Accepted  
**Decisions:** B, B2, B3

## Context

Users authenticate via Google OAuth. The app needs to make Drive API calls on behalf of the user, which requires persisting a refresh token across sessions. All route data must be strictly isolated per user.

## Decision

### Identity provider
Use **ASP.NET Core Identity** with the Google OAuth handler configured as:
```csharp
.AddGoogle(o => {
    o.SaveTokens = true;
    o.Scope.Add("https://www.googleapis.com/auth/drive.appdata");
})
```

### Token storage
Google refresh tokens are stored in the standard `AspNetUserTokens` table via `UserManager.GetAuthenticationTokenAsync` / `SetAuthenticationTokenAsync`. No custom `user_tokens` table is needed.

### Token refresh
Rely on the **Google client library's built-in auto-refresh** (`UserCredential`). After a library-triggered refresh, persist the new access token back to `AspNetUserTokens` so it survives server restarts.

### Token persistence event hook
Hook the `OnTokenReceived` event in the Google OAuth handler to persist the refresh token to `AspNetUserTokens`:
```csharp
.AddGoogle(o => {
    o.SaveTokens = true;
    o.Scope.Add("https://www.googleapis.com/auth/drive.appdata");
    o.Events = new OpenIdConnectEvents {
        OnTokenReceived = async context => {
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
            var user = await userManager.GetUserAsync(context.Principal);
            if (user != null && context.TokenEndpointResponse?.AccessToken != null) {
                await userManager.SetAuthenticationTokenAsync(
                    user, "Google", "access_token", context.TokenEndpointResponse.AccessToken);
                await userManager.SetAuthenticationTokenAsync(
                    user, "Google", "refresh_token", context.TokenEndpointResponse.RefreshToken);
            }
        }
    };
})
```
`OnTokenReceived` fires only on sign-in and token refresh (not on every request), minimizing database writes.

### User data isolation
Apply **EF Core Global Query Filters** on all user-owned entities (e.g. `Route`) keyed to the current user ID resolved from `IHttpContextAccessor`:
```csharp
modelBuilder.Entity<Route>().HasQueryFilter(r => r.UserId == _currentUserId);
```
Bypassing the filter requires an explicit `.IgnoreQueryFilters()` call.

## Consequences

- Full ASP.NET Core Identity schema (`AspNetUsers`, `AspNetUserTokens`, etc.) is added to the migration.
- `IHttpContextAccessor` must be registered and injected into `DbContext`.
- Horizontal scaling is safe because tokens are in the DB, not in-memory.
- `driveFileId` stored on `Route` is an internal field and **must not** be surfaced in any public-facing DTO or Blazor binding (see ADR-0004).
