using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeenThere.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsPublicFromRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_public",
                table: "routes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_public",
                table: "routes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
