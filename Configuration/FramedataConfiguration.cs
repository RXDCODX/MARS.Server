namespace MARS.Server.Configuration;

public enum FramedataSource
{
    Wavu,
    Tekkendocs,
}

public class FramedataConfiguration
{
    public static string SectionName { get; set; } = "Framedata";

#pragma warning disable CS8618
    public FramedataSource PrimarySource { get; set; } = FramedataSource.Tekkendocs;
#pragma warning restore CS8618
}


