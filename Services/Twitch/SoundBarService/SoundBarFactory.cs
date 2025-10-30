using System.Diagnostics;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarFactory(
    IHostEnvironment environment,
    IHttpClientFactory factory,
    ILogger<SoundBarFactory> logger
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
        return environment.IsProduction() ? "http://localhost:30695" : "http://localhost:30691";
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
