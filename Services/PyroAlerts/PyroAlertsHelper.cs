namespace MARS.Server.Services.PyroAlerts;

public class PyroAlertsHelper(ILogger<PyroAlertsHelper> logger) : ITelegramusService
{
    public async Task<MediaInfo?> GetTransferObj(ITelegramBotClient client, Message message)
    {
        try
        {
            var fileInfo = await GetTgFileInfo(client, message);

            if (fileInfo == null)
            {
                return null;
            }

            var fileContent = await DownloadFile(client, fileInfo);

            var downloadPath = fileInfo.FilePath ?? throw new NullReferenceException();
            var extension = Path.GetExtension(downloadPath);
            var fileType = await extension.GetFileMediaTypeAsync();

            var mediainfo = new MediaInfo
            {
                FileInfo = new MediaFileInfo
                {
                    Extension = extension,
                    Type = fileType,
                    FileName = fileInfo.FilePath,
                    IsLocalFile = true,
                    FilePath = "memory/" + fileInfo.FilePath,
                },
                MetaInfo = new MediaMetaInfo
                {
                    DisplayName = message.Chat.Username ?? string.Empty,
                    IsLooped = fileType == MediaType.Video,
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

            await MemoryStorage.AddFileAsync(fileInfo.FilePath, fileContent);

            return mediainfo;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return null;
    }

    public async Task<byte[]> DownloadFile(ITelegramBotClient client, TGFile fileInfo)
    {
        if (!fileInfo.FileSize.HasValue)
        {
            throw new NullReferenceException();
        }

        var buffer = new byte[fileInfo.FileSize.Value];
        await using var stream = new MemoryStream(buffer, true);
        try
        {
            if (fileInfo.FilePath != null)
            {
                await client.DownloadFile(fileInfo.FilePath, stream);
            }
            else
            {
                Array.Clear(buffer);
            }
        }
        catch (Exception)
        {
            Array.Clear(buffer);
        }

        return buffer;
    }

    public async Task DownloadFileAndCache(
        ITelegramBotClient client,
        TGFile fileInfo,
        string folderPath
    )
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
        return chat.Photo != null ? await client.GetFile(chat.Photo.BigFileId) : null;
    }

    public async Task<TGFile?> GetTgFileInfo(ITelegramBotClient client, Message? message)
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
