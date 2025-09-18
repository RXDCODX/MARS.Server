namespace MARS.Server.Configuration;

public class KinopoiskConfiguration
{
    public const string SectionName = "Kinopoisk";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public string Api { get; set; }
}
