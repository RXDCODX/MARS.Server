using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.Synthesizer.Entitys;

[Table("SevenTvEmotes")]
public class SevenTvEmote
{
    [Key]
    [MaxLength(100)]
    public required string Name { get; set; }

    public DateTime LoadedAt { get; set; } = DateTime.Now;
}
