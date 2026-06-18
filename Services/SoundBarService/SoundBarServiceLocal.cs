using System;
using System.Threading.Tasks;
using MARS.Server.Services.SoundBarService.Entitys;

namespace MARS.Server.Services.SoundBarService;

// Локальная реализация для разработки
public class SoundBarServiceLocal : ISoundBar
{
    public Task Mute(params string[] args)
    {
        Task result = Task.CompletedTask;

        if (args is { Length: > 0 })
        {
            // Локальная заглушка для разработки
            Console.WriteLine($"Local SoundBar: Muting processes: {string.Join(", ", args)}");
        }
        else
        {
            Console.WriteLine("Local SoundBar: Muting processes: (no processes specified)");
        }

        return result;
    }

    public Task Unmute()
    {
        Task result = Task.CompletedTask;

        // Локальная заглушка для разработки
        Console.WriteLine("Local SoundBar: Unmuting all processes");

        return result;
    }

    public Task<string> GetBagCount()
    {
        Task<string> result = Task.FromResult("Local: Bag count not available");

        // Локальная заглушка для разработки
        Console.WriteLine("Local SoundBar: GetBagCount called");

        return result;
    }

    public Task<bool> CheckHealthAsync()
    {
        return Task.FromResult(true);
    }
}
