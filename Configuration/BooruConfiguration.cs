#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Configuration;

public class BooruConfiguration
{
    public const string Section = "Booru";
    public string Login { get; set; }
    public string ApiKey { get; set; }
}
