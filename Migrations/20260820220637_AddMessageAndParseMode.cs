using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.MigrationsDb
{
    /// <inheritdoc />
    public partial class AddMessageAndParseMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "NSFWBooruAutoPostConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "DanbooruAutoPostConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TelegramParseMode",
                table: "DanbooruAutoPostConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Message",
                table: "NSFWBooruAutoPostConfigs");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "DanbooruAutoPostConfigs");

            migrationBuilder.DropColumn(
                name: "TelegramParseMode",
                table: "DanbooruAutoPostConfigs");
        }
    }
}
