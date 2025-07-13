namespace MARS.Server.Services.Framedata.Entitys;

/// <summary>
/// Represents a Tekken character with properties such as name, description, strengths, and weaknesses.
/// </summary>
public class TekkenCharacter
{
    [Key]
    [Required]
    public required string Name { get; set; }
    public string? LinkToImage { get; set; }
    public IEnumerable<Move>? Movelist { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; } = DateTimeOffset.Now;
    public string? Description { get; set; }
    public string[]? Strengths { get; set; }
    public string[]? Weaknesess { get; set; }
}
