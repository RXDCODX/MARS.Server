namespace MARS.Server.Services.Framedata.Entitys;

public class TekkenCharacter
{
    [Key]
    [Required]
    public required string Name { get; set; }
    public string? LinkToImage { get; set; }
    public IEnumerable<Move>? Movelist { get; set; }
    public DateTimeOffset LastUpdateTime { get; set; } = DateTimeOffset.Now;
}
