using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class BooruPosting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ScheduledAtUtc",
            table: "DanbooruAutoPostConfigs");

        migrationBuilder.AddColumn<int>(
            name: "PlanningHorizonDays",
            table: "DanbooruAutoPostConfigs",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PlanningHorizonDays",
            table: "DanbooruAutoPostConfigs");

        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledAtUtc",
            table: "DanbooruAutoPostConfigs",
            type: "timestamp with time zone",
            nullable: true);
    }
}
