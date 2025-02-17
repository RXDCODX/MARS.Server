using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.PyroAlerts;

public class PyroAlertsHandler(
    PyroAlertsHelper alertsHelper,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> factory
) : ITelegramusService
{
    public async Task HandAlert(ITelegramBotClient client, Update update)
    {
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();
        var whitelist = await dbContext.TelegramUsers.AsNoTracking().ToArrayAsync();
        var message = update.Message;
        if (update.Type == UpdateType.Message)
        {
            switch (message!.Type)
            {
                case MessageType.Sticker:
                    if (
                        whitelist.Any(e =>
                            (
                                e.Name?.Equals(message.Chat.Username)
                                ?? e.UserId.Equals(message.Chat.Id)
                            ) && e.PyroAlertsAccess
                        )
                    )
                    {
                        var mediaInfo = await alertsHelper.GetTransferObj(client, message);

                        if (mediaInfo != null)
                        {
                            await hubContext.Clients.All.Alert(
                                new MediaDto(mediaInfo) { MediaInfo = mediaInfo }
                            );
                        }
                    }

                    break;
                case MessageType.Video:
                    goto case MessageType.Sticker;
                case MessageType.Audio:
                    goto case MessageType.Sticker;
                case MessageType.Photo:
                    goto case MessageType.Sticker;
                case MessageType.Animation:
                    goto case MessageType.Sticker;
                case MessageType.Document:
                    goto case MessageType.Sticker;
                case MessageType.Voice:
                    if (
                        whitelist.Any(e =>
                            (
                                e.Name.Equals(message.Chat.Username)
                                || e.UserId.Equals(message.Chat.Id)
                            ) && e.PyroAlertsAccess
                        )
                    )
                    {
                        var chat = await client.GetChat(message.Chat);
                        if (chat.Photo != null)
                        {
                            var fileInfo = await alertsHelper.GetChatPhotoFilePath(client, chat);
                            if (fileInfo is { FilePath: not null })
                            {
                                await alertsHelper.DownloadFile(
                                    client,
                                    fileInfo,
                                    alertsHelper.TelegramCache
                                );
                                var avatarPath = await alertsHelper.GetFilePhysPath(fileInfo);

                                var mediaInfo = await alertsHelper.GetTransferObj(client, message);
                                if (mediaInfo != null)
                                {
                                    mediaInfo.FileInfo.Type = MediaType.Voice;
                                    mediaInfo.TextInfo.Text = avatarPath;
                                    mediaInfo.MetaInfo.DisplayName = chat.Username ?? string.Empty;
                                    await hubContext.Clients.All.Alert(
                                        new MediaDto { MediaInfo = mediaInfo }
                                    );
                                    break;
                                }
                            }
                        }
                    }
                    goto case MessageType.Sticker;
                case MessageType.Text:
                    goto case MessageType.Sticker;
            }
        }
    }
}
