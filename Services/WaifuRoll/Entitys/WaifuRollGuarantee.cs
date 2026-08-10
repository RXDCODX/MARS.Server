using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("WaifuRollGuarantees")]
public class WaifuRollGuarantee
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    public int RollCount { get; set; } = 0;

    public DateTime LastRoll { get; set; } = DateTime.Now;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
