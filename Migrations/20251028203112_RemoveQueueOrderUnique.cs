using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RemoveQueueOrderUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems",
            column: "QueueOrder"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems",
            column: "QueueOrder",
            unique: true,
            descending: Array.Empty<bool>()
        );
    }
}
