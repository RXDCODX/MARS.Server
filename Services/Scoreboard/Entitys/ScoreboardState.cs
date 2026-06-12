using System;
using System.Collections.Generic;

namespace MARS.Server.Services.Scoreboard.Entitys;

public class ScoreboardState
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FightRule { get; set; } = string.Empty;
    public string MainColor { get; set; } = "#3F00FF";
    public string PlayerNamesColor { get; set; } = "#FFFFFF";
    public string TournamentTitleColor { get; set; } = "#FFFFFF";
    public string FightModeColor { get; set; } = "#FFFFFF";
    public string ScoreColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "rgba(0, 0, 0, 0.8)";
    public string BorderColor { get; set; } = "#3F00FF";
    public bool IsVisible { get; set; } = true;
    public int AnimationDuration { get; set; } = 800;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Навигационные свойства
    public ICollection<ScoreboardPlayer> Players { get; set; } = [];
    public ScoreboardLayout? Layout { get; set; }
}
