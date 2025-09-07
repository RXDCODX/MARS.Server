using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RemoveBackupService : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PgDumpSettings");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PgDumpSettings",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                BackupPath = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true
                ),
                Comment = table.Column<string>(
                    type: "character varying(1000)",
                    maxLength: 1000,
                    nullable: true
                ),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                PgDumpPath = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false
                ),
                UpdatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PgDumpSettings", x => x.Id);
            }
        );
    }
}
