# ADR-0003: Authentication and token storage

**Status:** Accepted  
**Decisions:** B, B2, B3

## Context

Users authenticate via Google OAuth. The app needs to make Drive API calls on behalf of the user, which requires persisting a refresh token across sessions. All route data must be strictly isolated per user.

## Decision

### Identity provider
Use **ASP.NET Core Identity** with the Google OAuth handler chained via `AddAuthentication()` (called after `AddIdentity()` so Identity owns the default scheme):
```csharp
// AddIdentity() first — owns DefaultScheme (Identity.Application cookie)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Then chain Google without overriding Identity's defaults
builder.Services.AddAuthentication()
    .AddGoogle(o => {
        o.SaveTokens = true;
        o.Scope.Add("https://www.googleapis.com/auth/drive.appdata");
    });

// Override Identity's LoginPath (it defaults to /Account/Login)
builder.Services.ConfigureApplicationCookie(o => {
    o.LoginPath = "/signin";
});
```

### Token storage
Google refresh tokens are stored in the standard `AspNetUserTokens` table via `UserManager.GetAuthenticationTokenAsync` / `SetAuthenticationTokenAsync`. No custom `user_tokens` table is needed.

### Token refresh
Rely on the **Google client library's built-in auto-refresh** (`UserCredential`). After a library-triggered refresh, persist the new access token back to `AspNetUserTokens` so it survives server restarts.

### Token persistence event hook
Tokens are persisted in the `/signin-complete` endpoint (the OAuth callback completion handler) via `SignInManager.GetExternalLoginInfoAsync()`, which exposes all tokens returned by the Google callback as `ExternalLoginInfo.AuthenticationTokens`:
```csharp
// In /signin-complete endpoint (Program.cs)
var info = await signInManager.GetExternalLoginInfoAsync();
if (info?.AuthenticationTokens != null)
{
    foreach (var token in info.AuthenticationTokens)
        await userManager.SetAuthenticationTokenAsync(
            user, info.LoginProvider, token.Name, token.Value);
}
```
This fires once per sign-in (not on every request). Token names are `"access_token"`, `"refresh_token"`, and `"token_type"` as returned by Google's token endpoint.

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
