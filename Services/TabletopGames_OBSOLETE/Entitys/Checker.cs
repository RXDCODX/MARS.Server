using MARS.Server.Services.TabletopGames_OBSOLETE.Entitys.Abstractions;

namespace MARS.Server.Services.TabletopGames_OBSOLETE.Entitys;

/// <summary>
/// Represents a checker piece in a tabletop checkers game.
/// </summary>
public class Checker : Figure
{
    public required bool IsQueen { get; set; } = false;
    public required Color Color { get; set; }

    /// <summary>
    /// Checks if the checker can become a queen based on its position.
    /// </summary>
    /// <returns>True if the checker should become a queen.</returns>
    public bool CanBecomeQueen()
    {
        return !IsQueen && (Color == Color.White ? YCoordinate == 8 : YCoordinate == 1);
    }

    /// <summary>
    /// Promotes the checker to a queen.
    /// </summary>
    public void PromoteToQueen()
    {
        if (CanBecomeQueen())
        {
            IsQueen = true;
        }
    }

    /// <summary>
    /// Gets the valid move directions for this checker.
    /// </summary>
    /// <returns>List of valid move directions.</returns>
    public List<(int deltaX, int deltaY)> GetValidMoveDirections()
    {
        var directions = new List<(int deltaX, int deltaY)>();

        if (IsQueen)
        {
            // Queens can move in all diagonal directions
            directions.AddRange([(-1, -1), (-1, 1), (1, -1), (1, 1)]);
        }
        else
        {
            // Regular checkers can only move forward
            directions.AddRange(
                Color == Color.White ? new[] { (-1, 1), (1, 1) } : [(-1, -1), (1, -1)]
            );
        }

        return directions;
    }
}
