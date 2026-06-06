using System.Security.Claims;
using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;

namespace BeenThere.Web;

internal static class Handlers
{
    // Initiates Google OAuth challenge (ADR-0003).
    // ConfigureExternalAuthenticationProperties stores LoginProvider in the auth
    // properties — required by SignInManager.GetExternalLoginInfoAsync() at the
    // /signin-complete step. Without it, GetExternalLoginInfoAsync returns null
    // and we bounce back to /signin in an infinite loop.
    internal static IResult SignIn(SignInManager<IdentityUser> signInManager)
    {
        var properties = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, "/signin-complete");
        return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }

    // Completes Identity sign-in after Google OAuth callback (ADR-0003).
    // Google middleware handles /signin-google automatically, then sets Identity.External
    // cookie and redirects here. Exchange it for an Identity.Application cookie,
    // creating the user on first sign-in and persisting OAuth tokens.
    internal static async Task<IResult> SignInComplete(
        HttpContext context,
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return Results.Redirect("/welcome");
        }

        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
            // Returning sign-in: update tokens so DriveService always has a fresh access_token.
            // Google only returns refresh_token on first-ever consent; preserve any existing one
            // rather than overwriting with null.
            var existingUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingUser != null)
            {
                await PersistTokensAsync(userManager, existingUser, info, preserveExistingRefreshToken: true);
            }

            return Results.Redirect("/");
        }

        // First sign-in: create user from Google claims
        var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            return Results.Redirect("/welcome");
        }

        await userManager.AddLoginAsync(user, info);
        await PersistTokensAsync(userManager, user, info, preserveExistingRefreshToken: false);
        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Redirect("/");
    }

    /// <summary>
    /// Stores OAuth tokens from <paramref name="info"/> into AspNetUserTokens.
    /// When <paramref name="preserveExistingRefreshToken"/> is true, an incoming
    /// null/missing refresh_token does not overwrite an already-stored one
    /// (Google omits refresh_token on re-auth after first consent).
    /// </summary>
    private static async Task PersistTokensAsync(
        UserManager<IdentityUser> userManager,
        IdentityUser user,
        ExternalLoginInfo info,
        bool preserveExistingRefreshToken)
    {
        if (info.AuthenticationTokens == null)
        {
            return;
        }

        foreach (var token in info.AuthenticationTokens)
        {
            if (preserveExistingRefreshToken && token.Name == "refresh_token")
            {
                // Don't overwrite an existing refresh_token with a missing/empty one
                var existing = await userManager.GetAuthenticationTokenAsync(user, info.LoginProvider, "refresh_token");
                if (!string.IsNullOrEmpty(existing) && string.IsNullOrEmpty(token.Value))
                {
                    continue;
                }
            }
            await userManager.SetAuthenticationTokenAsync(user, info.LoginProvider, token.Name, token.Value);
        }
    }

    internal static async Task<IResult> SignOut(
        HttpContext context,
        SignInManager<IdentityUser> signInManager,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("SignOut");
#pragma warning disable CA1848
        logger.LogWarning("SignOut invoked. IsAuthenticated={IsAuth} User={User}",
            context.User.Identity?.IsAuthenticated, context.User.Identity?.Name);
#pragma warning restore CA1848

        // Sign out all Identity-related cookies (Application + External).
        // SignInManager.SignOutAsync clears these plus any two-factor cookies.
        await signInManager.SignOutAsync();

        // Belt-and-braces: also sign out the external scheme explicitly, in case
        // an Identity.External cookie persisted (e.g., from an interrupted flow).
        await context.SignOutAsync(IdentityConstants.ExternalScheme);

        // Redirect to the public landing page. Avoid "/" because it's [Authorize],
        // which would 302 → LoginPath. LoginPath used to be /signin, which auto-
        // challenges Google; with Google's session still alive, prompt=none would
        // silently re-sign the user back in — defeating sign-out.
        return Results.Redirect("/welcome");
    }

    // Returns the route file from Drive (ADR-0004).
    // The internal driveFileId is never exposed to the client; only routeId is.
    internal static async Task<IResult> DownloadRoute(
        Guid routeId,
        HttpContext context,
        SignInManager<IdentityUser> signInManager,
        IDriveService driveService)
    {
        var user = await signInManager.UserManager.GetUserAsync(context.User);
        if (user == null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var fileStream = await driveService.DownloadFileAsync(user.Id, routeId, context.RequestAborted);
            return Results.File(fileStream, "application/octet-stream", $"route-{routeId}.gpx");
        }
        catch (DriveDownloadException)
        {
            return Results.NotFound();
        }
    }
}
