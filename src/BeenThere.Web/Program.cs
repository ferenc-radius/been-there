using BeenThere.Infrastructure;
using BeenThere.Infrastructure.Persistence;
using BeenThere.Web;
using BeenThere.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

EnvFile.Load(builder);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
app.MapGet("/signin", Handlers.SignIn).WithName("SignIn").AllowAnonymous();
app.MapGet("/signin-complete", Handlers.SignInComplete).WithName("SignInComplete").AllowAnonymous();
app.MapPost("/signout", Handlers.SignOut).WithName("SignOut");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/routes/{routeId}/download", Handlers.DownloadRoute)
    .WithName("DownloadRoute").RequireAuthorization();

app.Run();

