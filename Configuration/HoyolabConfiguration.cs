// ReSharper disable InconsistentNaming
namespace MARS.Server.Configuration;

public class HoyolabConfiguration
{
    public static readonly string Section = "Hoyo";
    public string ltmid_v2 { get; set; } = null!;
    public string ltoken_v2 { get; set; } = null!;
    public string ltuid_v2 { get; set; } = null!;
}
