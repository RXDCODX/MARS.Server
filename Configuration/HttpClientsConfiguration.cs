namespace MARS.Server.Configuration;

public class HttpClientsConfiguration : AppBase
{
    public const string Configuration = "HttpClientsConfiguration";
    public int RutonyHttpClientPort { get; set; }
    public int HttpClientPort { get; set; }
}
