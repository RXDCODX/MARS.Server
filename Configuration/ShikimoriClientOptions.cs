namespace MARS.Server.Configuration;

public class ShikimoriClientOptions : AppBase
{
    public static string Options = "ShikimoriClientOptions";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string ClientName { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RedirectUri { get; set; }
    public string AuthFilePath { get; set; }
    public string ShikimoriSite { get; set; }
}
