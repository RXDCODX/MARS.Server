using MARS.Server.Services.TabletopGames.Entitys;
using Telegramus.Migrations;

namespace MARS.Server.Services.TabletopGames.Checkers;

public class CheckersGame
{
    private readonly GameBoard _gameBoard = GameBoard.CreateDefaultBoard();
    public List<Cell[]> Logs = [];
}
