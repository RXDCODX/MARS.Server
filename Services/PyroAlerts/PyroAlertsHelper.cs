namespace MARS.Server.Services.PyroAlerts;

public class PyroAlertsHelper(ILogger<PyroAlertsHelper> logger, IWebHostEnvironment environment)
    : ITelegramusService
{
    public string TelegramCache { get; } = Path.Combine(environment.WebRootPath, "TelegramCache");

    public async ValueTask<string> GetFilePhysPath(TGFile fileInfo)
    {
        //var address = _serverAddresses?.Addresses.First();
        return await ValueTask.FromResult("/TelegramCache/" + fileInfo.FilePath);
    }

    public async Task<MediaInfo?> GetTransferObj(ITelegramBotClient client, Message message)
    {
        try
        {
            var fileInfo = await GetFilePath(client, message);

            if (fileInfo == null)
            {
                return null;
            }

            var downloadPath = TelegramCache + "\\" + fileInfo.FilePath;

            if (!File.Exists(downloadPath))
            {
                await DownloadFile(client, fileInfo, TelegramCache);
            }
            else
            {
                File.SetLastAccessTime(downloadPath, DateTimeOffset.Now.LocalDateTime);
            }

            var extension = Path.GetExtension(downloadPath);
            var fileType = await extension.GetFileMediaTypeAsync();
            var filePath = await GetFilePhysPath(fileInfo);

            var mediainfo = new MediaInfo
            {
                FileInfo = new MediaFileInfo
                {
                    Extension = extension,
                    Type = fileType,
                    FileName = fileInfo.FileUniqueId,
                    LocalFilePath = filePath,
                },
                MetaInfo = new MediaMetaInfo
                {
                    DisplayName = message.Chat.Username ?? string.Empty,
                    IsLooped = fileType == MediaType.Video ? true : false,
                    VIP = false,
                },
                PositionInfo = new MediaPositionInfo()
                {
                    IsRotated = true,
                    IsResizeRequires = true,
                    Height = 500,
                    Width = 500,
                },
                TextInfo = new MediaTextInfo(),
                StylesInfo = new MediaStylesInfo(),
            };

            switch (fileType)
            {
                case MediaType.Video:

                    mediainfo.StylesInfo.IsBorder = true;
                    mediainfo.MetaInfo.IsLooped = true;
                    mediainfo.PositionInfo.Height = 500;
                    mediainfo.PositionInfo.Width = 500;
                    mediainfo.PositionInfo.IsResizeRequires = true;
                    break;
                case MediaType.None:
                    return null;
                case MediaType.Audio:
                    mediainfo.MetaInfo.IsLooped = false;
                    break;
                case MediaType.TelegramSticker:
                    mediainfo.PositionInfo.IsProportion = true;
                    mediainfo.PositionInfo.IsResizeRequires = true;
                    mediainfo.PositionInfo.Height = 600;
                    mediainfo.PositionInfo.Width = 600;
                    break;
            }

            return mediainfo;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return null;
    }

    public async Task DownloadFile(ITelegramBotClient client, TGFile fileInfo, string folderPath)
    {
        var filePath = Path.Combine(
            folderPath,
            fileInfo.FilePath ?? throw new InvalidOperationException()
        );

        EnsureDirectoryExists(filePath);

        await using var stream = new FileStream(filePath, FileMode.Create);
        if (fileInfo.FilePath != null)
        {
            await client.DownloadFile(fileInfo.FilePath, stream);
        }
    }

    public void EnsureDirectoryExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var directoryPath = Path.GetDirectoryName(filePath);
        // ReSharper disable once PossibleNullReferenceException
        var directories = directoryPath!.Split(Path.DirectorySeparatorChar);

        var currentPath = directories[0];
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (directories != null)
        {
            for (var i = 1; i < directories.Length; i++)
            {
                currentPath += Path.DirectorySeparatorChar + directories[i];
                if (!Directory.Exists(currentPath))
                {
                    Directory.CreateDirectory(currentPath);
                }
            }
        }
    }

    public async Task<TGFile?> GetChatPhotoFilePath(ITelegramBotClient client, ChatFullInfo chat)
    {
        if (chat.Photo != null)
        {
            return await client.GetFile(chat.Photo.BigFileId);
        }

        return null;
    }

    public async Task<TGFile?> GetFilePath(ITelegramBotClient client, Message? message)
    {
        try
        {
            if (message != null)
            {
                if (message.Photo != null)
                {
                    return await client.GetFile(message.Photo.LastOrDefault()!.FileId);
                }

                if (message.Video != null)
                {
                    return await client.GetFile(message.Video.FileId);
                }

                if (message.Voice != null)
                {
                    return await client.GetFile(message.Voice.FileId);
                }

                if (message.Sticker != null)
                {
                    return await client.GetFile(message.Sticker.FileId);
                }

                if (message.Animation != null)
                {
                    return await client.GetFile(message.Animation.FileId);
                }

                if (message.Document != null)
                {
                    return await client.GetFile(message.Document.FileId);
                }

                return null;
            }
        }
        catch (Exception)
        {
            // ignored
        }

        return null;
    }
}
