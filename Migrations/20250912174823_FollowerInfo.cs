using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class FollowerInfo : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FollowersEntitys",
            columns: table => new
            {
                UserId = table.Column<string>(type: "text", nullable: false),
                UserName = table.Column<string>(type: "text", nullable: false),
                UserLogin = table.Column<string>(type: "text", nullable: false),
                DisplayName = table.Column<string>(type: "text", nullable: true),
                ProfileImageUrl = table.Column<string>(type: "text", nullable: true),
                ChatColor = table.Column<string>(type: "text", nullable: true),
                IsModerator = table.Column<bool>(type: "boolean", nullable: false),
                IsVip = table.Column<bool>(type: "boolean", nullable: false),
                FollowedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                LastUpdated = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FollowersEntitys", x => x.UserId);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FollowersEntitys");
    }
}
