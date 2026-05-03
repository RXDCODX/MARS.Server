using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWeddingFromTwitchUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWeddingCongratulatedMonths",
                table: "TwitchUsers");

            migrationBuilder.DropColumn(
                name: "LastWeddingCongratulatedOn",
                table: "TwitchUsers");

            migrationBuilder.DropColumn(
                name: "WeddingDate",
                table: "TwitchUsers");

            migrationBuilder.AddColumn<int>(
                name: "LastWeddingCongratulatedMonths",
                table: "Hosts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWeddingCongratulatedMonths",
                table: "Hosts");

            migrationBuilder.AddColumn<int>(
                name: "LastWeddingCongratulatedMonths",
                table: "TwitchUsers",
                type: "integer",
                nullable: true);

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
    }
}
