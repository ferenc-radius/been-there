using BeenThere.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace BeenThere.Web;

internal static class AuthSetup
{
    /// <summary>
    /// Registers Identity, Google OAuth, Data Protection, and cookie configuration.
    ///
    /// Auth flow: unauthenticated → Identity redirects to LoginPath (/welcome) →
    ///   user clicks Sign In → /signin issues Google challenge →
    ///   /signin-google (Google callback, auto-handled) →
    ///   /signin-complete → SignInManager.ExternalLoginSignInAsync → Identity.Application cookie.
    /// </summary>
    internal static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Persist Data Protection keys so OAuth correlation state survives app restarts
        // (e.g. dotnet watch). Without this, any restart between challenge and callback
        // invalidates the encrypted state and produces "Correlation failed".
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(environment.ContentRootPath, ".dataprotection-keys")))
            .SetApplicationName("BeenThere");

        // Identity manages user persistence and the Identity.Application cookie.
        // AddAuthentication() chained after Identity avoids overriding its
        // DefaultScheme / DefaultChallengeScheme.
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthentication()
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = configuration["GOOGLE_CLIENT_ID"]
                    ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not configured");
                options.ClientSecret = configuration["GOOGLE_CLIENT_SECRET"]
                    ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not configured");
                // Request all necessary scopes for profile and Drive access
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("https://www.googleapis.com/auth/drive.appdata");
                options.SaveTokens = true;
                // Lax + SameAsRequest required so the correlation cookie survives the
                // cross-site redirect from Google back to the callback URL.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

        // Override Identity's default /Account/Login redirect path.
        // No global UseCookiePolicy — each cookie is configured here explicitly to
        // prevent the OAuth handler's Set-Cookie headers from being mutated in ways
        // that cause "Correlation failed".
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/welcome";
            options.AccessDeniedPath = "/welcome";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // Identity.External holds Google claims between /signin-google and /signin-complete.
        services.ConfigureExternalCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        return services;
    }
}
