namespace MARS.Server.Services.Scoreboard.Entitys;

public class ScoreboardPlayer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Final { get; set; } = "none"; // "winner", "loser", "none"
    public int Position { get; set; } // 1 или 2 для определения позиции игрока
    public int ScoreboardStateId { get; set; }
    public ScoreboardState ScoreboardState { get; set; } = null!;
}
