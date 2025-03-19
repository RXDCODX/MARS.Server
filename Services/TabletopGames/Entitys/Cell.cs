namespace MARS.Server.Services.TabletopGames.Entitys;

public class Cell
{
    public required char XCoordinate { get; set; }

    /// <summary>
    ///
    /// </summary>
    public required ushort YCoordinate { get; set; }

    /// <summary>
    /// Занята ли ячейка
    /// </summary>
    public bool IsBusy { get; set; }

    /// <summary>
    /// Стоит ли на этой ячейке дамка
    /// </summary>
    public bool IsKing { get; set; }

    public Color Color { get; set; } = Color.White;
}
