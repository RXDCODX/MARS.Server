namespace MARS.Server.Services.CinemaQueue.Models;

public class KinopoiskMovieDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? AlternativeName { get; set; }
    public string? EnName { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? Slogan { get; set; }
    public int? Year { get; set; }
    public string? Type { get; set; }
    public int? TypeNumber { get; set; }
    public string? Status { get; set; }
    public int? MovieLength { get; set; }
    public KinopoiskRating? Rating { get; set; }
    public KinopoiskVotes? Votes { get; set; }
    public KinopoiskPoster? Poster { get; set; }
    public KinopoiskExternalId? ExternalId { get; set; }
}
