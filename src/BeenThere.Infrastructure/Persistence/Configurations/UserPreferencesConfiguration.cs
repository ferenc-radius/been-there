using BeenThere.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeenThere.Infrastructure.Persistence.Configurations;

internal sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");

        builder.HasKey(up => up.UserId);

        builder.Property(up => up.TileProvider)
            .HasMaxLength(64)
            .HasDefaultValue("osm");
    }
}
