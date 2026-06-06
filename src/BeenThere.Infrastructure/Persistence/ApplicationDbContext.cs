using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BeenThere.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RoutePoint> RoutePoints => Set<RoutePoint>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global Query Filters — all user-owned queries are automatically scoped (ADR-0003)
        builder.Entity<Route>()
            .HasQueryFilter(r => r.UserId == currentUserService.UserId);

        builder.Entity<RoutePoint>()
            .HasQueryFilter(rp => rp.Route.UserId == currentUserService.UserId);

        builder.Entity<UserPreferences>()
            .HasQueryFilter(up => up.UserId == currentUserService.UserId);
    }
}
