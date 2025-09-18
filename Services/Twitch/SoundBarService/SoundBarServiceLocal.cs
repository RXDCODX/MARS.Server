using MARS.Server.Services.Twitch.SoundBarService.Entitys;

namespace MARS.Server.Services.Twitch.SoundBarService;

// Локальная реализация для разработки
public class SoundBarServiceLocal : ISoundBar
{
    public Task Mute(params string[] args)
    {
        // Локальная заглушка для разработки
        Console.WriteLine($"Local SoundBar: Muting processes: {string.Join(", ", args)}");
        return Task.CompletedTask;
    }

    public Task Unmute()
    {
        // Локальная заглушка для разработки
        Console.WriteLine("Local SoundBar: Unmuting all processes");
        return Task.CompletedTask;
    }

    public Task<string> GetBagCount()
    {
        // Локальная заглушка для разработки
        Console.WriteLine("Local SoundBar: GetBagCount called");
        return Task.FromResult("Local: Bag count not available");
    }
}
