using BeenThere.Infrastructure;
using BeenThere.Infrastructure.Persistence;
using BeenThere.Web;
using BeenThere.Web.Components;
using NetTopologySuite.Geometries;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

EnvFile.Load(builder);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient with BaseAddress for relative URLs in Blazor components
builder.Services.AddScoped<HttpClient>(sp =>
{
    // For Blazor Server, relative URLs need an absolute BaseAddress
    // Use localhost:7180 for development; adjust for production as needed
    var baseAddress = new Uri("https://localhost:7180/");
    var client = new HttpClient { BaseAddress = baseAddress };
    return client;
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuth(builder.Configuration, builder.Environment);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

// Run migrations on startup (ADR-0009)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// StatusCodePages re-executes the pipeline for error status codes; must run early
// so subsequent middleware (auth, authorization, routing) re-executes for /not-found.
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// NOTE: no global UseCookiePolicy — each cookie (application, external, correlation)
// is configured explicitly in AuthSetup. A global policy here can mutate Set-Cookie
// headers from the OAuth handler in ways that cause "Correlation failed".
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapStaticAssets();

// OAuth and API endpoints registered before MapRazorComponents (Blazor fallback)
app.MapGet("/signin", BeenThere.Web.Handlers.AuthHandlers.SignIn).WithName("SignIn").AllowAnonymous();
app.MapGet("/signin-complete", BeenThere.Web.Handlers.AuthHandlers.SignInComplete).WithName("SignInComplete").AllowAnonymous();
app.MapPost("/signout", BeenThere.Web.Handlers.AuthHandlers.SignOut).WithName("SignOut");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/routes/{routeId}/download", BeenThere.Web.Handlers.RouteHandlers.DownloadRoute)
    .WithName("DownloadRoute")
    .RequireAuthorization();

app.MapGet("/api/routes/{routeId}", BeenThere.Web.Handlers.RouteHandlers.GetRouteDetail)
    .WithName("GetRoute")
    .RequireAuthorization();

app.MapGet("/api/routes/search", BeenThere.Web.Handlers.RouteHandlers.SearchRoutes)
    .WithName("SearchRoutes")
    .RequireAuthorization();

app.MapPost("/api/routes/{routeId}/tags", BeenThere.Web.Handlers.RouteHandlers.UpdateRouteTags)
    .WithName("UpdateRouteTags")
    .RequireAuthorization()
    .DisableAntiforgery();

app.MapGet("/api/routes/geojson", BeenThere.Web.Handlers.RouteHandlers.GetRoutesGeoJson)
    .WithName("RoutesGeoJson")
    .RequireAuthorization();

app.MapDelete("/api/routes/{routeId}", BeenThere.Web.Handlers.RouteHandlers.DeleteRoute)
    .WithName("DeleteRoute")
    .RequireAuthorization()
    .DisableAntiforgery();

app.MapGet("/api/preferences", BeenThere.Web.Handlers.PreferencesHandlers.GetPreferences)
    .WithName("GetPreferences")
    .RequireAuthorization();

app.MapPost("/api/preferences/stick-figure", BeenThere.Web.Handlers.PreferencesHandlers.UpdateStickFigure)
    .WithName("UpdateStickFigure")
    .RequireAuthorization()
    .DisableAntiforgery();

app.Run();

