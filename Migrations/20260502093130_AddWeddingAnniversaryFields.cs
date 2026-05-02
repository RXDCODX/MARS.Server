using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddWeddingAnniversaryFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "LastWeddingCongratulatedOn",
            table: "TwitchUsers",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "WeddingDate",
            table: "TwitchUsers",
            type: "date",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastWeddingCongratulatedOn",
            table: "TwitchUsers");

        migrationBuilder.DropColumn(
            name: "WeddingDate",
            table: "TwitchUsers");
    }
}
