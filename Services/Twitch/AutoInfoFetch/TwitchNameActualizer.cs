using MARS.Server.Services.Twitch.Entitys;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.AutoInfoFetch;

public class TwitchNameActualizer(
    ITwitchClient client,
    IDbContextFactory<AppDbContext> factory,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private static readonly List<string> CachedTwitchIds = [];
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        });

        return Task.CompletedTask;
    }

    private async void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var userId = e.ChatMessage.UserId;
        var userName = e.ChatMessage.DisplayName;

        if (
            CachedTwitchIds.Contains(userId)
            || TwitchExstension.BlackList.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        await Task.Factory.StartNew(
            async () =>
            {
                await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
                
                // Проверяем существует ли TwitchUser
                var twitchUser = await dbContext.TwitchUsers
                    .FirstOrDefaultAsync(u => u.TwitchId == userId, _cancellationToken);

                if (twitchUser != null)
                {
                    // Обновляем данные пользователя
                    var needsUpdate = false;

                    if (twitchUser.DisplayName != userName)
                    {
                        twitchUser.DisplayName = userName;
                        needsUpdate = true;
                    }

                    if (twitchUser.UserLogin != e.ChatMessage.Username)
                    {
                        twitchUser.UserLogin = e.ChatMessage.Username;
                        needsUpdate = true;
                    }

                    if (!string.IsNullOrWhiteSpace(e.ChatMessage.ColorHex) && 
                        twitchUser.ChatColor != e.ChatMessage.ColorHex)
                    {
                        twitchUser.ChatColor = e.ChatMessage.ColorHex;
                        needsUpdate = true;
                    }

                    if (twitchUser.IsModerator != e.ChatMessage.IsModerator)
                    {
                        twitchUser.IsModerator = e.ChatMessage.IsModerator;
                        needsUpdate = true;
                    }

                    if (twitchUser.IsVip != e.ChatMessage.IsVip)
                    {
                        twitchUser.IsVip = e.ChatMessage.IsVip;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        twitchUser.LastUpdated = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(_cancellationToken);
                    }
                }
                else
                {
                    // Создаем нового пользователя если его нет
                    var newTwitchUser = new TwitchUser
                    {
                        TwitchId = userId,
                        UserLogin = e.ChatMessage.Username,
                        DisplayName = userName,
                        ChatColor = e.ChatMessage.ColorHex,
                        IsModerator = e.ChatMessage.IsModerator,
                        IsVip = e.ChatMessage.IsVip,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    await dbContext.TwitchUsers.AddAsync(newTwitchUser, _cancellationToken);
                    await dbContext.SaveChangesAsync(_cancellationToken);
                }

                CachedTwitchIds.Add(userId);
            },
            _cancellationToken
        );
    }
}
