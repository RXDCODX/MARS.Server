using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.MigrationsDb
{
    /// <inheritdoc />
    public partial class DanbooruAutoPost_RemoveBatch_AddTargetCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelKey",
                table: "PostedImageRecords");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "DanbooruAutoPostConfigs");

            migrationBuilder.AddColumn<int>(
                name: "TargetPostCount",
                table: "DanbooruAutoPostConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetPostCount",
                table: "DanbooruAutoPostConfigs");

            migrationBuilder.AddColumn<string>(
                name: "ChannelKey",
                table: "PostedImageRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "DanbooruAutoPostConfigs",
                type: "uuid",
                nullable: true);
        }
    }
}
