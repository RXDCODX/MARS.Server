using MARS.Server.Services.TabletopGames.Entitys.Abstractions;

namespace MARS.Server.Services.TabletopGames.Entitys;

public class Checker : Figure
{
    public required bool IsQueen { get; set; } = false;
}
