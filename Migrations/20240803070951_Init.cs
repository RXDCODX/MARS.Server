using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Alerts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchPointsCost = table.Column<int>(type: "integer", nullable: false),
                KeyWord = table.Column<string>(type: "text", nullable: true),
                FilePath = table.Column<string>(type: "text", nullable: true),
                Duration = table.Column<int>(type: "integer", nullable: false),
                RandomCoordinates = table.Column<bool>(type: "boolean", nullable: false),
                XCoordinate = table.Column<int>(type: "integer", nullable: false),
                YCoordinate = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                Text = table.Column<string>(type: "text", nullable: true),
                TextColor = table.Column<string>(type: "text", nullable: true),
                VIP = table.Column<bool>(type: "boolean", nullable: false),
                IsBorder = table.Column<bool>(type: "boolean", nullable: false),
                IsProportion = table.Column<bool>(type: "boolean", nullable: false),
                IsResizeRequires = table.Column<bool>(type: "boolean", nullable: false),
                Height = table.Column<int>(type: "integer", nullable: false),
                Width = table.Column<int>(type: "integer", nullable: false),
                IsRotated = table.Column<bool>(type: "boolean", nullable: false),
                Rotation = table.Column<int>(type: "integer", nullable: false),
                IsLooped = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Alerts", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "AutoMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Message = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoMessages", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Clips",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                WhenUploaded = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                Title = table.Column<string>(type: "text", nullable: true),
                YouTubeUrl = table.Column<string>(type: "text", nullable: true),
                PathToLocalFile = table.Column<string>(type: "text", nullable: true),
                Url = table.Column<string>(type: "text", nullable: true),
                EmbedUrl = table.Column<string>(type: "text", nullable: true),
                BroadcasterId = table.Column<string>(type: "text", nullable: true),
                BroadcasterName = table.Column<string>(type: "text", nullable: true),
                CreatorId = table.Column<string>(type: "text", nullable: true),
                CreatorName = table.Column<string>(type: "text", nullable: true),
                VideoId = table.Column<string>(type: "text", nullable: true),
                GameId = table.Column<string>(type: "text", nullable: true),
                Language = table.Column<string>(type: "text", nullable: true),
                ViewCount = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<string>(type: "text", nullable: true),
                ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                Duration = table.Column<float>(type: "real", nullable: false),
                VodOffset = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clips", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Hosts",
            columns: table => new
            {
                TwitchId = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                WhenOrdered = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                WaifuBrideId = table.Column<string>(type: "text", nullable: true),
                IsPrivated = table.Column<bool>(type: "boolean", nullable: false),
                OrderCount = table.Column<long>(type: "bigint", nullable: false),
                WaifuRollId = table.Column<long>(type: "bigint", nullable: false),
                WhenPrivated = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Hosts", x => x.TwitchId);
            }
        );

        migrationBuilder.CreateTable(
            name: "ResendLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TelegramMessage = table.Column<int>(type: "integer", nullable: false),
                DiscordMessage = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ResendLinks", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "TekkenCharacters",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                LinkToImage = table.Column<string>(type: "text", nullable: true),
                LastUpdateTime = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TekkenCharacters", x => x.Name);
            }
        );

        migrationBuilder.CreateTable(
            name: "TelegramUsers",
            columns: table => new
            {
                UserId = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                LastTimeMessage = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                RaidHelper = table.Column<bool>(type: "boolean", nullable: false),
                PyroAlertsAccess = table.Column<bool>(type: "boolean", nullable: false),
                HonkaiNotifications = table.Column<bool>(type: "boolean", nullable: false),
                StreamUpNotifications = table.Column<bool>(type: "boolean", nullable: false),
                ZenlessZoneZeroDailyNotif = table.Column<bool>(type: "boolean", nullable: false),
                ByeByeLastMessageTime = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                ByeByeServiceNotification = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelegramUsers", x => x.UserId);
            }
        );

        migrationBuilder.CreateTable(
            name: "Waifus",
            columns: table => new
            {
                ID = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                ShikiId = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "text", nullable: true),
                Age = table.Column<long>(type: "bigint", nullable: false),
                Anime = table.Column<string>(type: "text", nullable: true),
                Manga = table.Column<string>(type: "text", nullable: true),
                WhenAdded = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                LastOrder = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                OrderCount = table.Column<int>(type: "integer", nullable: false),
                IsPrivated = table.Column<bool>(type: "boolean", nullable: false),
                ImageUrl = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Waifus", x => x.ID);
            }
        );

        migrationBuilder.CreateTable(
            name: "AutoHello",
            columns: table => new
            {
                Guid = table.Column<Guid>(type: "uuid", nullable: false),
                HostId = table.Column<string>(type: "text", nullable: false),
                Time = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoHello", x => x.Guid);
                table.ForeignKey(
                    name: "FK_AutoHello_Hosts_HostId",
                    column: x => x.HostId,
                    principalTable: "Hosts",
                    principalColumn: "TwitchId"
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "CD",
            columns: table => new
            {
                Guid = table.Column<Guid>(type: "uuid", nullable: false),
                HostId = table.Column<string>(type: "text", nullable: false),
                Time = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CD", x => x.Guid);
                table.ForeignKey(
                    name: "FK_CD_Hosts_HostId",
                    column: x => x.HostId,
                    principalTable: "Hosts",
                    principalColumn: "TwitchId"
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "TekkenMoves",
            columns: table => new
            {
                CharacterName = table.Column<string>(type: "text", nullable: false),
                Command = table.Column<string>(type: "text", nullable: false),
                StanceCode = table.Column<string>(type: "text", nullable: false),
                StanceName = table.Column<string>(type: "text", nullable: true),
                HeatEngage = table.Column<bool>(type: "boolean", nullable: false),
                HeatSmash = table.Column<bool>(type: "boolean", nullable: false),
                PowerCrush = table.Column<bool>(type: "boolean", nullable: false),
                Throw = table.Column<bool>(type: "boolean", nullable: false),
                Homing = table.Column<bool>(type: "boolean", nullable: false),
                Tornado = table.Column<bool>(type: "boolean", nullable: false),
                HeatBurst = table.Column<bool>(type: "boolean", nullable: false),
                RequiresHeat = table.Column<bool>(type: "boolean", nullable: false),
                HitLevel = table.Column<string>(type: "text", nullable: true),
                Damage = table.Column<string>(type: "text", nullable: true),
                StartUpFrame = table.Column<string>(type: "text", nullable: true),
                BlockFrame = table.Column<string>(type: "text", nullable: true),
                HitFrame = table.Column<string>(type: "text", nullable: true),
                CounterHitFrame = table.Column<string>(type: "text", nullable: true),
                Notes = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TekkenMoves", x => new { x.CharacterName, x.Command });
                table.ForeignKey(
                    name: "FK_TekkenMoves_TekkenCharacters_CharacterName",
                    column: x => x.CharacterName,
                    principalTable: "TekkenCharacters",
                    principalColumn: "Name"
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_AutoHello_HostId",
            table: "AutoHello",
            column: "HostId",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_CD_HostId",
            table: "CD",
            column: "HostId",
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Alerts");

        migrationBuilder.DropTable(name: "AutoHello");

        migrationBuilder.DropTable(name: "AutoMessages");

        migrationBuilder.DropTable(name: "CD");

        migrationBuilder.DropTable(name: "Clips");

        migrationBuilder.DropTable(name: "ResendLinks");

        migrationBuilder.DropTable(name: "TekkenMoves");

        migrationBuilder.DropTable(name: "TelegramUsers");

        migrationBuilder.DropTable(name: "Waifus");

        migrationBuilder.DropTable(name: "Hosts");

        migrationBuilder.DropTable(name: "TekkenCharacters");
    }
}
