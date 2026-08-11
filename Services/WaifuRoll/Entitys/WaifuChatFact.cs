using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("WaifuChatFacts")]
public class WaifuChatFact
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string TwitchId { get; set; }

    public required string Fact { get; set; }

    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    public int Importance { get; set; } = 1;
}
