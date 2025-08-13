using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class RemoveFrameDataChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FramedataChangeInfos");

        migrationBuilder.DropTable(name: "FramedataChanges");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FramedataChanges",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                AppliedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                ChangeType = table.Column<string>(type: "text", nullable: false),
                CharacterName = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                Description = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true
                ),
                DetectedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                Status = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FramedataChanges", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "FramedataChangeInfos",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                FramedataChangeId = table.Column<int>(type: "integer", nullable: true),
                CurrentInfoId = table.Column<int>(type: "integer", nullable: true),
                DataHash = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true
                ),
                InfoType = table.Column<string>(type: "text", nullable: false),
                JsonData = table.Column<string>(type: "text", nullable: false),
                RetrievedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                SourceUrl = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FramedataChangeInfos", x => x.Id);
                table.ForeignKey(
                    name: "FK_FramedataChangeInfos_FramedataChanges_CurrentInfoId",
                    column: x => x.CurrentInfoId,
                    principalTable: "FramedataChanges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_FramedataChangeInfos_FramedataChanges_FramedataChangeId",
                    column: x => x.FramedataChangeId,
                    principalTable: "FramedataChanges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_FramedataChangeInfos_CurrentInfoId",
            table: "FramedataChangeInfos",
            column: "CurrentInfoId",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_FramedataChangeInfos_FramedataChangeId",
            table: "FramedataChangeInfos",
            column: "FramedataChangeId",
            unique: true
        );
    }
}
