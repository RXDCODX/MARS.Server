using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddTwitchUserTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "WaifuRollGuarantees",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "TwitchLeaderboardUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "TwitchLeaderboardUsers",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Hosts",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "Hosts",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HonkaiMarkupUser",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HelloVideosUsers",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "HelloVideosUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FumoUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "FumoUsers",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserName",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserLogin",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "ProfileImageUrl",
            table: "FollowersEntitys",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "ChatColor",
            table: "FollowersEntitys",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "FollowersEntitys",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "CD",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "AutoHello",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.CreateTable(
            name: "TwitchUsers",
            columns: table => new
            {
                TwitchId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
                UserLogin = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                DisplayName = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                ProfileImageUrl = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true
                ),
                ChatColor = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: true
                ),
                IsModerator = table.Column<bool>(type: "boolean", nullable: false),
                IsVip = table.Column<bool>(type: "boolean", nullable: false),
                FollowedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                LastUpdated = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TwitchUsers", x => x.TwitchId);
            }
        );

        migrationBuilder.Sql(
            """
            INSERT INTO "TwitchUsers" (
                "TwitchId",
                "UserLogin",
                "DisplayName",
                "ProfileImageUrl",
                "ChatColor",
                "IsModerator",
                "IsVip",
                "FollowedAt",
                "LastUpdated",
                "CreatedAt"
            )
            VALUES
            (
                '138694941',
                'saiternal',
                'Saiternal',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/cdd517fe-def4-11e9-948e-784f43822e80-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '439056875',
                'plaksych',
                'Plaksych',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/dbb2ee38-4c53-45e7-83f7-6fea4a69a3bb-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1052088882',
                'decamelia_',
                'decamelia_',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/ef4347e2-b6b8-40f8-a534-f1560f7933e7-profile_image-300x300.jpeg',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1067139746',
                'webstorm1337',
                'webstorm1337',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/ead5c8b2-a4c9-4724-b1dd-9f00b46cbd3d-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1093845998',
                'trulachatbot',
                'trulachatbot',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebb84563-db81-4b9c-8940-64ed33ccfc7b-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1095252291',
                'sosok_anby69',
                'sosok_anby69',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/142ba239-f8a5-4354-aab2-63dadeec9f2c-profile_image-300x300.jpeg',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1125628874',
                'cdofy__',
                'cdofy__',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/2c1cea33-a534-4cc4-90f1-d7580ff69f8a-profile_image-300x300.png',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1126135945',
                'leale__',
                'leale__',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/b8eb8600-ad3c-4f89-869c-50cf3c61651c-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1136212597',
                'srg_prime',
                'SRG_prime',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/ebef8b14-0054-41be-8283-981dab1ec90d-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1137756031',
                'elizabethwalker02',
                'elizabethwalker02',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/a1fb9cb4-28c2-4332-ba5d-a9637661ece6-profile_image-300x300.png',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '117509760',
                'tpehep192',
                'TPEHEP192',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/c2f6124d-f922-41c2-8aab-75fece58fd67-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1224600467',
                'sigma22866675',
                'sigma22866675',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/0c16dd2f-403e-4654-b10c-c42810fe909f-profile_image-300x300.jpeg',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '124597564',
                'sailormoon__________',
                'sailormoon__________',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/ca1f694e-9629-43aa-a674-233078049d60-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1301970296',
                'alisaassistant',
                'AlisaAssistant',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/44b0935d-8e4c-421f-8688-e43c12efb215-profile_image-300x300.png',
                '#FF0080',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1345714135',
                'aigisuai',
                'AigisuAI',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/54eab3bd-8d1f-4c70-a7ec-affdb8a0cffe-profile_image-300x300.png',
                '#FFFFFF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1358848435',
                'daviddo6y',
                'daviddo6y',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/de130ab0-def7-11e9-b668-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '136797418',
                'energojump',
                'energojump',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/41780b5a-def8-11e9-94d9-784f43822e80-profile_image-300x300.png',
                '#DAA520',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '148545436',
                'mag_orange',
                'mag_orange',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/13e5fa74-defa-11e9-809c-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '157147824',
                't1_kt0',
                't1_kt0',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/80dadd00-3402-4a2f-b152-57c7c038ef36-profile_image-300x300.png',
                '#5F9EA0',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '158105504',
                'coffe_devil',
                'coffe_devil',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/719f4c8d-6651-4074-b413-2ee6c9b7303d-profile_image-300x300.png',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '167580741',
                'ikavvai',
                'iKavvai',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/fcf5e5ee-6e68-4884-887c-883dbb2544f6-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '179789102',
                'janefioletovo101',
                'JaneFioletovo101',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/f9de4fca-334f-4818-bc5a-c132653e7b0f-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '180814901',
                'shendo_wt',
                'shendo_wt',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/f2cd2378-80da-4a3e-89d1-e8dd855c6cab-profile_image-300x300.png',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '192541047',
                'beast666beast',
                'Beast666Beast',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/ec6a9ece-3edf-4784-8df9-10d659903c24-profile_image-300x300.png',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '195710124',
                'aozora_143r',
                'aozora_143R',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/90ee346a-2ff6-4dd1-b313-b36e88db4bc9-profile_image-300x300.png',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '231797136',
                'vxmxs',
                'VxMxS',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/94c02524-17d2-450e-8bec-71e81b5177c2-profile_image-300x300.png',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '264496563',
                'tsoynasvay',
                'TsoyNasvay',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/1e780377-e498-493b-b955-e08cbae86fff-profile_image-300x300.png',
                '#5F9EA0',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '29072697',
                'ponakota',
                'ponakota',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/41780b5a-def8-11e9-94d9-784f43822e80-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '37795951',
                'doshipanda',
                'doshipanda',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/b2c828e9-7a94-4fab-ba9d-17f845c1d614-profile_image-300x300.png',
                '#FF4500',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '414145778',
                'sansmmr',
                'sansmmr',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/c42e10f8-f9e9-474e-ad63-cbdfe9bc8fc4-profile_image-300x300.png',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '42350875',
                'straven_shihoin',
                'Straven_Shihoin',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/6f4d37fc-edc5-4ded-ade0-38caeefe4dd3-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '42467318',
                'potatozauros',
                'Potatozauros',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/00798d75-9433-4730-9018-b77805afa0f2-profile_image-300x300.png',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '424727437',
                'heavenwantsbenz',
                'heavenwantsbenz',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/f449cb91-9818-48e6-ad97-06c8c0938a6f-profile_image-300x300.jpeg',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '47010495',
                'edge0100',
                'Edge0100',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebe4cd89-b4f4-4cd9-adac-2f30151b4209-profile_image-300x300.png',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '512425796',
                'motabon',
                'モタボン',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/7c8ec6f4-0bb2-474a-82fc-b12b58a31989-profile_image-300x300.png',
                '#DAA520',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '513924166',
                'alan_krain',
                'Alan_Krain',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/874f7d2e-fa4f-4142-b9c4-3068fe509435-profile_image-300x300.jpeg',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '527194989',
                'tversette',
                'tversette',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/b959f61b-bf4d-4e6c-a6e3-ba9253acd4fb-profile_image-300x300.png',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '59609747',
                'rinnegantk',
                'RinneganTK',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/9552a851-11d8-461c-8f23-b4b29952c426-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '62461495',
                'donnord',
                'DonNord',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/dd4756e1-73c5-429e-a628-df4a0d814dbc-profile_image-300x300.png',
                '#00FF7F',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '642751358',
                'dexstar00',
                'DexStar00',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/008e9963-74d2-4ec6-b34b-9dd76ae0471b-profile_image-300x300.jpeg',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '64547524',
                'karlik_s_vostoka',
                'karlik_s_vostoka',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/f9eec180-817b-4226-b1d1-813727831958-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '699683178',
                'deadcndance',
                'deadcndance',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebe4cd89-b4f4-4cd9-adac-2f30151b4209-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '723766021',
                'bruvamasc',
                'Bruvamasc',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/397fe543-89f6-49bf-a92a-568fc0a558a0-profile_image-300x300.png',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '731257324',
                'gordienkodev',
                'GordienkoDev',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/711c97b4-8f46-4a73-86b0-a97e1f86ffe3-profile_image-300x300.png',
                '#B22222',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '760308028',
                'redqueen__1433',
                'redqueen__1433',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/84b1b499-3809-48ea-bb28-045ea0b3a3d7-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '771148033',
                'drakonli',
                'drakonli',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/cdd517fe-def4-11e9-948e-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '77322336',
                'anubisaract',
                'Anubisaract',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/fa07eab1-08f9-4806-9d9b-1dff2f33cdc4-profile_image-300x300.png',
                '#DAA520',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '783645904',
                'crazu33',
                'Crazu33',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/ac01452a-31d4-4d7f-8660-876cfb430dd6-profile_image-300x300.png',
                '#FF4500',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '79103513',
                'woodwe',
                'WooDWE',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/7c6d4cb6-0fa6-4f5e-b6dd-9ea4f7b4be09-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '825313679',
                'completecontrolzxz',
                'CompleteControlZXZ',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/dbdc9198-def8-11e9-8681-784f43822e80-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '846772383',
                'jeetbot',
                'JeetBot',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/42cb4f97-c6d5-4d0f-82f7-0a1df81186cf-profile_image-300x300.png',
                '#00FF7F',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '854983207',
                'mister_fr3d',
                'mister_fr3d',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/22edb82f-ee92-4051-ab41-09f8bf806100-profile_image-300x300.png',
                '#D2691E',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '880458648',
                'destinynarukami',
                'destinynarukami',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/8c32cb01-29f2-4191-892b-c814a5288a2f-profile_image-300x300.jpeg',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '888848441',
                'catisaai',
                'CatisaAI',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/004722d9-a2bc-4d2c-95a9-a0a4059fbc93-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '89308929',
                'sandro_blade',
                'Sandro_blade',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/873fcdbb-0147-499a-bb65-216b1e302974-profile_image-300x300.png',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '931580204',
                'fwvvwrr',
                'fwvvwrr',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/d2ee62d5-8798-4c21-9fd7-9e3b8436b691-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '93172082',
                'shapowoler',
                'shapowoler',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/910e3381-fa3d-4872-b846-d021a6d77823-profile_image-300x300.png',
                '#D2691E',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '960240048',
                'amigaryu',
                '쐐기풀〇누워〇있는〇용',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/65cc67a0-b127-4f79-a756-283f9d1db63f-profile_image-300x300.png',
                '#5F9EA0',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '100135110',
                'streamelements',
                'streamelements',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1002568621',
                'youngsharpbarb',
                'youngsharpbarb',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1008705438',
                'dobrtyt',
                'dobrtyt',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '101407273',
                'scorpion_karma',
                'scorpion_karma',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/c6ee0421-ac19-4434-b9c3-99c1801934f2-profile_image-300x300.png',
                '#00FF7F',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '101435577',
                'ell3rd_',
                'ell3rd_',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1031217153',
                'marefnn1',
                'marefnn1',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1037037936',
                'mainman1313',
                'mainman1313',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/7336cfcc-1116-4a9e-93b3-dbe9faf8744c-profile_image-300x300.jpeg',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1072069935',
                'vanyambo1234',
                'vanyambo1234',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/215b7342-def9-11e9-9a66-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1074033622',
                'fushiguro_24',
                'fushiguro_24',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1085705302',
                'misttroy3',
                'misttroy3',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/f1d9d3ba-c3fb-4af1-92d8-3aace167b5d8-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1088118383',
                'burningmymind1',
                'burningmymind1',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/9af24aeb-7075-403a-af14-d4a84fd594a1-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '109427158',
                'aromorf',
                'Aromorf',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/aromorf-profile_image-2de7b031a6d5901b-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '109785665',
                'durabandura',
                'DuraBandura',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/durabandura-profile_image-fff51ec9f3142e62-300x300.jpeg',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1105567679',
                'tv50bdawag',
                'tv50bdawag',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1122106958',
                'gefah88k',
                'gefah88k',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1147200422',
                'zavvierr',
                'zavvierr',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1149864714',
                'cxemaz0v',
                'cxemaz0v',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1156113677',
                'soldatov_yt',
                'soldatov_yt',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1165915209',
                'demian02024',
                'demian02024',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1166068463',
                'verborum_deiectio',
                'verborum_deiectio',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '117284896',
                'smertokotick',
                'smertokotick',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '118250546',
                'sasailalky',
                'sasailalky',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1189470412',
                'benladnoo',
                'benladnoo',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1218481766',
                'heav7assaul1tank',
                'heav7assaul1tank',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1242946859',
                'thold_4244',
                'thold_4244',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1252476488',
                'jeff_monet12',
                'jeff_monet12',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/998f01ae-def8-11e9-b95c-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '125507630',
                'majeiestier',
                'majeiestier',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '126722854',
                'gleb_legend_killer_2001',
                'gleb_legend_killer_2001',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/cb53e58f-8445-4051-b1e0-17f3c0f0d357-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '126931084',
                'sanadzaki',
                'Sanadzaki',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1274455267',
                'personawqq',
                'personawqq',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1292310736',
                'komaru_sparda',
                'komaru_sparda',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1305814373',
                'neutralbackkorobka',
                'neutralbackkorobka',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1312268455',
                'catalinacatalinaxx73',
                'catalinacatalinaxx73',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1314003143',
                'yael071112013',
                'yael071112013',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebb84563-db81-4b9c-8940-64ed33ccfc7b-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1317227846',
                'vicor_emmy879',
                'vicor_emmy879',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1329175578',
                'deemonie_t',
                'deemonie_t',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1339268483',
                'rassyyy11737',
                'rassyyy11737',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1350742768',
                'illusiverion',
                'illusiverion',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1362484554',
                'stream_075',
                'stream_075',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '148448661',
                'brocklesna_r',
                'brocklesna_r',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/4980c467-c6aa-46f5-89b0-509d4ee62595-profile_image-300x300.png',
                '#FF4500',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '150689943',
                'belgraxx',
                'Belgraxx',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/4fe62f4c-c221-497d-9ed6-4b6afe41403c-profile_image-300x300.jpg',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '1564983',
                'moobot',
                'Moobot',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '166767990',
                '09c0',
                '09c0',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-300x300.png',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '183869726',
                'vbdrr',
                'vBdrr',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/00d57a61-d21b-40ce-b8ac-b20f70280285-profile_image-300x300.jpeg',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '189206248',
                'ursomon',
                'ursomon',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '189990259',
                '100cringe',
                '100cringe',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '19264788',
                'nightbot',
                'nightbot',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '198164251',
                'fanatlmgmob',
                'fanatlmgmob',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '203773375',
                's3nketsuuu',
                's3nketsuuu',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/0ede84b3-1b6a-48c0-87ea-260871cae383-profile_image-300x300.png',
                '#000000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '207680013',
                'lkaff_art',
                'Lkaff_ART',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/c3a6b6a6-7818-4e40-8b02-a17b91a9eac8-profile_image-300x300.png',
                '#9ACD32',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '207723361',
                'mex13370',
                'mex13370',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/74bdc65c-7502-4fbb-99e8-31ad649aa098-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '234528939',
                'sandorc1eganee',
                'sandorc1eganee',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '236032648',
                'blackpowder',
                'BlackPowder',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '248308192',
                '1eydey',
                '1eydey',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/7c8d3f3c-15cb-4ed6-9351-60bb3083af4e-profile_image-300x300.png',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '31308539',
                'morally_grey',
                'morally_grey',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '37928160',
                'aoiharu',
                'AoiHaru',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/aoiharu-profile_image-f1a79e2d29b22504-300x300.jpeg',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '39346053',
                'gips__',
                'Gips__',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/gips__-profile_image-549bd2be5db5cc4c-300x300.jpeg',
                '#1E90FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '39803249',
                'atef808',
                'Atef808',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/739eba45-0b40-4608-b8eb-a7575daf6c97-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '407910802',
                'sscorpionnpro',
                'sscorpionnpro',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '421182469',
                'dustdufox',
                'dustdufox',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '426150439',
                'egoist_555',
                'EGOist_555',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '440568770',
                'morphidev',
                'morphidev',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '441688439',
                'gandalf598',
                'gandalf598',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/98c10715-ba7d-4e76-a2a7-5c14b6f9483a-profile_image-300x300.jpeg',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '443144474',
                'maxislinou',
                'maxislinou',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '457063567',
                'kirikirikii',
                'kirikirikii',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '465552836',
                'dikiypep',
                'dikiypep',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '475180647',
                'pivo_sailormoon',
                'pivo_sailormoon',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/606f288c-a4dc-470e-b649-b37d58ec7a6b-profile_image-300x300.jpeg',
                '#FF7F50',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '491097578',
                'halaps',
                'Halaps',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/dafad2cf-68c0-478f-8774-72bfaf3a5481-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '498634901',
                'cemoal',
                'cemoal',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '500603394',
                'qp_illson',
                'qp_illson',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '502859534',
                'reccontrol',
                'reccontrol',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/a6658b02-e6cb-4e3c-82a9-05eac6b6e380-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '512893353',
                'gggjxd',
                'gggjxd',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '523145924',
                'neranika',
                'neranika',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '523198594',
                'orthodox_choice',
                'orthodox_choice',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '524415932',
                'snm8',
                'snm8',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '533361038',
                'maria___friendly___sunny',
                'MARIA___friendly___sunny',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '539170290',
                'excellsnow',
                'excellsnow',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '555882241',
                'thatdonnog',
                'thatdonnog',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '561321212',
                'alerontimak',
                'alerontimak',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '562858891',
                'freu_xd',
                'freu_xd',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/2c37d053-6bbe-499e-a981-62b974bfc7b9-profile_image-300x300.png',
                '#0000FF',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '58141656',
                'neron_96',
                'NeroN_96',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '592070517',
                'enfy7',
                'enfy7',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '620504092',
                'killeddelirium',
                'killeddelirium',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/13e5fa74-defa-11e9-809c-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '629458269',
                'lucifer___san',
                'lucifer___san',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '637230401',
                'hiitachins',
                'hiitachins',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '653720646',
                'stezenkok',
                'stezenkok',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/11290ce1-5fc6-476e-9c99-f86211c40a7e-profile_image-300x300.png',
                '#00FF7F',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '668055240',
                'kersh01',
                'kersh01',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '68176805',
                'getreal_ss',
                'GetReaL_ss',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/25ef1034-8936-4028-b43f-2bac0cb116b6-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '682735787',
                'miyadzaki_2',
                'miyadzaki_2',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '692934988',
                'yoko_mizuno',
                '水野蓉子',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/6f78f725-ae3b-41d6-abba-afffe53912dc-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '717308880',
                'arman700',
                'arman700',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '735594864',
                'troniks001',
                'troniks001',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '74098042',
                'nilxanar',
                'nilxanar',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '752965577',
                'touhid560220',
                'touhid560220',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '757817407',
                'playich17',
                'Playich17',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/fea15555-9b57-4906-ae33-7d3901f4906a-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '76541462',
                'jaekso',
                'JAEKSO',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/ead5c8b2-a4c9-4724-b1dd-9f00b46cbd3d-profile_image-300x300.png',
                '#D2691E',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '770827234',
                'sasamber485',
                'sasamber485',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '773094605',
                'deepdoter',
                'deepdoter',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '785975641',
                'rxdcodx',
                'RXDCODX',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '816203487',
                'lolifox2010',
                'lolifox2010',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/dbdc9198-def8-11e9-8681-784f43822e80-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '816842013',
                'trevsad',
                'trevsad',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '82141624',
                'kirillo_id',
                'kirillo_id',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '844276803',
                'mitsuri_2228',
                'mitsuri_2228',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '850127005',
                'dermin_87',
                'dermin_87',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '855554663',
                'batinok34087',
                'batinok34087',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '87591758',
                'memnii',
                'memnii',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '877226161',
                'mon_evidence',
                'mon_evidence',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/786ecd1b-9e2b-4d2c-a6c4-111091508b2b-profile_image-300x300.png',
                '#FF0000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '888775942',
                'kotisabot',
                'KotisaBot',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/ee4917c0-47b0-4952-81e4-eb6f3a324d31-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '895848145',
                'exotic1313',
                'exotic1313',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '89850477',
                'chongpongjong',
                'chongpongjong',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '904133003',
                'volchracz',
                'volchracz',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/e6f7b926-e329-4ca2-94b6-7fb2c46e03d9-profile_image-300x300.png',
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '904364688',
                'miyukaiter',
                'MiyuKaiter',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/2206c856-1aa7-4e8f-9e37-119864e5a381-profile_image-300x300.png',
                '#8A2BE2',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '908879236',
                'prlnce_of_darkness',
                'prlnce_of_darkness',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '92457175',
                'catalina_wolf',
                'catalina_wolf',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '936548395',
                'crazy_rpgrussia021',
                'Crazy_RPGRussia021',
                'https://static-cdn.jtvnw.net/user-default-pictures-uv/998f01ae-def8-11e9-b95c-784f43822e80-profile_image-300x300.png',
                '#FF69B4',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '939788519',
                'yoyhoy',
                'yoyhoy',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '97178760',
                'walk_in_shadow',
                'Walk_In_Shadow',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '99156116',
                'geomethod',
                'geomethod',
                NULL,
                NULL,
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            ),
            (
                '996227069',
                'head755',
                'head755',
                'https://static-cdn.jtvnw.net/jtv_user_pictures/9d5ac565-32d0-48c2-8792-09a23b462802-profile_image-300x300.png',
                '#008000',
                false,
                false,
                NULL,
                '2025-10-21 17:51:50',
                '2025-10-21 17:51:50'
            )
            ON CONFLICT ("TwitchId") DO UPDATE SET
                "UserLogin" = EXCLUDED."UserLogin",
                "DisplayName" = EXCLUDED."DisplayName",
                "ProfileImageUrl" = COALESCE(EXCLUDED."ProfileImageUrl", "TwitchUsers"."ProfileImageUrl"),
                "ChatColor" = COALESCE(EXCLUDED."ChatColor", "TwitchUsers"."ChatColor"),
                "LastUpdated" = EXCLUDED."LastUpdated";
            """
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_HonkaiMarkupUser_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_HelloVideosUsers_TwitchId",
            table: "HelloVideosUsers",
            column: "TwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_CinemaQueue_TwitchUserId",
            table: "CinemaQueue",
            column: "TwitchUserId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue",
            column: "TwitchUserId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_FollowersEntitys_TwitchUsers_UserId",
            table: "FollowersEntitys",
            column: "UserId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_FumoUsers_TwitchUsers_TwitchId",
            table: "FumoUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HelloVideosUsers_TwitchUsers_TwitchId",
            table: "HelloVideosUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_Hosts_TwitchUsers_TwitchId",
            table: "Hosts",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_FollowersEntitys_TwitchUsers_UserId",
            table: "FollowersEntitys"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_FumoUsers_TwitchUsers_TwitchId",
            table: "FumoUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_HelloVideosUsers_TwitchUsers_TwitchId",
            table: "HelloVideosUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser"
        );

        migrationBuilder.DropForeignKey(name: "FK_Hosts_TwitchUsers_TwitchId", table: "Hosts");

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees"
        );

        migrationBuilder.DropTable(name: "TwitchUsers");

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropIndex(name: "IX_HonkaiMarkupUser_TwitchId", table: "HonkaiMarkupUser");

        migrationBuilder.DropIndex(name: "IX_HelloVideosUsers_TwitchId", table: "HelloVideosUsers");

        migrationBuilder.DropIndex(name: "IX_CinemaQueue_TwitchUserId", table: "CinemaQueue");

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "WaifuRollGuarantees",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "TwitchLeaderboardUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "TwitchLeaderboardUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Hosts",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "Hosts",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HonkaiMarkupUser",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HelloVideosUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "HelloVideosUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FumoUsers",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "FumoUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserName",
            table: "FollowersEntitys",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserLogin",
            table: "FollowersEntitys",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "ProfileImageUrl",
            table: "FollowersEntitys",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FollowersEntitys",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "ChatColor",
            table: "FollowersEntitys",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "FollowersEntitys",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "CD",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "AutoHello",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );
    }
}
