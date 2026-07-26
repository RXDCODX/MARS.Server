using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddDiscordTtsRelayConfigToRootState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "RootState",
            columns: new[] { "Name", "Value", "Description", "TypeDescription" },
            values: new object[,]
            {
                {
                    "DiscordTtsRelayTargetUserId",
                    "260383142903414785",
                    "ID Discord пользователя для TTS voice relay",
                    "ulong"
                },
                {
                    "DiscordTtsRelayTargetVoiceChannelId",
                    "1406679380369080481",
                    "ID Discord голосового канала для TTS voice relay",
                    "ulong"
                },
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "RootState",
            keyColumn: "Name",
            keyValues: new object[]
            {
                "DiscordTtsRelayTargetUserId",
                "DiscordTtsRelayTargetVoiceChannelId",
            }
        );
    }
}
