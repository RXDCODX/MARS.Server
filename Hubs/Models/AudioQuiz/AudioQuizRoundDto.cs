namespace MARS.Server.Hubs.Models.AudioQuiz;

public class AudioQuizRoundDto
{
    public required string TrackUrl { get; set; }
    public string? ArtworkUrl { get; set; }
    public int RoundSeconds { get; set; }
}
