using System.Collections.Frozen;
using MARS.Server.Services.MemoryStorageService;
using TL;
using Document = TL.Document;
using Message = TL.Message;
using PhotoSize = TL.PhotoSize;
using Update = TL.Update;

namespace MARS.Server.Services.RandomMem;

public class RandomMemOnline(
    IHostApplicationLifetime lifetime,
    WTelegramClient client,
    IDbContextFactory<AppDbContext> factory,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext
) : BackgroundService
{
    public static bool IsStop
    {
        get;
        set
        {
            if (StaticDbContextFactory.Factory != null)
            {
                using var dbContext = StaticDbContextFactory.Factory.CreateDbContext();
                var state = dbContext.ApplicationState.Single();
                state.RandomMemeOnlineIsStop = value;
                dbContext.SaveChanges();
            }

            field = value;
        }
    }

    private readonly FrozenSet<long> _allowedIds = factory
        .CreateDbContext()
        .WTelegramAlloweedChannels.Select(e => e.Value)
        .ToFrozenSet();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            using var dbContext = factory.CreateDbContext();
            var isStop = dbContext.ApplicationState.Single().RandomMemeOnlineIsStop;

            IsStop = isStop;

            client.OnUpdates += async (@base) =>
            {
                if (IsStop)
                {
                    return;
                }

                foreach (Update update in @base.UpdateList)
                {
                    if (update is UpdateNewChannelMessage message)
                    {
                        await Task.Factory.StartNew(() => OnUpdate(message), stoppingToken);
                    }
                }
            };
        });

        return Task.CompletedTask;
    }

    private async Task OnUpdate(UpdateNewChannelMessage arg1)
    {
        if (arg1.message is Message message)
        {
            if (_allowedIds.Contains(message.peer_id.ID))
            {
                if (
                    message.entities.Any(e =>
                        e
                            is MessageEntityTextUrl
                                or MessageEntityUrl
                                or MessageEntityBankCard
                                or MessageEntityBotCommand
                                or MessageEntityCashtag
                                or MessageEntityCode
                                or MessageEntityEmail
                                or MessageEntityPhone
                                or MessageEntitySpoiler
                                or MessageEntityMention
                                or MessageEntityMentionName
                    )
                )
                {
                    return;
                }

                switch (message.media)
                {
                    case MessageMediaPhoto photo:
                        if (photo.photo is Photo photoa)
                        {
                            await Task.Factory.StartNew(
                                () => ProcessPhoto(photoa, message.message)
                            );
                        }
                        break;
                    case MessageMediaDocument document:
                        if (document.document is Document doc)
                        {
                            var typeFile = doc.mime_type.Split('/')[1];
                            if (
                                Enum.TryParse(
                                    typeof(Storage_FileType),
                                    typeFile,
                                    true,
                                    out var value
                                )
                            )
                            {
                                var fileType = (Storage_FileType)value;

                                switch (fileType)
                                {
                                    case Storage_FileType.jpeg
                                    or Storage_FileType.png
                                    or Storage_FileType.webp:
                                        await Task.Factory.StartNew(
                                            () => ProcessPhotoDocument(doc, message.message)
                                        );
                                        break;
                                    case Storage_FileType.gif
                                    or Storage_FileType.mov
                                    or Storage_FileType.mp4:
                                        await Task.Factory.StartNew(
                                            () => ProcessVideoDocument(doc, message.message)
                                        );
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        }
    }

    private async Task ProcessVideoDocument(Document doc, string? message)
    {
        var buffer = new byte[doc.size];
        await using var fs = new MemoryStream(buffer);

        var fileInfo = await client.DownloadFileAsync(doc, fs);
        var extension = fileInfo.Split('/')[1];
        var fileName = $"{doc.id}.{extension}";

        await MemoryStorage.AddFileAsync(fileName, buffer);

        var mediaInfo = new MediaDto(
            new MediaInfo
            {
                FileInfo = new MediaFileInfo
                {
                    FilePath = "memory/" + fileName,
                    Extension = extension,
                    FileName = fileName,
                    Type = MediaType.Video,
                },
                MetaInfo = new MediaMetaInfo
                {
                    DisplayName = string.Empty,
                    Duration = 999,
                    IsLooped = false,
                    Priority = MediaAlertPriority.Normal,
                },
                PositionInfo = new MediaPositionInfo(),
                StylesInfo = new MediaStylesInfo(),
                TextInfo = new MediaTextInfo { Text = message },
            }
        );

        await hubContext.Clients.All.RandomMem(mediaInfo);
    }

    private async Task ProcessPhoto(Photo photo, string? message)
    {
        if (photo.LargestPhotoSize is PhotoSize size)
        {
            var buffer = new byte[size.FileSize];
            await using var fs = new MemoryStream(buffer);

            var fileInfo = await client.DownloadFileAsync(photo, fs, size);
            var fileName = $"{photo.id}.jpeg";

            await MemoryStorage.AddFileAsync($"{photo.ID}.{fileInfo}", buffer);

            var mediaInfo = new MediaDto(
                new MediaInfo
                {
                    FileInfo = new MediaFileInfo
                    {
                        FilePath = "memory/" + fileName,
                        Extension = "jpeg",
                        FileName = fileName,
                        Type = MediaType.Image,
                    },
                    MetaInfo = new MediaMetaInfo
                    {
                        DisplayName = string.Empty,
                        IsLooped = false,
                        Priority = MediaAlertPriority.Normal,
                    },
                    PositionInfo = new MediaPositionInfo(),
                    StylesInfo = new MediaStylesInfo(),
                    TextInfo = new MediaTextInfo { Text = message },
                }
            );

            await hubContext.Clients.All.RandomMem(mediaInfo);
        }
    }

    private async Task ProcessPhotoDocument(Document document, string? message)
    {
        var buffer = new byte[document.size];
        await using var fs = new MemoryStream(buffer);

        var fileInfo = await client.DownloadFileAsync(document, fs);
        var extension = fileInfo.Split('/')[1];
        var fileName = $"{document.id}.{extension}";

        await MemoryStorage.AddFileAsync($"{document.id}.{fileInfo.Split('/')[1]}", buffer);

        var mediaInfo = new MediaDto(
            new MediaInfo
            {
                FileInfo = new MediaFileInfo
                {
                    FilePath = "memory/" + fileName,
                    Extension = extension,
                    FileName = fileName,
                    Type = MediaType.Image,
                },
                MetaInfo = new MediaMetaInfo
                {
                    DisplayName = string.Empty,
                    IsLooped = false,
                    Priority = MediaAlertPriority.Normal,
                },
                PositionInfo = new MediaPositionInfo(),
                StylesInfo = new MediaStylesInfo(),
                TextInfo = new MediaTextInfo { Text = message },
            }
        );

        await hubContext.Clients.All.RandomMem(mediaInfo);
    }
}
