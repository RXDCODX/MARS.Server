using MARS.Server.Services.Twitch;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class TwitchToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var token = new TokenInfo();

        migrationBuilder.CreateTable(
            name: "TwitchToken",
            columns: builder => new
            {
                Id = builder.Column<Guid>(type: "uuid", nullable: false),
                AccessToken = builder.Column<string>(
                    type: "character varying",
                    nullable: true,
                    maxLength: 30
                ),
                RefreshToken = builder.Column<string>(type: "character varying", nullable: false),
                ExpiresIn = builder.Column<TimeSpan>(
                    type: "time without time zone",
                    nullable: false
                ),
                WhenCreated = builder.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                WhenExpires = builder.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
            },
            constraints: builder => builder.PrimaryKey("PK_Id", a => a.Id)
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TwitchToken");
    }
}
