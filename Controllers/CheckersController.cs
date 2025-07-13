using MARS.Server.Services.TabletopGames.Checkers;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Controller for managing checkers games.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CheckersController : ControllerBase
{
    private readonly CheckersGameManager _gameManager;
    private readonly CheckersQueue _queue;

    public CheckersController()
    {
        _gameManager = new CheckersGameManager();
        _queue = new CheckersQueue();
    }

    /// <summary>
    /// Starts a new checkers game.
    /// </summary>
    /// <returns>Game status information.</returns>
    [HttpPost("start")]
    public IActionResult StartNewGame()
    {
        _gameManager.StartNewGame();

        return Ok(
            new
            {
                _gameManager.Status,
                _gameManager.CurrentPlayer,
                Message = "New game started successfully",
            }
        );
    }

    /// <summary>
    /// Makes a move in the current game.
    /// </summary>
    /// <param name="request">Move request containing coordinates.</param>
    /// <returns>Result of the move attempt.</returns>
    [HttpPost("move")]
    public IActionResult MakeMove([FromBody] MoveRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = _gameManager.MakeMove(request.FromX, request.FromY, request.ToX, request.ToY);

        return !success
            ? BadRequest(
                new
                {
                    Message = "Invalid move",
                    _gameManager.CurrentPlayer,
                    _gameManager.Status,
                }
            )
            : Ok(
                new
                {
                    Success = true,
                    _gameManager.CurrentPlayer,
                    _gameManager.Status,
                    _gameManager.Winner,
                    Message = "Move completed successfully",
                }
            );
    }

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    /// <returns>Current game state information.</returns>
    [HttpGet("state")]
    public IActionResult GetGameState()
    {
        var board = _gameManager.Board;
        var boardState = new List<object>();

        for (var x = 'a'; x <= 'h'; x++)
        {
            for (ushort y = 1; y <= 8; y++)
            {
                var cell = board.GetCell(x, y);
                if (cell != null)
                {
                    boardState.Add(
                        new
                        {
                            X = x,
                            Y = y,
                            cell.IsBusy,
                            cell.IsKing,
                            CellColor = cell.Color,
                            CheckerColor = cell.Checker?.Color,
                            cell.Checker?.IsQueen,
                        }
                    );
                }
            }
        }

        return Ok(
            new
            {
                _gameManager.Status,
                _gameManager.CurrentPlayer,
                _gameManager.Winner,
                Board = boardState,
                MoveCount = _gameManager.Logs.Count,
            }
        );
    }

    /// <summary>
    /// Gets valid moves for a specific position.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>List of valid moves.</returns>
    [HttpGet("moves/{x}/{y}")]
    public IActionResult GetValidMoves(char x, ushort y)
    {
        var moves = _gameManager.GetValidMoves(x, y);

        return Ok(
            new
            {
                Position = new { X = x, Y = y },
                ValidMoves = moves,
                moves.Count,
            }
        );
    }

    /// <summary>
    /// Adds a player to the queue.
    /// </summary>
    /// <param name="request">Player join request.</param>
    /// <returns>Queue status information.</returns>
    [HttpPost("queue/join")]
    public IActionResult JoinQueue([FromBody] JoinQueueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest(new { Message = "Player ID is required" });
        }

        var success = _queue.AddPlayer(request.PlayerId);

        return !success
            ? BadRequest(new { Message = "Player is already in queue" })
            : Ok(
                new
                {
                    Success = true,
                    request.PlayerId,
                    Position = _queue.GetPlayerPosition(request.PlayerId),
                    QueueSize = _queue.Count,
                    Message = "Successfully joined queue",
                }
            );
    }

    /// <summary>
    /// Removes a player from the queue.
    /// </summary>
    /// <param name="request">Player leave request.</param>
    /// <returns>Queue status information.</returns>
    [HttpPost("queue/leave")]
    public IActionResult LeaveQueue([FromBody] LeaveQueueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest(new { Message = "Player ID is required" });
        }

        var success = _queue.RemovePlayer(request.PlayerId);

        return !success
            ? BadRequest(new { Message = "Player is not in queue" })
            : Ok(
                new
                {
                    Success = true,
                    request.PlayerId,
                    QueueSize = _queue.Count,
                    Message = "Successfully left queue",
                }
            );
    }

    /// <summary>
    /// Gets the current queue status.
    /// </summary>
    /// <returns>Queue information.</returns>
    [HttpGet("queue/status")]
    public IActionResult GetQueueStatus()
    {
        var players = _queue.GetAllPlayers();
        var queueInfo = players
            .Select(playerId => new
            {
                PlayerId = playerId,
                Position = _queue.GetPlayerPosition(playerId),
                WaitTime = _queue.GetPlayerWaitTime(playerId),
            })
            .ToList();

        return Ok(
            new
            {
                QueueSize = _queue.Count,
                Players = queueInfo,
                CanStartGame = _queue.Count >= 2,
            }
        );
    }

    /// <summary>
    /// Gets the next two players for a game.
    /// </summary>
    /// <returns>Next game players or null if not enough players.</returns>
    [HttpGet("queue/next-game")]
    public IActionResult GetNextGamePlayers()
    {
        var players = _queue.GetNextGamePlayers();

        return players == null
            ? Ok(new { CanStartGame = false, Message = "Not enough players in queue" })
            : (IActionResult)Ok(
                new
                {
                    CanStartGame = true,
                    Player1 = players.Value.player1,
                    Player2 = players.Value.player2,
                    QueueSize = _queue.Count,
                    Message = "Players ready for game",
                }
            );
    }
}

/// <summary>
/// Request model for making a move.
/// </summary>
public class MoveRequest
{
    public char FromX { get; set; }
    public ushort FromY { get; set; }
    public char ToX { get; set; }
    public ushort ToY { get; set; }
}

/// <summary>
/// Request model for joining the queue.
/// </summary>
public class JoinQueueRequest
{
    public string PlayerId { get; set; } = string.Empty;
}

/// <summary>
/// Request model for leaving the queue.
/// </summary>
public class LeaveQueueRequest
{
    public string PlayerId { get; set; } = string.Empty;
}
