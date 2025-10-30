using System.Text;
using System.Text.Json;
using MARS.Server.Services.Twitch.SoundBarService.Entitys;
using MARS.Server.Services.Twitch.SoundBarService.Models;

namespace MARS.Server.Services.Twitch.SoundBarService;

public class SoundBarHttpClient(
    string audioControllerUrl,
    IHttpClientFactory factory,
    ILogger logger
) : ISoundBar, IDisposable
{
    private readonly string[] _defaultValues = ["obs64", "obs32", "obs-browser-page"];

    public async Task Mute(params string[] args)
    {
        if (args is not { Length: > 0 })
        {
            args = _defaultValues;
        }

        var request = new { ProcessNames = args.ToList() };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var httpClient = factory.CreateClient("Mute Service");
            var response = await httpClient.PostAsync(
                $"{audioControllerUrl}/api/soundbar/mute",
                content
            );

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Successfully muted audio for processes: {Processes}",
                    string.Join(", ", args)
                );
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError(
                    "Failed to mute audio: {StatusCode}, {Error}",
                    response.StatusCode,
                    errorContent
                );
            }
        }
        catch
        {
            logger.LogError("HTTP call failed for mute operation");
        }
    }

    public async Task Unmute()
    {
        try
        {
            using var httpClient = factory.CreateClient("Mute Service");
            var response = await httpClient.PostAsync(
                $"{audioControllerUrl}/api/soundbar/unmute",
                null
            );

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully unmuted audio");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError(
                    "Failed to unmute audio: {StatusCode}, {Error}",
                    response.StatusCode,
                    errorContent
                );
            }
        }
        catch
        {
            logger.LogError("HTTP call failed for unmute operation");
        }
    }

    public async Task<string> GetBagCount()
    {
        try
        {
            using var httpClient = factory.CreateClient("Mute Service");
            var response = await httpClient.GetAsync($"{audioControllerUrl}/api/soundbar/bagcount");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BagCountResponse>(content);
                return result?.BagCount ?? "No data";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError(
                    "Failed to get bag count: {StatusCode}, {Error}",
                    response.StatusCode,
                    errorContent
                );
                return $"Error: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError("HTTP call failed for GetBagCount operation");
            return $"Error: {ex.Message}";
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
