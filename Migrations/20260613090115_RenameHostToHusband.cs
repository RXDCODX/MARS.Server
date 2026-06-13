using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RenameHostToHusband : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AutoHello_Hosts_HostId",
            table: "AutoHello");

        migrationBuilder.DropForeignKey(
            name: "FK_CD_Hosts_HostId",
            table: "CD");

        migrationBuilder.DropForeignKey(
            name: "FK_Hosts_TwitchUsers_TwitchId",
            table: "Hosts");

        migrationBuilder.DropPrimaryKey(
            name: "PK_Hosts",
            table: "Hosts");

        migrationBuilder.RenameTable(
            name: "Hosts",
            newName: "Husbands");

        migrationBuilder.AddColumn<bool>(
            name: "MetaInfo_IsFreezeRequired",
            table: "Alerts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddPrimaryKey(
            name: "PK_Husbands",
            table: "Husbands",
            column: "TwitchId");

        migrationBuilder.AddForeignKey(
            name: "FK_AutoHello_Husbands_HostId",
            table: "AutoHello",
            column: "HostId",
            principalTable: "Husbands",
            principalColumn: "TwitchId");

        migrationBuilder.AddForeignKey(
            name: "FK_CD_Husbands_HostId",
            table: "CD",
            column: "HostId",
            principalTable: "Husbands",
            principalColumn: "TwitchId");

        migrationBuilder.AddForeignKey(
            name: "FK_Husbands_TwitchUsers_TwitchId",
            table: "Husbands",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AutoHello_Husbands_HostId",
            table: "AutoHello");

        migrationBuilder.DropForeignKey(
            name: "FK_CD_Husbands_HostId",
            table: "CD");

        migrationBuilder.DropForeignKey(
            name: "FK_Husbands_TwitchUsers_TwitchId",
            table: "Husbands");

        migrationBuilder.DropPrimaryKey(
            name: "PK_Husbands",
            table: "Husbands");

        migrationBuilder.DropColumn(
            name: "MetaInfo_IsFreezeRequired",
            table: "Alerts");

        migrationBuilder.RenameTable(
            name: "Husbands",
            newName: "Hosts");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Hosts",
            table: "Hosts",
            column: "TwitchId");

        migrationBuilder.AddForeignKey(
            name: "FK_AutoHello_Hosts_HostId",
            table: "AutoHello",
            column: "HostId",
            principalTable: "Hosts",
            principalColumn: "TwitchId");

        migrationBuilder.AddForeignKey(
            name: "FK_CD_Hosts_HostId",
            table: "CD",
            column: "HostId",
            principalTable: "Hosts",
            principalColumn: "TwitchId");

        migrationBuilder.AddForeignKey(
            name: "FK_Hosts_TwitchUsers_TwitchId",
            table: "Hosts",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict);
    }
}
