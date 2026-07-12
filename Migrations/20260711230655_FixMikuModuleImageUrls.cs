using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class FixMikuModuleImageUrls : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fix URLs where title contains / : * characters that differ from actual filenames in wwwroot/miku_monday/
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Animal Fortune-telling (Outfit)_Miku.jpg' WHERE \"PageId\" = 6791"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Clover♣Club (Outfit)_Miku.jpg' WHERE \"PageId\" = 6807"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/DE_MONSTAR.jpg' WHERE \"PageId\" = 4030"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Electric Angel (Outfit)_Miku.jpg' WHERE \"PageId\" = 7566"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Hatsune Miku_ Halloween.jpg' WHERE \"PageId\" = 7575"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Hello_How are you_ (Outfit).jpg' WHERE \"PageId\" = 6800"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/KONEKO NO PAYAPAYA (Outfit)_Miku.jpg' WHERE \"PageId\" = 7552"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Matryoshka (Outfit)_Miku.jpg' WHERE \"PageId\" = 6802"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/P4_ Dancing All Night Miku.png' WHERE \"PageId\" = 3707"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/PIANO_GIRL (Module).png' WHERE \"PageId\" = 3261"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Reverse rainbow (Outfit)_Miku.jpg' WHERE \"PageId\" = 6783"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Romeo and Cinderella (Outfit)_Miku.jpg' WHERE \"PageId\" = 7558"
        );
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"ThumbnailUrl\" = '/miku_monday/Sugar_Soldier Uniform.jpg' WHERE \"PageId\" = 7580"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
