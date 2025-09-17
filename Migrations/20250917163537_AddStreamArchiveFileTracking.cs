using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddStreamArchiveFileTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StreamArchiveFiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                OriginalFileName = table.Column<string>(type: "text", nullable: false),
                ProcessedFileName = table.Column<string>(type: "text", nullable: false),
                OriginalFilePath = table.Column<string>(type: "text", nullable: false),
                OriginalFileSize = table.Column<long>(type: "bigint", nullable: false),
                DiscoveredAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                ProcessingStartedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                ProcessingCompletedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                Status = table.Column<int>(type: "integer", nullable: false),
                ChunksCount = table.Column<int>(type: "integer", nullable: false),
                ErrorMessage = table.Column<string>(type: "text", nullable: true),
                TelegramMessageId = table.Column<long>(type: "bigint", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamArchiveFiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_StreamArchiveFiles_StreamArchiveConfigs_ConfigId",
                    column: x => x.ConfigId,
                    principalTable: "StreamArchiveConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "StreamArchiveFileChunks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FileId = table.Column<Guid>(type: "uuid", nullable: false),
                ChunkNumber = table.Column<int>(type: "integer", nullable: false),
                TotalChunks = table.Column<int>(type: "integer", nullable: false),
                ChunkFileName = table.Column<string>(type: "text", nullable: false),
                ChunkSize = table.Column<long>(type: "bigint", nullable: false),
                OffsetInOriginalFile = table.Column<long>(type: "bigint", nullable: false),
                UploadedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                TelegramMessageId = table.Column<long>(type: "bigint", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                ErrorMessage = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamArchiveFileChunks", x => x.Id);
                table.ForeignKey(
                    name: "FK_StreamArchiveFileChunks_StreamArchiveFiles_FileId",
                    column: x => x.FileId,
                    principalTable: "StreamArchiveFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_StreamArchiveFileChunks_FileId",
            table: "StreamArchiveFileChunks",
            column: "FileId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_StreamArchiveFiles_ConfigId_OriginalFilePath",
            table: "StreamArchiveFiles",
            columns: ["ConfigId", "OriginalFilePath"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StreamArchiveFileChunks");

        migrationBuilder.DropTable(name: "StreamArchiveFiles");
    }
}
