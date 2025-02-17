using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class HelloVideosFix : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FileExtension", table: "HelloVideosUsers");

        migrationBuilder.DropColumn(name: "LocalFilePath", table: "HelloVideosUsers");

        migrationBuilder.RenameColumn(
            name: "TextInfo_KeyWord",
            table: "Alerts",
            newName: "TextInfo_TriggerWord"
        );

        migrationBuilder.AddColumn<Guid>(
            name: "MediaInfoId",
            table: "HelloVideosUsers",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
        );

        migrationBuilder.AddColumn<char>(
            name: "TextInfo_KeyWordSybmolDelimiter",
            table: "Alerts",
            type: "character(1)",
            nullable: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_HelloVideosUsers_MediaInfoId",
            table: "HelloVideosUsers",
            column: "MediaInfoId",
            unique: true
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HelloVideosUsers_Alerts_MediaInfoId",
            table: "HelloVideosUsers",
            column: "MediaInfoId",
            principalTable: "Alerts",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_HelloVideosUsers_Alerts_MediaInfoId",
            table: "HelloVideosUsers"
        );

        migrationBuilder.DropIndex(
            name: "IX_HelloVideosUsers_MediaInfoId",
            table: "HelloVideosUsers"
        );

        migrationBuilder.DropColumn(name: "MediaInfoId", table: "HelloVideosUsers");

        migrationBuilder.DropColumn(name: "TextInfo_KeyWordSybmolDelimiter", table: "Alerts");

        migrationBuilder.RenameColumn(
            name: "TextInfo_TriggerWord",
            table: "Alerts",
            newName: "TextInfo_KeyWord"
        );

        migrationBuilder.AddColumn<string>(
            name: "FileExtension",
            table: "HelloVideosUsers",
            type: "text",
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "LocalFilePath",
            table: "HelloVideosUsers",
            type: "text",
            nullable: false,
            defaultValue: ""
        );
    }
}
