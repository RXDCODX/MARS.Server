namespace MARS.Server.Services.SoundRequest.Entitys;

public class BaseTrackInfo
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public required string TrackName { get; set; }
    public string[]? Authors { get; set; }
    public string[]? FeatAuthors { get; set; }
    public TimeSpan Duration { get; set; }
    public string[]? Genre { get; set; }
}
