using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.Entitys;

[Table("UserMikuCollections")]
public class UserMikuCollection
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public required string TwitchUserId { get; set; }

    public int MikuPageId { get; set; }

    public int Count { get; set; }

    public DateTime FirstObtained { get; set; }

    public DateTime LastObtained { get; set; }
}
