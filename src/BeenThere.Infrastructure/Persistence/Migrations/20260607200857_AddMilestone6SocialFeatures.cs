using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeenThere.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMilestone6SocialFeatures : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_public",
            table: "routes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "route_ratings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "text", nullable: false),
                route_id = table.Column<Guid>(type: "uuid", nullable: false),
                rating = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_route_ratings", x => x.id);
                table.ForeignKey(
                    name: "fk_route_ratings_routes_route_id",
                    column: x => x.route_id,
                    principalTable: "routes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "route_reviews",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "text", nullable: false),
                route_id = table.Column<Guid>(type: "uuid", nullable: false),
                review_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                is_flagged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_route_reviews", x => x.id);
                table.ForeignKey(
                    name: "fk_route_reviews_routes_route_id",
                    column: x => x.route_id,
                    principalTable: "routes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_route_ratings_route_id",
            table: "route_ratings",
            column: "route_id");

        migrationBuilder.CreateIndex(
            name: "route_ratings_user_route_unique_idx",
            table: "route_ratings",
            columns: new[] { "user_id", "route_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_route_reviews_route_id",
            table: "route_reviews",
            column: "route_id");

        migrationBuilder.CreateIndex(
            name: "route_reviews_user_route_unique_idx",
            table: "route_reviews",
            columns: new[] { "user_id", "route_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "route_ratings");

        migrationBuilder.DropTable(
            name: "route_reviews");

        migrationBuilder.DropColumn(
            name: "is_public",
            table: "routes");
    }
}
