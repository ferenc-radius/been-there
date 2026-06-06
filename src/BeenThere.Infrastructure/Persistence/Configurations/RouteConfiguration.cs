using BeenThere.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeenThere.Infrastructure.Persistence.Configurations;

internal sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Mode).HasMaxLength(64);
        builder.Property(r => r.OriginalFilename).HasMaxLength(512);
        builder.Property(r => r.Notes).HasMaxLength(4000);

        // Tags stored as jsonb with value comparer for change-tracking
        builder.Property(r => r.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        // DriveFileId is internal — convention auto-maps to drive_file_id
        builder.Property(r => r.DriveFileId).HasMaxLength(256);

        builder.Property(r => r.Geom).HasColumnType("geometry(LineString,4326)");
        builder.Property(r => r.Centroid).HasColumnType("geometry(Point,4326)");

        // Spatial indexes (ADR-0006)
        builder.HasIndex(r => r.Centroid)
            .HasMethod("GIST")
            .HasDatabaseName("routes_centroid_idx");

        builder.HasIndex(r => r.DistanceM)
            .HasDatabaseName("routes_distance_idx");

        builder.HasIndex(r => r.Date)
            .HasDatabaseName("routes_date_idx");

        builder.HasMany(r => r.Points)
            .WithOne(rp => rp.Route)
            .HasForeignKey(rp => rp.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
