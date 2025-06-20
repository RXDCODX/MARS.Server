using System.Diagnostics;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarFactory
{
    public static SoundBarService Instance = new();
    public static SoundBarFromHub? InstanceHub;
    private readonly IHubContext<SoundBarHub, ISoundBarHub> hubContext;

    public SoundBarFactory(IHubContext<SoundBarHub, ISoundBarHub> hubContext)
    {
        this.hubContext = hubContext;
        SoundBarFactory.InstanceHub = new(hubContext);
    }

    public ISoundBar CreateSoundBar()
    {
        if (IsRunningAsWindowsService())
        {
            return InstanceHub ??= new(hubContext);
        }
        else
        {
            return Instance;
        }
    }

    private bool IsRunningAsWindowsService()
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
