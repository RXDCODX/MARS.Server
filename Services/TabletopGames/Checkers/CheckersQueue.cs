namespace MARS.Server.Services.TabletopGames.Checkers;

/// <summary>
/// Manages the queue of players waiting to join a checkers game.
/// </summary>
public class CheckersQueue
{
    private readonly Queue<string> _playerQueue = new();
    private readonly Dictionary<string, DateTime> _playerJoinTimes = [];
    private readonly Lock _lockObject = new();

    /// <summary>
    /// Gets the current number of players in the queue.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lockObject)
            {
                return _playerQueue.Count;
            }
        }
    }

    /// <summary>
    /// Adds a player to the queue.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>True if the player was successfully added to the queue.</returns>
    public bool AddPlayer(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        lock (_lockObject)
        {
            if (_playerQueue.Contains(playerId))
            {
                return false;
            }

            _playerQueue.Enqueue(playerId);
            _playerJoinTimes[playerId] = DateTime.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Removes a player from the queue.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>True if the player was successfully removed from the queue.</returns>
    public bool RemovePlayer(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        lock (_lockObject)
        {
            if (!_playerQueue.Contains(playerId))
            {
                return false;
            }

            var tempQueue = new Queue<string>();
            while (_playerQueue.Count > 0)
            {
                var currentPlayer = _playerQueue.Dequeue();
                if (currentPlayer != playerId)
                {
                    tempQueue.Enqueue(currentPlayer);
                }
            }

            while (tempQueue.Count > 0)
            {
                _playerQueue.Enqueue(tempQueue.Dequeue());
            }

            _playerJoinTimes.Remove(playerId);
            return true;
        }
    }

    /// <summary>
    /// Gets the next player from the queue without removing them.
    /// </summary>
    /// <returns>The next player's ID, or null if the queue is empty.</returns>
    public string? PeekNextPlayer()
    {
        lock (_lockObject)
        {
            return _playerQueue.Count > 0 ? _playerQueue.Peek() : null;
        }
    }

    /// <summary>
    /// Gets and removes the next player from the queue.
    /// </summary>
    /// <returns>The next player's ID, or null if the queue is empty.</returns>
    public string? GetNextPlayer()
    {
        lock (_lockObject)
        {
            if (_playerQueue.Count == 0)
            {
                return null;
            }

            var playerId = _playerQueue.Dequeue();
            _playerJoinTimes.Remove(playerId);
            return playerId;
        }
    }

    /// <summary>
    /// Gets the next two players from the queue for a game.
    /// </summary>
    /// <returns>A tuple containing the two player IDs, or null if there aren't enough players.</returns>
    public (string player1, string player2)? GetNextGamePlayers()
    {
        lock (_lockObject)
        {
            if (_playerQueue.Count < 2)
            {
                return null;
            }

            var player1 = _playerQueue.Dequeue();
            var player2 = _playerQueue.Dequeue();

            _playerJoinTimes.Remove(player1);
            _playerJoinTimes.Remove(player2);

            return (player1, player2);
        }
    }

    /// <summary>
    /// Gets the position of a player in the queue.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>The player's position (1-based), or -1 if not found.</returns>
    public int GetPlayerPosition(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return -1;
        }

        lock (_lockObject)
        {
            var position = 1;
            foreach (var player in _playerQueue)
            {
                if (player == playerId)
                {
                    return position;
                }

                position++;
            }
            return -1;
        }
    }

    /// <summary>
    /// Gets the time a player has been waiting in the queue.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>The time span the player has been waiting, or null if not found.</returns>
    public TimeSpan? GetPlayerWaitTime(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return null;
        }

        lock (_lockObject)
        {
            return !_playerJoinTimes.TryGetValue(playerId, out var joinTime)
                ? null
                : DateTime.UtcNow - joinTime;
        }
    }

    /// <summary>
    /// Gets all players currently in the queue.
    /// </summary>
    /// <returns>A list of player IDs in the queue.</returns>
    public List<string> GetAllPlayers()
    {
        lock (_lockObject)
        {
            return [.. _playerQueue];
        }
    }

    /// <summary>
    /// Clears the entire queue.
    /// </summary>
    public void Clear()
    {
        lock (_lockObject)
        {
            _playerQueue.Clear();
            _playerJoinTimes.Clear();
        }
    }

    /// <summary>
    /// Checks if a player is currently in the queue.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>True if the player is in the queue.</returns>
    public bool ContainsPlayer(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        lock (_lockObject)
        {
            return _playerQueue.Contains(playerId);
        }
    }
}
