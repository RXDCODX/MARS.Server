using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.MiniGamesStats.Entitys;

public class TwitchLeaderboardUser
{
    [Key]
    [Required]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя (дублируется для обратной совместимости)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [NotMapped]
    public int TotalWins => TekkenVictorinaWins + RussianRouletteWins + TriviaWins;
    public int TekkenVictorinaWins { get; set; }
    public int TekkenVictorinaWinsWithWaifu { get; set; }
    public int RussianRouletteWins { get; set; }
    public int RussianRouletteWinsWithWaifu { get; set; }
    public int TriviaWins { get; set; }
    public int TriviaWinsWithWaifus { get; set; }
}
