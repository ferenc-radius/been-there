using BeenThere.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeenThere.Infrastructure.Persistence.Configurations;

internal sealed class RouteRatingConfiguration : IEntityTypeConfiguration<RouteRating>
{
    public void Configure(EntityTypeBuilder<RouteRating> builder)
    {
        builder.ToTable("route_ratings");

        builder.HasKey(rr => rr.Id);

        builder.Property(rr => rr.UserId).IsRequired();
        builder.Property(rr => rr.RouteId).IsRequired();
        builder.Property(rr => rr.Rating).IsRequired();

        // Unique constraint: one rating per (UserId, RouteId)
        builder.HasIndex(rr => new { rr.UserId, rr.RouteId })
            .IsUnique()
            .HasDatabaseName("route_ratings_user_route_unique_idx");

        // FK to Route with cascade delete
        builder.HasOne(rr => rr.Route)
            .WithMany(r => r.Ratings)
            .HasForeignKey(rr => rr.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
