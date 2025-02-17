using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class Media_Info : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "YCoordinate",
            table: "Alerts",
            newName: "PositionInfo_YCoordinate"
        );

        migrationBuilder.RenameColumn(
            name: "XCoordinate",
            table: "Alerts",
            newName: "PositionInfo_XCoordinate"
        );

        migrationBuilder.RenameColumn(
            name: "Width",
            table: "Alerts",
            newName: "PositionInfo_Width"
        );

        migrationBuilder.RenameColumn(name: "VIP", table: "Alerts", newName: "MetaInfo_VIP");

        migrationBuilder.RenameColumn(name: "Type", table: "Alerts", newName: "FileInfo_Type");

        migrationBuilder.RenameColumn(
            name: "TwitchPointsCost",
            table: "Alerts",
            newName: "MetaInfo_TwitchPointsCost"
        );

        migrationBuilder.RenameColumn(
            name: "TextColor",
            table: "Alerts",
            newName: "TextInfo_TextColor"
        );

        migrationBuilder.RenameColumn(name: "Text", table: "Alerts", newName: "TextInfo_Text");

        migrationBuilder.RenameColumn(
            name: "Rotation",
            table: "Alerts",
            newName: "PositionInfo_Rotation"
        );

        migrationBuilder.RenameColumn(
            name: "RandomCoordinates",
            table: "Alerts",
            newName: "PositionInfo_RandomCoordinates"
        );

        migrationBuilder.RenameColumn(
            name: "KeyWord",
            table: "Alerts",
            newName: "TextInfo_KeyWord"
        );

        migrationBuilder.RenameColumn(
            name: "IsRotated",
            table: "Alerts",
            newName: "PositionInfo_IsRotated"
        );

        migrationBuilder.RenameColumn(
            name: "IsResizeRequires",
            table: "Alerts",
            newName: "PositionInfo_IsResizeRequires"
        );

        migrationBuilder.RenameColumn(
            name: "IsProportion",
            table: "Alerts",
            newName: "PositionInfo_IsProportion"
        );

        migrationBuilder.RenameColumn(
            name: "IsLooped",
            table: "Alerts",
            newName: "MetaInfo_IsLooped"
        );

        migrationBuilder.RenameColumn(
            name: "IsBorder",
            table: "Alerts",
            newName: "StylesInfo_IsBorder"
        );

        migrationBuilder.RenameColumn(
            name: "Height",
            table: "Alerts",
            newName: "PositionInfo_Height"
        );

        migrationBuilder.RenameColumn(
            name: "Duration",
            table: "Alerts",
            newName: "MetaInfo_Duration"
        );

        migrationBuilder.RenameColumn(
            name: "DisplayName",
            table: "Alerts",
            newName: "MetaInfo_DisplayName"
        );

        migrationBuilder.RenameColumn(
            name: "FilePath",
            table: "Alerts",
            newName: "FileInfo_LocalFilePath"
        );

        migrationBuilder.AlterColumn<string>(
            name: "FileInfo_Type",
            table: "Alerts",
            type: "text",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer"
        );

        migrationBuilder.AlterColumn<string>(
            name: "MetaInfo_DisplayName",
            table: "Alerts",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "FileInfo_Extension",
            table: "Alerts",
            type: "text",
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "FileInfo_FileName",
            table: "Alerts",
            type: "text",
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "TextInfo_KeyWordsColor",
            table: "Alerts",
            type: "text",
            nullable: true,
            defaultValue: ""
        );

        migrationBuilder.CreateTable(
            name: "RandomMemeOrder",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Order = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                FilePath = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RandomMemeOrder", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RandomMemeOrder");

        migrationBuilder.DropColumn(name: "TextInfo_KeyWordsColor", table: "Alerts");

        migrationBuilder.RenameColumn(
            name: "FileInfo_LocalFilePath",
            table: "Alerts",
            newName: "FilePath"
        );

        migrationBuilder.DropColumn(name: "FileInfo_Extension", table: "Alerts");

        migrationBuilder.DropColumn(name: "FileInfo_FileName", table: "Alerts");

        migrationBuilder.RenameColumn(
            name: "TextInfo_TextColor",
            table: "Alerts",
            newName: "TextColor"
        );

        migrationBuilder.RenameColumn(name: "TextInfo_Text", table: "Alerts", newName: "Text");

        migrationBuilder.RenameColumn(
            name: "TextInfo_KeyWord",
            table: "Alerts",
            newName: "KeyWord"
        );

        migrationBuilder.RenameColumn(
            name: "StylesInfo_IsBorder",
            table: "Alerts",
            newName: "IsBorder"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_YCoordinate",
            table: "Alerts",
            newName: "YCoordinate"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_XCoordinate",
            table: "Alerts",
            newName: "XCoordinate"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_Width",
            table: "Alerts",
            newName: "Width"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_Rotation",
            table: "Alerts",
            newName: "Rotation"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_RandomCoordinates",
            table: "Alerts",
            newName: "RandomCoordinates"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_IsRotated",
            table: "Alerts",
            newName: "IsRotated"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_IsResizeRequires",
            table: "Alerts",
            newName: "IsResizeRequires"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_IsProportion",
            table: "Alerts",
            newName: "IsProportion"
        );

        migrationBuilder.RenameColumn(
            name: "PositionInfo_Height",
            table: "Alerts",
            newName: "Height"
        );

        migrationBuilder.RenameColumn(name: "MetaInfo_VIP", table: "Alerts", newName: "VIP");

        migrationBuilder.RenameColumn(
            name: "MetaInfo_TwitchPointsCost",
            table: "Alerts",
            newName: "TwitchPointsCost"
        );

        migrationBuilder.RenameColumn(
            name: "MetaInfo_IsLooped",
            table: "Alerts",
            newName: "IsLooped"
        );

        migrationBuilder.RenameColumn(
            name: "MetaInfo_Duration",
            table: "Alerts",
            newName: "Duration"
        );

        migrationBuilder.RenameColumn(
            name: "MetaInfo_DisplayName",
            table: "Alerts",
            newName: "DisplayName"
        );

        migrationBuilder.RenameColumn(name: "FileInfo_Type", table: "Alerts", newName: "Type");

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "Alerts",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<int>(
            name: "Type",
            table: "Alerts",
            type: "integer",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.CreateTable(
            name: "TekkenCharacters",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                LastUpdateTime = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                LinkToImage = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TekkenCharacters", x => x.Name);
            }
        );

        migrationBuilder.CreateTable(
            name: "TekkenMoves",
            columns: table => new
            {
                CharacterName = table.Column<string>(type: "text", nullable: false),
                Command = table.Column<string>(type: "text", nullable: false),
                BlockFrame = table.Column<string>(type: "text", nullable: true),
                CounterHitFrame = table.Column<string>(type: "text", nullable: true),
                Damage = table.Column<string>(type: "text", nullable: true),
                HeatBurst = table.Column<bool>(type: "boolean", nullable: false),
                HeatEngage = table.Column<bool>(type: "boolean", nullable: false),
                HeatSmash = table.Column<bool>(type: "boolean", nullable: false),
                HitFrame = table.Column<string>(type: "text", nullable: true),
                HitLevel = table.Column<string>(type: "text", nullable: true),
                Homing = table.Column<bool>(type: "boolean", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true),
                PowerCrush = table.Column<bool>(type: "boolean", nullable: false),
                RequiresHeat = table.Column<bool>(type: "boolean", nullable: false),
                StanceCode = table.Column<string>(type: "text", nullable: false),
                StanceName = table.Column<string>(type: "text", nullable: true),
                StartUpFrame = table.Column<string>(type: "text", nullable: true),
                Throw = table.Column<bool>(type: "boolean", nullable: false),
                Tornado = table.Column<bool>(type: "boolean", nullable: false),
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
    }
}
