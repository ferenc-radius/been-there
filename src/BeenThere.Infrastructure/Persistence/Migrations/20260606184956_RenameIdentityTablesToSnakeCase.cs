using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeenThere.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RenameIdentityTablesToSnakeCase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "AspNetUserTokens",
            newName: "asp_net_user_tokens");

        migrationBuilder.RenameTable(
            name: "AspNetUsers",
            newName: "asp_net_users");

        migrationBuilder.RenameTable(
            name: "AspNetUserRoles",
            newName: "asp_net_user_roles");

        migrationBuilder.RenameTable(
            name: "AspNetUserLogins",
            newName: "asp_net_user_logins");

        migrationBuilder.RenameTable(
            name: "AspNetUserClaims",
            newName: "asp_net_user_claims");

        migrationBuilder.RenameTable(
            name: "AspNetRoles",
            newName: "asp_net_roles");

        migrationBuilder.RenameTable(
            name: "AspNetRoleClaims",
            newName: "asp_net_role_claims");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "asp_net_users",
            newName: "AspNetUsers");

        migrationBuilder.RenameTable(
            name: "asp_net_user_tokens",
            newName: "AspNetUserTokens");

        migrationBuilder.RenameTable(
            name: "asp_net_user_roles",
            newName: "AspNetUserRoles");

        migrationBuilder.RenameTable(
            name: "asp_net_user_logins",
            newName: "AspNetUserLogins");

        migrationBuilder.RenameTable(
            name: "asp_net_user_claims",
            newName: "AspNetUserClaims");

        migrationBuilder.RenameTable(
            name: "asp_net_roles",
            newName: "AspNetRoles");

        migrationBuilder.RenameTable(
            name: "asp_net_role_claims",
            newName: "AspNetRoleClaims");
    }
}
