namespace MARS.Server.Services.Scoreboard.Entitys;

public class ScoreboardDto
{
    public ScoreboardPlayerDto Player1 { get; set; } = new();
    public ScoreboardPlayerDto Player2 { get; set; } = new();
    public ScoreboardMetaDto Meta { get; set; } = new();
    public ScoreboardColorsDto Colors { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public int AnimationDuration { get; set; } = 800;
    public ScoreboardLayoutDto? Layout { get; set; }
}

public class ScoreboardPlayerDto
{
    public string Name { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Final { get; set; } = "none";
}

public class ScoreboardMetaDto
{
    public string Title { get; set; } = string.Empty;
    public string FightRule { get; set; } = string.Empty;
}

public class ScoreboardColorsDto
{
    public string MainColor { get; set; } = "#3F00FF";
    public string PlayerNamesColor { get; set; } = "#FFFFFF";
    public string TournamentTitleColor { get; set; } = "#FFFFFF";
    public string FightModeColor { get; set; } = "#FFFFFF";
    public string ScoreColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "rgba(0, 0, 0, 0.8)";
    public string BorderColor { get; set; } = "#3F00FF";
}

public class ScoreboardLayoutDto
{
    // Позиционирование
    public int HeaderTop { get; set; } = 16;
    public int HeaderLeft { get; set; } = 50;
    public int PlayersTop { get; set; } = 0;
    public int PlayersLeft { get; set; } = 0;
    public int PlayersRight { get; set; } = 0;
    
    // Размеры
    public int HeaderHeight { get; set; } = 60;
    public int HeaderWidth { get; set; } = 400;
    public int PlayerBarHeight { get; set; } = 80;
    public int PlayerBarWidth { get; set; } = 500;
    public int ScoreSize { get; set; } = 60;
    public int FlagSize { get; set; } = 24;
    
    // Отступы
    public int Spacing { get; set; } = 16;
    public int Padding { get; set; } = 16;
    
    // Видимость элементов
    public bool ShowHeader { get; set; } = true;
    public bool ShowFlags { get; set; } = true;
    public bool ShowSponsors { get; set; } = true;
    public bool ShowTags { get; set; } = true;
} 