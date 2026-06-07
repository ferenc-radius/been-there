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
    public DbSet<RouteRating> RouteRatings => Set<RouteRating>();
    public DbSet<RouteReview> RouteReviews => Set<RouteReview>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Rename Identity tables to snake_case (UseSnakeCaseNamingConvention only handles columns;
        // Identity sets table names explicitly via ToTable() which takes precedence)
        builder.Entity<IdentityUser>().ToTable("asp_net_users");
        builder.Entity<IdentityRole>().ToTable("asp_net_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("asp_net_user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("asp_net_user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("asp_net_user_tokens");
        builder.Entity<IdentityUserRole<string>>().ToTable("asp_net_user_roles");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("asp_net_role_claims");

        // Global Query Filters — (ADR-0003)
        // UserPreferences: scoped to current user
        builder.Entity<UserPreferences>()
            .HasQueryFilter(up => up.UserId == currentUserService.UserId);
        
        // Routes & RoutePoints: visible to all users (no privacy distinction — "I have been there" is about sharing)
        // Ownership checks happen at the handler level for edit/delete operations
    }
}
