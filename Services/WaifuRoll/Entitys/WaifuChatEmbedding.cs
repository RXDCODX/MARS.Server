using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("WaifuChatEmbeddings")]
public class WaifuChatEmbedding
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string TwitchId { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? Embedding { get; set; }

    public required string Text { get; set; }

    public required string Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
