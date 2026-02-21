namespace MARS.Server.Configuration;

public enum FramedataSource
{
    None = 0,
    Wavu,
    Tekkendocs,
    Okizeme,
}

public class FramedataConfiguration
{
    public static string SectionName { get; set; } = "Framedata";

    public FramedataSource PrimarySource { get; set; } = FramedataSource.Okizeme;
}
