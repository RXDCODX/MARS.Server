using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations
{
    /// <inheritdoc />
    public partial class SeedMikuMondayAlertMedia : Migration
    {
        /// <summary>
        /// Статичный Guid записи Miku Monday Alert в таблице Alerts.
        /// Используется наградой MikuMondayAlert_TwitchReward для поиска медиа.
        /// </summary>
        internal static readonly Guid MikuMondayAlertMediaId = new(
            "B4A5F2C1-0000-4D49-4B55-4D4F4E444159"
        );

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Alerts",
                columns:
                [
                    "Id",
                    "FileInfo_Extension",
                    "FileInfo_FileName",
                    "FileInfo_IsFileNotConvertable",
                    "FileInfo_IsLocal",
                    "FileInfo_LocalFilePath",
                    "FileInfo_Type",
                    "MetaInfo_IsEnabled",
                    "MetaInfo_IsFreezeRequired",
                    "MetaInfo_DisplayName",
                    "MetaInfo_Duration",
                    "MetaInfo_IsLooped",
                    "MetaInfo_Priority",
                    "MetaInfo_TwitchGuid",
                    "MetaInfo_TwitchPointsCost",
                    "MetaInfo_VIP",
                    "PositionInfo_Height",
                    "PositionInfo_IsHorizontalCenter",
                    "PositionInfo_IsProportion",
                    "PositionInfo_IsResizeRequires",
                    "PositionInfo_IsRotated",
                    "PositionInfo_IsUseOriginalWidthAndHeight",
                    "PositionInfo_IsVerticallCenter",
                    "PositionInfo_RandomCoordinates",
                    "PositionInfo_Rotation",
                    "PositionInfo_Width",
                    "PositionInfo_XCoordinate",
                    "PositionInfo_YCoordinate",
                    "StylesInfo_IsBorder",
                    "StylesInfo_IsShowLetterbox",
                    "TextInfo_KeyWordSybmolDelimiter",
                    "TextInfo_KeyWordsColor",
                    "TextInfo_Text",
                    "TextInfo_TextColor",
                    "TextInfo_TriggerWord",
                    "MetaInfo_Volume",
                ],
                values:
                [
                    MikuMondayAlertMediaId,
                    ".mp4",
                    "MikuMondayAlert.mp4",
                    false,
                    true,
                    "/Alerts/MikuMondayAlert.mp4",
                    "Video",
                    true,
                    false,
                    "Miku Monday Alert",
                    7,
                    false,
                    1,
                    null,
                    0,
                    false,
                    500,
                    false,
                    true,
                    false,
                    true,
                    true,
                    false,
                    true,
                    0,
                    500,
                    0,
                    0,
                    false,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    100,
                ]
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Alerts",
                keyColumn: "Id",
                keyValue: MikuMondayAlertMediaId
            );
        }
    }
}
