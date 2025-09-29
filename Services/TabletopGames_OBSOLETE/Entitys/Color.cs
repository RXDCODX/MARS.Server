namespace MARS.Server.Services.TabletopGames_OBSOLETE.Entitys;

/// <summary>
/// Represents the color of a checker piece or cell.
/// </summary>
public enum Color
{
    White,
    Black,
}

/// <summary>
/// Extension methods for Color enum.
/// </summary>
public static class ColorExtensions
{
    public static bool Equals(this Color color, Color other)
    {
        return color == other;
    }
}
