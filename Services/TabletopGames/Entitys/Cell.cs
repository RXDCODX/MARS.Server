namespace MARS.Server.Services.TabletopGames.Entitys;

/// <summary>
/// Represents a single cell on the game board.
/// </summary>
public class Cell
{
    public required char XCoordinate { get; set; }

    /// <summary>
    /// Y coordinate of the cell (1-8).
    /// </summary>
    public required ushort YCoordinate { get; set; }

    /// <summary>
    /// Whether the cell is occupied by a checker.
    /// </summary>
    public bool IsBusy { get; set; }

    /// <summary>
    /// Whether the cell contains a king (queen) checker.
    /// </summary>
    public bool IsKing { get; set; }

    /// <summary>
    /// The color of the cell (for board pattern).
    /// </summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// The checker piece occupying this cell, if any.
    /// </summary>
    public Checker? Checker { get; set; }

    /// <summary>
    /// Checks if the cell is a valid position for placing checkers (dark squares only).
    /// </summary>
    /// <returns>True if the cell is a valid position for checkers.</returns>
    public bool IsValidCheckerPosition()
    {
        return Color == Color.Black;
    }

    /// <summary>
    /// Places a checker on this cell.
    /// </summary>
    /// <param name="checker">The checker to place.</param>
    public void PlaceChecker(Checker checker)
    {
        Checker = checker;
        IsBusy = true;
        IsKing = checker.IsQueen;
    }

    /// <summary>
    /// Removes the checker from this cell.
    /// </summary>
    public void RemoveChecker()
    {
        Checker = null;
        IsBusy = false;
        IsKing = false;
    }
}
