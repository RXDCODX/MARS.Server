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

public class KinopoiskRating
{
    public double? Kp { get; set; }
    public double? Imdb { get; set; }
    public double? Tmdb { get; set; }
    public double? FilmCritics { get; set; }
    public double? RfCritics { get; set; }
    public double? Await { get; set; }
}

public class KinopoiskVotes
{
    public int? Kp { get; set; }
    public int? Imdb { get; set; }
    public int? Tmdb { get; set; }
    public int? FilmCritics { get; set; }
    public int? RfCritics { get; set; }
    public int? Await { get; set; }
}

public class KinopoiskPoster
{
    public string? Url { get; set; }
    public string? PreviewUrl { get; set; }
}

public class KinopoiskExternalId
{
    public string? Imdb { get; set; }
    public int? Tmdb { get; set; }
    public string? KpHd { get; set; }
}

public class KinopoiskSearchResponse
{
    public List<KinopoiskMovieDto>? Docs { get; set; }
    public int? Total { get; set; }
    public int? Limit { get; set; }
    public int? Page { get; set; }
    public int? Pages { get; set; }
}
