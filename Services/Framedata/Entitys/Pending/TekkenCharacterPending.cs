namespace MARS.Server.Services.Framedata.Entitys.Pending;

public class TekkenCharacterPending
{
    [Key]
    [Required]
    public required string Name { get; set; }

    [MaxLength(300)]
    public string? LinkToImage { get; set; }

    [MaxLength(200)]
    public required string PageUrl { get; set; }

    public byte[]? Image { get; set; }

    [MaxLength(20)]
    public string? ImageExtension { get; set; }

    public byte[]? AvatarImage { get; set; }

    [MaxLength(20)]
    public string? AvatarImageExtension { get; set; }

    public byte[]? FullBodyImage { get; set; }

    [MaxLength(20)]
    public string? FullBodyImageExtension { get; set; }

    public IEnumerable<MovePending>? Movelist { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; } = DateTimeOffset.Now;
    public string? Description { get; set; }
    public string[]? Strengths { get; set; }
    public string[]? Weaknesess { get; set; }
}
