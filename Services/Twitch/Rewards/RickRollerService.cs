using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.Rewards;

public class RickRollerService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IConfiguration configuration,
    ITwitchClient client,
    TwitchUserEnsureService userEnsureService
)
{
    private static readonly Random Rnd = new();
    public double RickRollChance { get; private set; } =
        double.Parse(configuration["AppSettings:RickRoll:Chance"] ?? "0.05");

    public async Task<bool> TryRickRollAsync(TwitchUser user, Func<Task> whenNotRickRolled)
    {
        var roll = Rnd.NextDouble();
        if (roll < RickRollChance)
        {
            user = await userEnsureService.EnsureUserExistsAsync(user);
            await hubContext.Clients.All.RickRoll(user);
            await client.SendMessageToMainTwitchAsync(
                $"@{user.UserLogin}, прости, но награда тебя рикрольнула! Ничего личного, просто рандом!"
            );
            return true;
        }
        else
        {
            await whenNotRickRolled();
            return false;
        }
    }
}
