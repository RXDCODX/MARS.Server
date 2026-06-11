namespace MARS.Server.Services.Twitch.Entitys;

[Table("Fumos")]
public class Fumo
{
    [Key]
    public int MfcId { get; set; }

    [Required]
    [MaxLength(300)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Character { get; set; }

    [MaxLength(100)]
    public string? Origin { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ThumbnailUrl { get; set; }

    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public DateTimeOffset WhenAdded { get; set; }
    public DateTimeOffset LastOrder { get; set; }
    public int OrderCount { get; set; }
}
