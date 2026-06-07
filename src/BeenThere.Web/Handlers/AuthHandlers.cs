using System.Security.Claims;
using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;

namespace BeenThere.Web.Handlers;

internal static class AuthHandlers
{
    internal static IResult SignIn(SignInManager<IdentityUser> signInManager)
    {
        var properties = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, "/signin-complete");

        properties.Parameters["access_type"] = "offline";
        properties.Parameters["prompt"] = "consent";
        properties.Parameters["include_granted_scopes"] = "true";

        return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }

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
            var existingUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingUser != null)
            {
                await PersistTokensAsync(userManager, existingUser, info, preserveExistingRefreshToken: true);
            }

            return Results.Redirect("/");
        }

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

        await signInManager.SignOutAsync();
        await context.SignOutAsync(IdentityConstants.ExternalScheme);

        return Results.Redirect("/welcome");
    }
}
