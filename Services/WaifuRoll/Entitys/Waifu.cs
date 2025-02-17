using System.Text.Json.Serialization;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("Waifus")]
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
    public DateTimeOffset WhenAdded { get; set; }
    public DateTimeOffset LastOrder { get; set; }
    public int OrderCount { get; set; }
    public bool IsPrivated { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ImageUrl { get; set; }

    [NotMapped]
    public bool IsMerged { get; set; } = false;

    [NotMapped]
    public bool IsAdded { get; set; } = false;
}
