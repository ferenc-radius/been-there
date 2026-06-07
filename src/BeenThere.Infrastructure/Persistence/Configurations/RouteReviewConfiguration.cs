using BeenThere.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeenThere.Infrastructure.Persistence.Configurations;

internal sealed class RouteReviewConfiguration : IEntityTypeConfiguration<RouteReview>
{
    public void Configure(EntityTypeBuilder<RouteReview> builder)
    {
        builder.ToTable("route_reviews");

        builder.HasKey(rr => rr.Id);

        builder.Property(rr => rr.UserId).IsRequired();
        builder.Property(rr => rr.RouteId).IsRequired();
        builder.Property(rr => rr.ReviewText).HasMaxLength(500).IsRequired();
        builder.Property(rr => rr.IsFlagged).IsRequired().HasDefaultValue(false);

        // Unique constraint: one review per (UserId, RouteId)
        builder.HasIndex(rr => new { rr.UserId, rr.RouteId })
            .IsUnique()
            .HasDatabaseName("route_reviews_user_route_unique_idx");

        // FK to Route with cascade delete
        builder.HasOne(rr => rr.Route)
            .WithMany(r => r.Reviews)
            .HasForeignKey(rr => rr.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
