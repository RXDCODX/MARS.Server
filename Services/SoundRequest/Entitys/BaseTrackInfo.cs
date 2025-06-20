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
    public required string Url { get; init; }
    public DateTime LastTimePlays { get; set; } = DateTime.UnixEpoch;

    [NotMapped]
    public string Title
    {
        get
        {
            if (Authors is { Length: > 0 })
            {
                var authors = string.Join(',', Authors);

                if (FeatAuthors is { Length: > 0 })
                {
                    var featAuthors = string.Join(',', Authors);
                    return string.Concat(authors, ' ', '-', ' ', TrackName, " feat ", featAuthors);
                }

                return string.Concat(authors, ' ', '-', ' ', TrackName);
            }

            return TrackName;
        }
    }
}
