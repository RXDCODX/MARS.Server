namespace MARS.Server.Services.Twitch.MiniGamesStats.Entitys;

public class TwitchLeaderboardUser
{
    [Key]
    public required string TwitchId { get; set; }
    public required string DisplayName { get; set; }

    [NotMapped]
    public int TotalWins => TekkenVictorinaWins + RussianRouletteWins + TriviaWins;
    public int TekkenVictorinaWins { get; set; }
    public int TekkenVictorinaWinsWithWaifu { get; set; }
    public int RussianRouletteWins { get; set; }
    public int RussianRouletteWinsWithWaifu { get; set; }
    public int TriviaWins { get; set; }
    public int TriviaWinsWithWaifus { get; set; }
}
