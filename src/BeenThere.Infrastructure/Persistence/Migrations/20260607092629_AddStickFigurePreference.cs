using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeenThere.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddStickFigurePreference : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "stick_figure",
            table: "user_preferences",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "stick_figure",
            table: "user_preferences");
    }
}
