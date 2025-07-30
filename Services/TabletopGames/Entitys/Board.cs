namespace MARS.Server.Services.TabletopGames.Entitys;

/// <summary>
/// Represents the game board for a tabletop game, such as checkers.
/// </summary>
public sealed class GameBoard
{
    private GameBoard() { }

    public const short Width = 8;
    public const short Height = 8;
    public Cell[,] Board = new Cell[Width, Height];

    public static GameBoard CreateDefaultBoard()
    {
        var board = new GameBoard();
        var isWhite = false;

        // Инициализация каждой клетки на доске
        for (var x = 'a' ; x <= 'h' ; x++)
        {
            for (ushort y = 1 ; y <= Height ; y++)
            {
                board.Board[x - 'a', y - 1] = new Cell
                {
                    XCoordinate = x,
                    YCoordinate = y,
                    IsBusy = false, // По умолчанию клетка не занята
                    Color = isWhite ? Color.White : Color.Black,
                };

                isWhite = !isWhite;
            }
            isWhite = !isWhite; // Переход на новую строку
        }

        return board;
    }

    /// <summary>
    /// Initializes the board with checkers in their starting positions.
    /// </summary>
    public void InitializeCheckers()
    {
        // Place black checkers on the top three rows
        for (var x = 'a' ; x <= 'h' ; x++)
        {
            for (ushort y = 1 ; y <= 3 ; y++)
            {
                var cell = GetCell(x, y);
                if (cell?.IsValidCheckerPosition() == true)
                {
                    var checker = new Checker
                    {
                        XCoordinate = x,
                        YCoordinate = y,
                        Color = Color.Black,
                        IsQueen = false,
                    };
                    cell.PlaceChecker(checker);
                }
            }
        }

        // Place white checkers on the bottom three rows
        for (var x = 'a' ; x <= 'h' ; x++)
        {
            for (ushort y = 6 ; y <= 8 ; y++)
            {
                var cell = GetCell(x, y);
                if (cell?.IsValidCheckerPosition() == true)
                {
                    var checker = new Checker
                    {
                        XCoordinate = x,
                        YCoordinate = y,
                        Color = Color.White,
                        IsQueen = false,
                    };
                    cell.PlaceChecker(checker);
                }
            }
        }
    }

    /// <summary>
    /// Gets a cell at the specified coordinates.
    /// </summary>
    /// <param name="x">X coordinate (a-h).</param>
    /// <param name="y">Y coordinate (1-8).</param>
    /// <returns>The cell at the specified coordinates, or null if out of bounds.</returns>
    public Cell? GetCell(char x, ushort y)
    {
        return x < 'a' || x > 'h' || y < 1 || y > 8 ? null : Board[x - 'a', y - 1];
    }

    /// <summary>
    /// Checks if the specified coordinates are within the board boundaries.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>True if the coordinates are valid.</returns>
    public static bool IsValidPosition(char x, ushort y)
    {
        return x >= 'a' && x <= 'h' && y >= 1 && y <= 8;
    }

    /// <summary>
    /// Gets all valid moves for a checker at the specified position.
    /// </summary>
    /// <param name="x">X coordinate of the checker.</param>
    /// <param name="y">Y coordinate of the checker.</param>
    /// <returns>List of valid move coordinates.</returns>
    public List<(char x, ushort y)> GetValidMoves(char x, ushort y)
    {
        var cell = GetCell(x, y);
        if (cell?.Checker == null)
        {
            return [];
        }

        var moves = new List<(char x, ushort y)>();
        var directions = cell.Checker.GetValidMoveDirections();

        foreach (var (deltaX, deltaY) in directions)
        {
            var newX = (char)(x + deltaX);
            var newY = (ushort)(y + deltaY);

            if (IsValidPosition(newX, newY))
            {
                var targetCell = GetCell(newX, newY);
                if (targetCell?.IsValidCheckerPosition() == true && !targetCell.IsBusy)
                {
                    moves.Add((newX, newY));
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Gets all valid capture moves for a checker at the specified position.
    /// </summary>
    /// <param name="x">X coordinate of the checker.</param>
    /// <param name="y">Y coordinate of the checker.</param>
    /// <returns>List of valid capture move coordinates.</returns>
    public List<(char x, ushort y)> GetValidCaptures(char x, ushort y)
    {
        var cell = GetCell(x, y);
        if (cell?.Checker == null)
        {
            return [];
        }

        var captures = new List<(char x, ushort y)>();
        var directions = cell.Checker.GetValidMoveDirections();

        foreach (var (deltaX, deltaY) in directions)
        {
            var jumpX = (char)(x + deltaX);
            var jumpY = (ushort)(y + deltaY);
            var landX = (char)(x + (deltaX * 2));
            var landY = (ushort)(y + (deltaY * 2));

            if (IsValidPosition(jumpX, jumpY) && IsValidPosition(landX, landY))
            {
                var jumpCell = GetCell(jumpX, jumpY);
                var landCell = GetCell(landX, landY);

                if (
                    jumpCell?.IsBusy == true
                    && jumpCell.Checker?.Color != cell.Checker.Color
                    && landCell?.IsValidCheckerPosition() == true
                    && !landCell.IsBusy
                )
                {
                    captures.Add((landX, landY));
                }
            }
        }

        return captures;
    }
}
