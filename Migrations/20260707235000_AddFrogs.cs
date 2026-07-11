using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddFrogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Frogs",
            columns: table => new
            {
                Pid = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CommonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ScientificName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RussianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ThumbnailUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Habits = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                WhenAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastOrder = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                OrderCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Frogs", x => x.Pid);
            });

        migrationBuilder.InsertData(
            table: "Frogs",
            columns: new[] { "Pid", "CommonName", "ScientificName", "Family", "RussianName", "ThumbnailUrl", "Size", "Status", "Category", "Habits", "WhenAdded", "LastOrder", "OrderCount" },
            values: new object[,]
            {
                { 1, "Arnhem Toadlet", "Uperoleia ?", "Myobatrachidae", "Песчаная бородавница", "/frogs/1_1.jpg", "20mm", null, "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 2, "Blacksoil Toadlet", "Uperoleia trachyderma", "Myobatrachidae", "Толстокожая бородавница", "/frogs/2_1.jpg", "25mm", "Least Concern", "Native", "Nocturnal. Call is a  harsh  \"creak\"", DateTime.MinValue, DateTime.MinValue, 0 },
                { 3, "Carpenter Frog, Woodworker Frog", "Limnodynastes lignarius", "Limnodynastidae", "Жаба-плотник", "/frogs/3_2.jpg", "50mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 4, "Centralian Tree Frog", "Litoria gilleni", "Hylidae", "Литория Гиллена", "/frogs/4_1.jpg", null, "Least Concern", "Native", "Nocturnal. Usually found on rock surfaces enclosing permanent streams and waterholes in the mountains throughout its range.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 5, "Copland's Rock Frog, Common Rock Frog, Sandstone Frog", "Litoria coplandi", "Hylidae", "Каменистая литория", "/frogs/5_1.jpg", "40mm", "Least Concern", "Native", "Nocturnal. Shelters by day and emerges at dusk to feed.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 6, "Dahl's Aquatic Frog", "Litoria dahlii", "Hylidae", "Равнинная литория", "/frogs/6_1.jpg", "70mm", "Least Concern", "Native", "Active both day & night. Known to feed on other frog species. Juveniles are known to migrate in very large numbers across the wetlands in the early wet season.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 7, "Daly Waters Frog", "Cyclorana maculosa", "Hylidae", "Пятнистая лопатница", "/frogs/7_5.jpg", "50mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 8, "Desert Spadefoot Toad", "Notaden nichollsi", "Limnodynastidae", "Голубиная пустынница", "/frogs/8_2.jpg", "65mm", "Least Concern", "Native", "Nocturnal. Burrowing frog.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 9, "Hidden-ear Frog", "Cyclorana cryptotis", "Hylidae", "Округлая лопатница", "/frogs/9_5.jpg", "40mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 10, "Knife-footed Frog", "Cyclorana cultripes", "Hylidae", "Блеющая лопатница", "/frogs/10_2.jpg", "50mm", "Least Concern", "Native", "Nocturnal. A burrowing frog which is usually found only when it emerges to feed, and to breed in temporary ponds, clay-pans and creeks after rain", DateTime.MinValue, DateTime.MinValue, 0 },
                { 11, "Main's Frog", "Cyclorana maini", "Hylidae", "Лопатница Мэна", "/frogs/11_4.jpg", "45mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 13, "Remote Froglet", "Crinia remota", "Myobatrachidae", "Пискливая криния", "/frogs/13_1.jpg", "Less than 20mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 14, "Shoemaker Frog", "Neobatrachus sutor", "Limnodynastidae", "Лягушка-сапожник", "/frogs/14_2.jpg", "40mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 15, "Spencer's Frog, Spencer's Burrowing Frog", "Platyplectrum spenceri", "Limnodynastidae", "Широкоголовая литория", "/frogs/15_2.jpg", "50mm", "Least Concern", "Native", "Nocturnal. Burrowing frog", DateTime.MinValue, DateTime.MinValue, 0 },
                { 16, "Spotted Grass Frog, Spotted Marsh Frog", "Limnodynastes tasmaniensis", "Limnodynastidae", "Лягушковидная жаба", "/frogs/16_5.jpg", "40 mm", "Least Concern", "Native", "Nocturnal. Usually shelters under logs and stones on the edges of both permanent and temporary swamps, lagoons and creeks", DateTime.MinValue, DateTime.MinValue, 0 },
                { 17, "Stonemason Toadlet", "Uperoleia lithomoda", "Myobatrachidae", "Бородавница-каменотёс", "/frogs/17_3.jpg", "25mm", "Least Concern", "Native", "Nocturnal. Call is an explosive \"tick\"", DateTime.MinValue, DateTime.MinValue, 0 },
                { 18, "Tanami Toadlet", "Uperoleia micromeles", "Myobatrachidae", "Пустынная бородавница", "/frogs/18_1.jpg", "25mm", "Least Concern", "Native", "Nocturnal.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 19, "Trilling Frog, Sudell's Frog", "Neobatrachus sudellae (centralis)", "Limnodynastidae", "Лягушка Суделлы", "/frogs/19_3.jpg", "55mm", "Least Concern", "Native", "Nocturnal. A burrowing species found breeding in clay-pans after summer and autumn rains. Call a prolonged high pitched trill", DateTime.MinValue, DateTime.MinValue, 0 },
                { 20, "Wailing Frog", "Cyclorana vagita", "Hylidae", "Плачущая лопатница", "/frogs/20_2.jpg", null, "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 21, "Water-holding frog", "Cyclorana platycephala", "Hylidae", "Плоскоголовая лопатница", "/frogs/21_4.jpg", "60mm", "Least Concern", "Native", "Nocturnal. A burrowing frog appears above ground only after rain.  During the dry period burrows deep into the soil where it makes a cocoon-like chamber, with an impervious lining, which, together with the frog's bladder, is filled with water.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 23, "Bilingual Froglet, Ratchet Frog", "Crinia bilingua", "Myobatrachidae", "Мелодичная криния", "/frogs/23_1.jpg", "20mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 25, "Desert Froglet", "Crinia deserticola", "Myobatrachidae", "Пустынная криния", "/frogs/25_1.jpg", "Less than 20mm", "Least Concern", "Native", "Nocturnal. Seen after rain", DateTime.MinValue, DateTime.MinValue, 0 },
                { 26, "Flat-headed Frog", "Limnodynastes depressus", "Limnodynastidae", "Приплюснутая лягушка", "/frogs/26_2.jpg", null, "Least Concern", "native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 27, "Floodplain Toadlet", "Uperoleia inundata", "Myobatrachidae", "Пойменная бородавница", "/frogs/27_2.jpg", "25mm", "Least Concern", "Native", "Nocturnal. Burrowing frog - call is a short \"rasp\"", DateTime.MinValue, DateTime.MinValue, 0 },
                { 28, "Giant Frog", "Cyclorana australis", "Hylidae", "Большеголовая лопатница", "/frogs/28_1.jpg", "100mm", "Least Concern", "Native", "Nocturnal. A burrowing frog usually seen above ground only after rain.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 29, "Northern Spadefoot Toad, Golfball Frog", "Notaden melanoscaphus", "Limnodynastidae", "Термитная пустынница", "/frogs/29_2.jpg", null, "Least Concern", "Native", "Nocturnal. Found above the ground only after rain, or on humid evenings. Call - a series of owl like 'whoo-oos'. The breeding colony moves slightly each rainy night & concentrates on a different area of the swamp, probably to increase the chances of survival of the eggs. A protracted monsoon will mean a better egg coverage of the breeding habitat.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 30, "Green Tree-frog", "Litoria caerulea", "Hylidae", "Коралловопалая литория", "/frogs/30_1.jpg", "100mm", "Least Concern", "Native", "Nocturnal. Call - a deep \"wark wark wark\"", DateTime.MinValue, DateTime.MinValue, 0 },
                { 31, "Jabiru Toadlet", "Uperoleia arenicola", "Myobatrachidae", "Песчаная бородавница", "/frogs/31_2.jpg", "20mm", "Least Concern", null, "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 34, "Marbled Frog", "Limnodynastes convexiusculus", "Limnodynastidae", "Выпуклая лягушка", "/frogs/34_1.jpg", "50mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 35, "Masked Rock-frog, Masked Cave-Frog", "Litoria personata", "Hylidae", "Маскированная литория", "/frogs/35_2.jpg", "30mm", "Least Concern", "Native", "Nocturnal. Found during the wet around sandstone rock faces and crevices near permanent and temporary water in stony hills and scarps. During the dry found among sedges and shrubs bordering small permanent streams.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 36, "Northern Dwarf Tree-frog", "Litoria bicolor", "Hylidae", "Двухцветная литория", "/frogs/36_2.jpg", "85mm", "Least Concern", "Native", "Nocturnal/ Diurnal. During the dry season isolated specimens have been found in pandanus palms", DateTime.MinValue, DateTime.MinValue, 0 },
                { 37, "Northern Territory Frog", "Austrochaperina adelphe", "Microhylidae", "Австралохаперина", "/frogs/37_1.jpg", "18mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 38, "Ornate Burrowing Frog", "Platyplectrum (Limnodynastes) ornatum", "Limnodynastidae", "Украшенная лягушка", "/frogs/38_1.jpg", "40mm", "Least Concern", "Native", "Nocturnal. Burrowing species. Usually active after rain or on warm humid nights", DateTime.MinValue, DateTime.MinValue, 0 },
                { 39, "Pale Frog", "Litoria pallida", "Hylidae", "Клиноголовая литория", "/frogs/39_2.jpg", "30mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 40, "Peters' Frog", "Litoria inermis", "Hylidae", "Бородавчатая литория", "/frogs/40_2.jpg", "35mm", "Least Concern", "Native", "Nocturnal. Usually found on warm humid nights especially after rain", DateTime.MinValue, DateTime.MinValue, 0 },
                { 42, "Rocket Frog", "Litoria nasuta", "Hylidae", "Остроносая литория", "/frogs/42_2.jpg", "50mm", "Least Concern", "Native", "Nocturnal. Terrestrial.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 43, "Rockhole Frog", "Litoria meiriana", "Hylidae", "Литория Мейра", "/frogs/43_2.jpg", "20mm", "Least Concern", "Native", "Diurnal. Most active by day, easy to capture at night when it congregates on floating vegetation or at the  water's edge", DateTime.MinValue, DateTime.MinValue, 0 },
                { 44, "Roth's Tree-frog", "Litoria rothii", "Hylidae", "Литория Рота", "/frogs/44_2.jpg", "40mm", "Least Concern", "Native", "Nocturnal. Similar in looks and habits to perroni", DateTime.MinValue, DateTime.MinValue, 0 },
                { 45, "Magnificent Tree-frog", "Litoria splendida", "Hylidae", "Очаровательная литория", "/frogs/45_2.jpg", "90mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 46, "Tornier's Frog", "Litoria tornieri", "Hylidae", "Травяная литория", "/frogs/46_1.jpg", "35mm", "Least Concern", "Native", "Nocturnal", DateTime.MinValue, DateTime.MinValue, 0 },
                { 47, "Water Frog, Wood Frog", "Hylarana (Sylvirana) daemeli", "Ranidae", "Австралийская лягушка", "/frogs/47_5.jpg", null, "Near Threatened", "Native", "Remains active during the dry season and spend a lot of its time in the water.", DateTime.MinValue, DateTime.MinValue, 0 },
                { 48, "Wotjulum Frog", "Litoria wotjulumensis", "Hylidae", "Разноголосая литория", "/frogs/48_4.jpg", "75mm", "Least Concern", "Native", "Nocturnal. Ground dwelling frog", DateTime.MinValue, DateTime.MinValue, 0 },
                { 49, "Striped Burrowing Frog", "Cyclorana alboguttata", "Hylidae", "Роющая лопатница", "/frogs/49_2.jpg", null, "Least Concern", "Native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 103, "Howard Springs Toadlet, Howard River Toadlet, Davies's Toadlet", "Uperoleia daviesae", "Myobatrachidae", "Бородавница Дэвиса", "/frogs/103_2.jpg", "17-22 mm", "Vulnerable", null, "Often found with U. inundata and U. lithomoda", DateTime.MinValue, DateTime.MinValue, 0 },
                { 104, "Cave-dwelling frog", "Litoria cavernicola", "Hylidae", "Пещерная литория", "/frogs/104_1.jpg", "4.4-5.7 cm", "Data Deficient", "native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 106, "Derby Toadlet", "Uperoleia aspera", "Myobatrachidae", "Беззубая бородавница", "/frogs/106_2.jpg", "2.4 -3.4 cm", "Least Concern", "native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 109, "Small Toadlet", "Uperoleia minima", "Myobatrachidae", "Карликовая бородавница", "/frogs/109_2.jpg", null, "Least Concern", "Native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 110, "Mjoberg's Toadlet", "Uperoleia mjobergii", "Myobatrachidae", "Зубатая бородавница", "/frogs/110_1.jpg", null, "Least Concern", "Native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 111, "Mole Toadlet", "Uperoleia talpa", "Myobatrachidae", "Кротовая бородавница", "/frogs/111_2.jpg", "2.6-3.8 cm", "Least Concern", "Native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 141, "Elcho Toadlet", "Uperoleia Sp??", "Myobatrachidae", "Элчоская бородавница", "/frogs/141_1.jpg", null, "Unknown", "Native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 142, "Galiwinku Toadlet", "Uperoleia Sp???", "Myobatrachidae", "Галивинкуская бородавница", "/frogs/142_1.jpg", null, "Unknown", "Native", null, DateTime.MinValue, DateTime.MinValue, 0 },
                { 161, "Northern Toadlet", "Uperoleia borealis", "Myobatrachidae", "Травянистая бородавница", "/frogs/161_1.jpg", null, "Least Concern", null, "Very little is known of the life cycle of these frogs. They appear to occupy a range of habitats including grassland, river corridores, high sandstone ridges & eucalypt woodland. Roadside quarries are a favorite breeding site. When undisturbed, they walk on their toes rather than hopping.", DateTime.MinValue, DateTime.MinValue, 0 }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Frogs");
    }
}
