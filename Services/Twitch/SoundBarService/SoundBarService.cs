using System.Diagnostics;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;
using NAudio.CoreAudioApi;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarService : ISoundBar
{
    private readonly List<AudioSessionControl> _mutedBag = [];
    private readonly Lock _locker = new();
    private readonly string[] _defaultValues = ["obs64", "obs32", "obs-browser-page"];

    public Task Mute(params string[] args)
    {
        if (args is { Length: 0 })
        {
            args = _defaultValues;
        }

        lock (_locker)
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia
            );

            // Добавьте эту проверку
            if (defaultDevice.AudioSessionManager == null)
            {
                throw new InvalidOperationException("Cannot access audio session manager");
            }

            for (var index = 0; index < defaultDevice.AudioSessionManager.Sessions.Count; index++)
            {
                var session = defaultDevice.AudioSessionManager.Sessions[index];
                try
                {
                    var processName = GetProcessName(session);
                    if (args.All(e => !processName.Contains(e, StringComparison.OrdinalIgnoreCase)))
                    {
                        lock (_locker)
                        {
                            session.SimpleAudioVolume.Mute = true;
                            _mutedBag.Add(session);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return Task.CompletedTask;
        }
    }

    private static string GetProcessName(AudioSessionControl session)
    {
        try
        {
            return session.GetProcessID == 0
                ? "System"
                : Process.GetProcessById((int)session.GetProcessID).ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    public Task Unmute()
    {
        lock (_locker)
        {
            foreach (var control in _mutedBag.ToList())
            {
                control.SimpleAudioVolume.Mute = false;
                _mutedBag.Remove(control);
            }
        }

        return Task.CompletedTask;
    }

    public string GetBagCount()
    {
        lock (_locker)
        {
            return string.Join(
                Environment.NewLine,
                _mutedBag.ToArray().Select(e => $"({e.GetProcessID}) {e.DisplayName}")
            );
        }
    }
}
