using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingAndTelegramUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultProductImageStoragePath",
                table: "Tenants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoStoragePath",
                table: "Tenants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "Customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultProductImageStoragePath",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoStoragePath",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "Customers");
        }
    }
}
