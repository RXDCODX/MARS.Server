namespace MARS.Server.Services.TabletopGames.Entitys;

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
        for (var x = 'a'; x <= 'h'; x++)
        {
            for (ushort y = 1; y <= Height; y++)
            {
                board.Board[x - 'a', y - 1] = new()
                {
                    XCoordinate = x,
                    YCoordinate = y,
                    IsBusy = false, // По умолчанию клетка не занята
                    Color = isWhite ? Color.White : Color.Black,
                };

                isWhite = !isWhite;
            }
        }

        return board;
    }
}
