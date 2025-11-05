using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RefactorMikuMondayToUseBaseTrackInfo : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MikuMondayActivations_MikuTracks_MikuTrackId",
            table: "MikuMondayActivations"
        );

        migrationBuilder.DropTable(name: "MikuTracks");

        migrationBuilder.RenameColumn(
            name: "MikuTrackId",
            table: "MikuMondayActivations",
            newName: "MikuMondayTrackId"
        );

        migrationBuilder.RenameIndex(
            name: "IX_MikuMondayActivations_MikuTrackId",
            table: "MikuMondayActivations",
            newName: "IX_MikuMondayActivations_MikuMondayTrackId"
        );

        migrationBuilder.CreateTable(
            name: "MikuMondayTracks",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Number = table.Column<int>(type: "integer", nullable: false),
                BaseTrackInfoId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MikuMondayTracks", x => x.Id);
                table.ForeignKey(
                    name: "FK_MikuMondayTracks_SoundRequestBaseTrackInfos_BaseTrackInfoId",
                    column: x => x.BaseTrackInfoId,
                    principalTable: "SoundRequestBaseTrackInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_MikuMondayTracks_BaseTrackInfoId",
            table: "MikuMondayTracks",
            column: "BaseTrackInfoId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_MikuMondayTracks_Number",
            table: "MikuMondayTracks",
            column: "Number",
            unique: true
        );

        migrationBuilder.AddForeignKey(
            name: "FK_MikuMondayActivations_MikuMondayTracks_MikuMondayTrackId",
            table: "MikuMondayActivations",
            column: "MikuMondayTrackId",
            principalTable: "MikuMondayTracks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MikuMondayActivations_MikuMondayTracks_MikuMondayTrackId",
            table: "MikuMondayActivations"
        );

        migrationBuilder.DropTable(name: "MikuMondayTracks");

        migrationBuilder.RenameColumn(
            name: "MikuMondayTrackId",
            table: "MikuMondayActivations",
            newName: "MikuTrackId"
        );

        migrationBuilder.RenameIndex(
            name: "IX_MikuMondayActivations_MikuMondayTrackId",
            table: "MikuMondayActivations",
            newName: "IX_MikuMondayActivations_MikuTrackId"
        );

        migrationBuilder.CreateTable(
            name: "MikuTracks",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Artist = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                Number = table.Column<int>(type: "integer", nullable: false),
                ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                Title = table.Column<string>(type: "text", nullable: false),
                Url = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MikuTracks", x => x.Id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_MikuTracks_Number",
            table: "MikuTracks",
            column: "Number",
            unique: true
        );

        migrationBuilder.AddForeignKey(
            name: "FK_MikuMondayActivations_MikuTracks_MikuTrackId",
            table: "MikuMondayActivations",
            column: "MikuTrackId",
            principalTable: "MikuTracks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );
    }
}
