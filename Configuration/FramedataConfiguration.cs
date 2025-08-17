namespace MARS.Server.Configuration;

public enum FramedataSource
{
    None = 0,
    Wavu,
    Tekkendocs,
}

public class FramedataConfiguration
{
    public static string SectionName { get; set; } = "Framedata";

    public FramedataSource PrimarySource { get; set; } = FramedataSource.Tekkendocs;
}
