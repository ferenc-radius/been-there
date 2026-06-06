using BeenThere.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeenThere.Infrastructure.Persistence.Configurations;

internal sealed class RoutePointConfiguration : IEntityTypeConfiguration<RoutePoint>
{
    public void Configure(EntityTypeBuilder<RoutePoint> builder)
    {
        builder.ToTable("route_points");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.Id).UseIdentityByDefaultColumn();
        builder.Property(rp => rp.Geom)
            .HasColumnType("geometry(Point,4326)")
            .IsRequired();

        // Ordered point retrieval for elevation profile (ADR-0006)
        builder.HasIndex(rp => new { rp.RouteId, rp.Idx })
            .HasDatabaseName("route_points_route_idx");

        // Spatial index for ST_DWithin proximity queries (ADR-0006)
        builder.HasIndex(rp => rp.Geom)
            .HasMethod("GIST")
            .HasDatabaseName("route_points_geom_idx");
    }
}
