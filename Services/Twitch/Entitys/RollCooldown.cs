using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.Entitys;

[Table("RollCooldowns")]
public class RollCooldown
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public required string TwitchUserId { get; set; }

    [Required]
    [MaxLength(50)]
    public required string RollType { get; set; }

    public DateTime LastRollTime { get; set; }
}
