namespace MARS.Server.Services.Framedata.Entitys;

/// <summary>
/// Represents a Tekken character with properties such as name, description, strengths, and weaknesses.
/// </summary>
public class TekkenCharacter
{
    [Key]
    [Required]
    public required string Name { get; set; }

    [NotMapped]
    public string DisplayName =>
        Name.Length > 0 ? string.Concat(Name[0].ToString().ToUpper(), Name.AsSpan(1)) : Name;

    [MaxLength(300)]
    public string? LinkToImage { get; set; }

    [MaxLength(200)]
    public required string PageUrl { get; set; }

    // Legacy image field - keeping for backward compatibility
    public byte[]? Image { get; set; }

    [MaxLength(20)]
    public string? ImageExtension { get; set; }

    // New avatar image field for character portraits
    public byte[]? AvatarImage { get; set; }

    [MaxLength(20)]
    public string? AvatarImageExtension { get; set; }

    // New full-body image field for background images
    public byte[]? FullBodyImage { get; set; }

    [MaxLength(20)]
    public string? FullBodyImageExtension { get; set; }

    public IEnumerable<Move>? Movelist { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; } = DateTimeOffset.Now;
    public string? Description { get; set; }
    public string[]? Strengths { get; set; }
    public string[]? Weaknesess { get; set; }
}
