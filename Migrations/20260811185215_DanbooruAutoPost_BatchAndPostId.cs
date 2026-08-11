using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.MigrationsDb
{
    /// <inheritdoc />
    public partial class DanbooruAutoPost_BatchAndPostId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DanbooruBatchImages");

            migrationBuilder.AddColumn<int>(
                name: "DanbooruPostId",
                table: "DanbooruAutoPostConfigs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DanbooruPostId",
                table: "DanbooruAutoPostConfigs");

            migrationBuilder.CreateTable(
                name: "DanbooruBatchImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DanbooruPostId = table.Column<int>(type: "integer", nullable: false),
                    DownloadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanbooruBatchImages", x => x.Id);
                });
        }
    }
}
