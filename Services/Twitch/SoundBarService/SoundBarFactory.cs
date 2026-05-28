using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarFactory(
    IHostEnvironment environment,
    IHttpClientFactory factory,
    ILogger<SoundBarFactory> logger,
    IOptions<MARS.Server.Configuration.HttpClientsConfiguration> httpClientsOptions
)
{
    private static readonly SoundBarServiceLocal SoundBarService = new();
    private static readonly SoundBarServiceLocal Instance = SoundBarService;
    private static SoundBarHttpClient? _instanceHttp;

    public ISoundBar CreateSoundBar()
    {
        var url = GetAudioControllerUrl();
        return (_instanceHttp ??= new SoundBarHttpClient(url, factory, logger));
        //return IsRunningAsWindowsService()
        //    ? (_instanceHttp ??= new SoundBarHttpClient(GetAudioControllerUrl(), logger))
        //    : Instance;
    }

    private string GetAudioControllerUrl()
    {
        var config = httpClientsOptions.Value;
        var port = environment.IsProduction()
            ? config.AudioControllerProdPort
            : config.AudioControllerDevPort;
        if (port <= 0)
        {
            // Fallback to hardcoded ports if configuration not present
            port = environment.IsProduction() ? 30695 : 30691;
        }

        return $"http://127.0.0.1:{port}";
    }

    private static bool IsRunningAsWindowsService()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // В .NET Framework можно использовать Environment.UserInteractive
        if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.Version.Major < 5) // .NET Framework
        {
            return !Environment.UserInteractive;
        }

        // В .NET Core / 5+ используем имя процесса
        try
        {
            var process = Process.GetCurrentProcess();
            var modules = process.Modules.Cast<ProcessModule>();

            // Если среди модулей есть "services.exe" или "svchost.exe" — это служба
            return modules.Any(m => m.ModuleName?.ToLower() is "services.exe" or "svchost.exe");
        }
        catch
        {
            return false;
        }
    }
}
