namespace MARS.Server.Configuration;

public class HttpClientsConfiguration : AppBase
{
    public const string Configuration = "HttpClientsConfiguration";
    public int RutonyHttpClientPort { get; set; }
    public int HttpClientPort { get; set; }

    // AudioController ports: use different ports for development and production if needed
    public int AudioControllerDevPort { get; set; } = 30691;
    public int AudioControllerProdPort { get; set; } = 30695;
}
