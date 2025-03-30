namespace MARS.Server.Services.TabletopGames.Entitys.Abstractions;

public abstract class Figure
{
    public required char XCoordinate { get; set; }
    public required ushort YCoordinate { get; set; }
}
