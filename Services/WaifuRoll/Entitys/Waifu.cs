using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table(nameof(Waifu) + "s")]
public class Waifu
{
    [Key]
    [Required]
    [MaxLength(20)]
    public required string ShikiId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }
    public long Age { get; set; }
    public string? Anime { get; set; }
    public string? Manga { get; set; }
    public DateTime WhenAdded { get; set; }
    public DateTime LastOrder { get; set; }
    public int OrderCount { get; set; }
    public bool IsPrivated { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ImageUrl { get; set; }

    public Guid? AudioId { get; set; }

    [ForeignKey(nameof(AudioId))]
    public WaifuRollAudio? Audio { get; set; }

    [NotMapped]
    public bool IsMerged { get; set; } = false;

    [NotMapped]
    public bool IsAdded { get; set; } = false;
}
