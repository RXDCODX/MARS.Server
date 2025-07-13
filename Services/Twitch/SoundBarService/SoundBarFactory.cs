using System.Diagnostics;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarFactory
{
    private static readonly SoundBarService SoundBarService = new();
    private static readonly SoundBarService Instance = SoundBarService;
    private static SoundBarFromHub? _instanceHub;
    private readonly IHubContext<SoundBarHub, ISoundBarHub> _hubContext;

    public SoundBarFactory(IHubContext<SoundBarHub, ISoundBarHub> hubContext)
    {
        _hubContext = hubContext;
        _instanceHub ??= new SoundBarFromHub(hubContext);
    }

    public ISoundBar CreateSoundBar()
    {
        return IsRunningAsWindowsService()
            ? (_instanceHub ??= new SoundBarFromHub(_hubContext))
            : Instance;
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
