using System.Collections.Generic;

namespace MARS.Server.Services.CinemaQueue.Models;

public class KinopoiskSearchResponse
{
    public List<KinopoiskMovieDto>? Docs { get; set; }
    public int? Total { get; set; }
    public int? Limit { get; set; }
    public int? Page { get; set; }
    public int? Pages { get; set; }
}
