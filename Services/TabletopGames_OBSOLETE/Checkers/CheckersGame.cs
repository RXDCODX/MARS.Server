using System;
using System.Collections.Generic;
using MARS.Server.Services.TabletopGames_OBSOLETE.Entitys;
using MARS.Server.Services.TabletopGames_OBSOLETE.Entitys.Enums;

namespace MARS.Server.Services.TabletopGames_OBSOLETE.Checkers;

/// <summary>
/// Manages the logic and state of a checkers game.
/// </summary>
public class CheckersGame
{
    private static GameBoard GameBoard => GameBoard.CreateDefaultBoard();
    public List<Cell[]> Logs = [];

    /// <summary>
    /// Current player's turn (White or Black).
    /// </summary>
    public Color CurrentPlayer { get; private set; } = Color.White;

    /// <summary>
    /// Game status.
    /// </summary>
    public GameStatus Status { get; private set; } = GameStatus.NotStarted;

    /// <summary>
    /// Winner of the game, if any.
    /// </summary>
    public Color? Winner { get; private set; }

    /// <summary>
    /// Gets the current game board.
    /// </summary>
    public GameBoard Board => GameBoard;

    /// <summary>
    /// Initializes a new game of checkers.
    /// </summary>
    public void StartNewGame()
    {
        GameBoard.InitializeCheckers();
        CurrentPlayer = Color.White;
        Status = GameStatus.InProgress;
        Winner = null;
        Logs.Clear();
    }

    /// <summary>
    /// Attempts to make a move from one position to another.
    /// </summary>
    /// <param name="fromX">Starting X coordinate.</param>
    /// <param name="fromY">Starting Y coordinate.</param>
    /// <param name="toX">Target X coordinate.</param>
    /// <param name="toY">Target Y coordinate.</param>
    /// <returns>True if the move was successful.</returns>
    public bool MakeMove(char fromX, ushort fromY, char toX, ushort toY)
    {
        if (Status != GameStatus.InProgress)
        {
            return false;
        }

        var fromCell = GameBoard.GetCell(fromX, fromY);
        var toCell = GameBoard.GetCell(toX, toY);

        if (fromCell?.Checker == null || toCell == null)
        {
            return false;
        }

        if (!fromCell.Checker.Color.Equals(CurrentPlayer))
        {
            return false;
        }

        // Check if it's a capture move
        var isCapture = Math.Abs(toX - fromX) == 2 && Math.Abs(toY - fromY) == 2;

        return isCapture
            ? MakeCaptureMove(fromX, fromY, toX, toY)
            : MakeRegularMove(fromX, fromY, toX, toY);
    }

    /// <summary>
    /// Makes a regular move (one square diagonal).
    /// </summary>
    private bool MakeRegularMove(char fromX, ushort fromY, char toX, ushort toY)
    {
        var fromCell = GameBoard.GetCell(fromX, fromY);
        var toCell = GameBoard.GetCell(toX, toY);

        if (toCell?.IsValidCheckerPosition() != true || toCell.IsBusy)
        {
            return false;
        }

        var validMoves = GameBoard.GetValidMoves(fromX, fromY);
        if (!validMoves.Contains((toX, toY)))
        {
            return false;
        }

        // Check if captures are available - if so, regular moves are not allowed
        if (HasAnyCaptures(CurrentPlayer))
        {
            return false;
        }

        // Совершить ход
        if (fromCell?.Checker == null)
        {
            // Если шашка отсутствует, ход невозможен
            return false;
        }
        var checker = fromCell.Checker;
        fromCell.RemoveChecker();

        checker.XCoordinate = toX;
        checker.YCoordinate = toY;
        toCell.PlaceChecker(checker);

        // Check for promotion
        if (checker.CanBecomeQueen())
        {
            checker.PromoteToQueen();
        }

        // Save move to logs
        SaveMoveToLogs(fromX, fromY, toX, toY);

        // Switch players
        SwitchPlayer();

        // Check for game end
        CheckGameEnd();

        return true;
    }

    /// <summary>
    /// Makes a capture move (jumping over an opponent's piece).
    /// </summary>
    private bool MakeCaptureMove(char fromX, ushort fromY, char toX, ushort toY)
    {
        var fromCell = GameBoard.GetCell(fromX, fromY);
        var toCell = GameBoard.GetCell(toX, toY);

        if (toCell?.IsValidCheckerPosition() != true || toCell.IsBusy)
        {
            return false;
        }

        var validCaptures = GameBoard.GetValidCaptures(fromX, fromY);
        if (!validCaptures.Contains((toX, toY)))
        {
            return false;
        }

        // Calculate the position of the captured piece
        var capturedX = (char)((fromX + toX) / 2);
        var capturedY = (ushort)((fromY + toY) / 2);
        var capturedCell = GameBoard.GetCell(capturedX, capturedY);

        if (capturedCell?.Checker == null || capturedCell.Checker.Color.Equals(CurrentPlayer))
        {
            return false;
        }

        // Make the capture move
        if (fromCell == null || capturedCell == null || fromCell.Checker == null)
        {
            return false;
        }
        var checker = fromCell.Checker;
        fromCell.RemoveChecker();
        capturedCell.RemoveChecker();

        checker.XCoordinate = toX;
        checker.YCoordinate = toY;
        toCell.PlaceChecker(checker);

        // Check for promotion
        if (checker.CanBecomeQueen())
        {
            checker.PromoteToQueen();
        }

        // Save move to logs
        SaveMoveToLogs(fromX, fromY, toX, toY);

        // Check for additional captures
        if (!HasAdditionalCaptures(toX, toY))
        {
            SwitchPlayer();
        }

        // Check for game end
        CheckGameEnd();

        return true;
    }

    /// <summary>
    /// Checks if the current player has any capture moves available.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player has capture moves.</returns>
    private static bool HasAnyCaptures(Color player)
    {
        for (var x = 'a'; x <= 'h'; x++)
        {
            for (ushort y = 1; y <= 8; y++)
            {
                var cell = GameBoard.GetCell(x, y);
                if (cell?.Checker != null && cell.Checker.Color.Equals(player))
                {
                    var captures = GameBoard.GetValidCaptures(x, y);
                    if (captures.Count > 0)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a checker at the specified position has additional capture moves.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>True if additional captures are available.</returns>
    private static bool HasAdditionalCaptures(char x, ushort y)
    {
        var captures = GameBoard.GetValidCaptures(x, y);
        return captures.Count > 0;
    }

    /// <summary>
    /// Switches the current player.
    /// </summary>
    private void SwitchPlayer()
    {
        CurrentPlayer = CurrentPlayer == Color.White ? Color.Black : Color.White;
    }

    /// <summary>
    /// Checks if the game has ended and updates the status.
    /// </summary>
    private void CheckGameEnd()
    {
        var whitePieces = CountPieces(Color.White);
        var blackPieces = CountPieces(Color.Black);

        if (whitePieces == 0)
        {
            Status = GameStatus.Finished;
            Winner = Color.Black;
        }
        else if (blackPieces == 0)
        {
            Status = GameStatus.Finished;
            Winner = Color.White;
        }
        else if (!HasAnyValidMoves(CurrentPlayer))
        {
            Status = GameStatus.Finished;
            Winner = CurrentPlayer == Color.White ? Color.Black : Color.White;
        }
    }

    /// <summary>
    /// Counts the number of pieces for a given player.
    /// </summary>
    /// <param name="player">The player to count pieces for.</param>
    /// <returns>The number of pieces.</returns>
    private static int CountPieces(Color player)
    {
        var count = 0;
        for (var x = 'a'; x <= 'h'; x++)
        {
            for (ushort y = 1; y <= 8; y++)
            {
                var cell = GameBoard.GetCell(x, y);
                if (cell?.Checker != null && cell.Checker.Color.Equals(player))
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Checks if a player has any valid moves.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player has valid moves.</returns>
    private static bool HasAnyValidMoves(Color player)
    {
        for (var x = 'a'; x <= 'h'; x++)
        {
            for (ushort y = 1; y <= 8; y++)
            {
                var cell = GameBoard.GetCell(x, y);
                if (cell?.Checker != null && cell.Checker.Color.Equals(player))
                {
                    var moves = GameBoard.GetValidMoves(x, y);
                    var captures = GameBoard.GetValidCaptures(x, y);
                    if (moves.Count > 0 || captures.Count > 0)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Saves a move to the game logs.
    /// </summary>
    private void SaveMoveToLogs(char fromX, ushort fromY, char toX, ushort toY)
    {
        var fromCell = GameBoard.GetCell(fromX, fromY);
        var toCell = GameBoard.GetCell(toX, toY);
        if (fromCell != null && toCell != null)
        {
            var move = new Cell[] { fromCell, toCell };
            Logs.Add(move);
        }
    }

    /// <summary>
    /// Gets all valid moves for a checker at the specified position.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>List of valid moves.</returns>
    public List<(char x, ushort y)> GetValidMoves(char x, ushort y)
    {
        var cell = GameBoard.GetCell(x, y);
        if (cell?.Checker == null || !cell.Checker.Color.Equals(CurrentPlayer))
        {
            return [];
        }

        var moves = new List<(char x, ushort y)>();
        var captures = GameBoard.GetValidCaptures(x, y);
        var regularMoves = GameBoard.GetValidMoves(x, y);

        // If captures are available, only return captures
        moves.AddRange(captures.Count > 0 ? captures : regularMoves);

        return moves;
    }
}
