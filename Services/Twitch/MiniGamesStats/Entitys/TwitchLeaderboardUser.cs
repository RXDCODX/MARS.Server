using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.MiniGamesStats.Entitys;

public class TwitchLeaderboardUser
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    [NotMapped]
    public int TotalWins => RussianRouletteWins + TriviaWins;
    public int RussianRouletteWins { get; set; }
    public int RussianRouletteWinsWithWaifu { get; set; }
    public int TriviaWins { get; set; }
    public int TriviaWinsWithWaifus { get; set; }
}
