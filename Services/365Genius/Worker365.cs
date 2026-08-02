using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services._365Genius.Entitys;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;

namespace MARS.Server.Services._365Genius;

public class Worker365(
    IOptions<Config365> options,
    IHttpClientFactory httpClientFactory,
    IHostApplicationLifetime lifetime,
    IHostEnvironment environment,
    IDbContextFactory<AppDbContext> appDbContextFactory,
    WTelegramClientService wTelegramClientService,
    SiteAvailabilityChecker siteAvailabilityChecker,
    SiteUnavailableNotifier siteUnavailableNotifier,
    ILogger<Worker365> logger
) : IHostedService
{
    private readonly Uri _site = new(options.Value.Site ?? throw new NullReferenceException());

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public async Task Main()
    {
        ValidateConfig(options.Value);

        try
        {
            await siteAvailabilityChecker.CheckAllAsync(_site, _cancellationToken);
        }
        catch (Exception e)
        {
            await siteUnavailableNotifier.NotifyAsync(_site, e, _cancellationToken);
            throw;
        }

        var httpClient = httpClientFactory.CreateClient();

        var response = await httpClient.GetAsync(_site, _cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        var sessionId = GetPhpSessionId(response);

        var cookies = CreateLogPassCookies();
        cookies.Add(_site, sessionId);
        httpClient.DefaultRequestHeaders.Add("cookie", cookies.GetCookieHeader(_site));

        var request = GetFavoritePageRequestMessage(null);
        await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
        var pageResponse = await httpClient.SendAsync(request, _cancellationToken);

        if (!pageResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        var pageContent = await pageResponse.Content.ReadAsStringAsync(_cancellationToken);

        var parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(pageContent, _cancellationToken);

        ValidateFavouriteCountVideos(doc);

        var pageNumbers = GetFavouritePagesCount(doc);

        for (var i = pageNumbers; i >= 1; i--)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
            var pageDoc = await GetFavouritePageHtmlDocument(httpClient, i);

            await GetVideos365(httpClient, pageDoc);
        }
    }

    private static void ValidateConfig(Config365? optionsValue)
    {
        if (optionsValue is null)
        {
            throw new NullReferenceException();
        }

        if (
            string.IsNullOrWhiteSpace(optionsValue.Login)
            || string.IsNullOrWhiteSpace(optionsValue.Password)
            || string.IsNullOrWhiteSpace(optionsValue.Site)
            || optionsValue.TelegramChannelId == 0
        )
        {
            throw new NullReferenceException();
        }
    }

    private async Task<Video365> UploadVideo(HttpClient http, Video365 video)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), $"{video.Id}.mp4");
        const int bufferSize = 16 * 1024;
        await using var outputFileStream = File.Create(path, bufferSize);

        await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
        var req = await http.GetAsync(
            video.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            _cancellationToken
        );

        if (!req.IsSuccessStatusCode)
        {
            throw new NullReferenceException();
        }

        await using var responseStream = await req.Content.ReadAsStreamAsync(_cancellationToken);

        var buffer = new byte[bufferSize];
        int bytesRead;
        do
        {
            bytesRead = await responseStream.ReadAsync(
                buffer.AsMemory(0, bufferSize),
                _cancellationToken
            );
            await outputFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancellationToken);
        } while (bytesRead > 0);

        responseStream.Close();
        outputFileStream.Close();

        var bb = File.OpenRead(path);

        var botClient = await wTelegramClientService.GetClientAsync(_cancellationToken);
        var chats = await botClient.Messages_GetAllChats();
        var channel = chats.chats[options.Value.TelegramChannelId];
        var file = await botClient.UploadFileAsync(bb, video.Id.ToString());
        var thumb = await botClient.UploadFileAsync(video.ThumbnailFilePath);
        await botClient.Messages_SendMedia(
            channel,
            new InputMediaUploadedDocument
            {
                file = file,
                mime_type = "video/mp4",
                attributes =
                [
                    new DocumentAttributeVideo
                    {
                        duration = video.Duration.TotalSeconds,
                        w = video.VideoWidth,
                        h = video.VideoHeight,
                        flags = DocumentAttributeVideo.Flags.supports_streaming,
                    },
                ],
                thumb = thumb,
            },
            video.Title,
            Random.Shared.NextInt64()
        );

        video.IsUploaded = true;
        File.Delete(path);
        File.Delete(video.ThumbnailFilePath);
        return video;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsProduction())
        {
            try
            {
                await Task.Factory.StartNew(Main, _cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogException(e);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private HttpRequestMessage GetFavoritePageRequestMessage(int? pageNumber)
    {
        Uri newUri = pageNumber.HasValue
            ? new Uri(_site.AbsoluteUri + "favorites/" + pageNumber.Value)
            : new Uri(_site.AbsoluteUri + "favorites");

        var msg = new HttpRequestMessage(HttpMethod.Get, newUri);
        return msg;
    }

    private CookieContainer CreateLogPassCookies()
    {
        var container = new CookieContainer();
        container.Add(_site, new Cookie("login", options.Value.Login));
        container.Add(_site, new Cookie("password", options.Value.Password));

        return container;
    }

    private Cookie GetPhpSessionId(HttpResponseMessage response)
    {
        var container = new CookieContainer();
        var responseCookies = response.Headers.GetValues("Set-Cookie");
        foreach (var responseCookie in responseCookies)
        {
            container.SetCookies(_site, responseCookie);
        }

        var cookie = new Cookie(
            "PHPSESSID",
            container.GetCookies(_site).FirstOrDefault(c => c.Name == "PHPSESSID")?.Value
        );

        return cookie;
    }

    private static void ValidateFavouriteCountVideos(IHtmlDocument doc)
    {
        var favouriteNode = doc.Body?.SelectSingleNode("//a[@class='fav_a']") as IElement;

        var count = favouriteNode
            ?.SelectSingleNode(".//span[@class='user_fav_count']")
            ?.TextContent;

        if (!int.TryParse(count, out var aa))
        {
            throw new NullReferenceException();
        }

        if (aa == 0)
        {
            throw new NullReferenceException();
        }
    }

    private static int GetFavouritePagesCount(IHtmlDocument document)
    {
        var pagenav = document.GetElementById("pagenav") ?? throw new NullReferenceException();
        var lastPageNode =
            pagenav.SelectSingleNode(".//ul/li[last()-1]") as IElement
            ?? throw new NullReferenceException();
        var lastPageText = lastPageNode.TextContent.Trim();

        return int.TryParse(lastPageText, out var pageCount)
            ? pageCount
            : throw new NullReferenceException();
    }

    private async Task<IHtmlDocument> GetFavouritePageHtmlDocument(HttpClient httpClient, int i)
    {
        var requestMessage = GetFavoritePageRequestMessage(i);
        var favPageResponse = await httpClient.SendAsync(requestMessage, _cancellationToken);

        if (!favPageResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        var favPageContent = await favPageResponse.Content.ReadAsStringAsync(_cancellationToken);

        var parser = new HtmlParser();
        var pageDoc = await parser.ParseDocumentAsync(favPageContent, _cancellationToken);

        return pageDoc;
    }

    private async Task GetVideos365(HttpClient httpClient, IHtmlDocument document)
    {
        await using var dbConext = await appDbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );
        var liNodes = document
            .Body?.SelectNodes("//div[@id='video-content']//li[contains(@class, 'video_block')]")
            ?.Cast<IElement>()
            .Reverse()
            .ToList();
        var ids = liNodes
            ?.Select(e => int.TryParse(e.GetAttribute("id"), out var id) ? id : 0)
            .ToArray();

        var ll = ids?.Where(e => dbConext.Videos365.All(t => t.SiteId != e)).ToArray();

        if (ll != null && ll.Length != 0)
        {
            if (liNodes != null)
            {
                foreach (var node in liNodes)
                {
                    var id = int.TryParse(node.GetAttribute("id"), out var nodeId) ? nodeId : 0;
                    var link =
                        (node.SelectSingleNode(".//a[@class='image']") as IElement)?.GetAttribute(
                            "href"
                        ) ?? string.Empty;

                    var isUploaded = await dbConext.Videos365.AnyAsync(
                        e => e.SiteId == id,
                        cancellationToken: _cancellationToken
                    );

                    if (!isUploaded)
                    {
                        try
                        {
                            if (link != null)
                            {
                                var video = await GetVideo365Information(httpClient, link, id);
                                if (video != null)
                                {
                                    video = await UploadVideo(httpClient, video);

                                    if (dbConext.Videos365.Any(e => e.SiteId == video.SiteId))
                                    {
                                        dbConext.Videos365.Update(video);
                                    }
                                    else
                                    {
                                        dbConext.Videos365.Add(video);
                                    }
                                }
                            }

                            await dbConext.SaveChangesAsync(_cancellationToken);
                        }
                        catch (HttpRequestException)
                        {
                            break;
                        }
                    }
                    else
                    {
                        var pass = await dbConext.Videos365.AnyAsync(
                            e => e.SiteId == id && !e.IsUploaded,
                            cancellationToken: _cancellationToken
                        );

                        if (pass)
                        {
                            var video = await dbConext.Videos365.SingleAsync(
                                e => e.SiteId == id,
                                cancellationToken: _cancellationToken
                            );
                            video = await UploadVideo(httpClient, video);

                            if (dbConext.Videos365.Any(e => e.SiteId == video.SiteId))
                            {
                                dbConext.Videos365.Update(video);
                            }
                            else
                            {
                                dbConext.Videos365.Add(video);
                            }

                            await dbConext.SaveChangesAsync(_cancellationToken);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), _cancellationToken);
                }
            }
        }
    }

    private async Task<Video365?> GetVideo365Information(HttpClient httpClient, string link, int id)
    {
        var url = new Uri(link);
        var stringVideoId = url.Segments[2];

        if (!int.TryParse(stringVideoId, out var intId))
        {
            throw new NullReferenceException();
        }
        else if (intId != id)
        {
            throw new NullReferenceException();
        }

        var rqs = GetMovieRequest(id);
        await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
        var response = await httpClient.SendAsync(rqs, _cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        var html = await response.Content.ReadAsStringAsync(_cancellationToken);
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, _cancellationToken);

        var title = (document.Head?.SelectSingleNode("//title") as IElement)!.TextContent.Trim();
        var discription = (
            document.Body?.SelectSingleNode("//div[@class='story_desription']") as IElement
        )?.TextContent;
        var playerUrl =
            (document.Body?.SelectSingleNode("//video[@playsinline]") as IElement)?.GetAttribute(
                "src"
            ) ?? string.Empty;
        var downloadUrlElement =
            (
                document.Body?.SelectSingleNode("//ul[@class='download_ul']") as IElement
            )?.SelectSingleNode(".//a[@title]") as IElement;
        var downloadUrl = downloadUrlElement?.GetAttribute("href") ?? string.Empty;
        var durationStr = (
            document.Head?.SelectSingleNode("//meta[@property='video:duration']") as IElement
        )?.GetAttribute("content");
        var duration = int.TryParse(durationStr, out var dur) ? dur : 0;
        var widthStr = (
            document.Head?.SelectSingleNode("//meta[@property='og:video:width']") as IElement
        )?.GetAttribute("content");
        var width = int.TryParse(widthStr, out var w) ? w : 0;
        var heightStr = (
            document.Head?.SelectSingleNode("//meta[@property='og:video:height']") as IElement
        )?.GetAttribute("content");
        var height = int.TryParse(heightStr, out var h) ? h : 0;
        var thumbNailFilePath = await GetThumbNailFilePath(httpClient, document);

        if (
            string.IsNullOrWhiteSpace(discription)
            || string.IsNullOrWhiteSpace(playerUrl)
            || string.IsNullOrWhiteSpace(downloadUrl)
            || duration == 0
        )
        {
            throw new NullReferenceException();
        }

        if (duration != 0)
        {
            if (height != 0)
            {
                if (width != 0)
                {
                    return new Video365
                    {
                        Description = discription,
                        DirectLinkUrl = url.AbsoluteUri,
                        Title = title,
                        PlayerUrl = playerUrl,
                        DownloadUrl = downloadUrl,
                        SiteId = id,
                        Duration = TimeSpan.FromSeconds(duration),
                        VideoHeight = height,
                        VideoWidth = width,
                        ThumbnailFilePath = thumbNailFilePath,
                    };
                }
            }
        }

        return null;
    }

    private async Task<string> GetThumbNailFilePath(HttpClient httpClient, IHtmlDocument document)
    {
        try
        {
            var saveDirectory = Directory.GetCurrentDirectory();

            // 1. Находим элемент <link> с прелоад-изображением
            var linkNode =
                (
                    document.Head?.SelectSingleNode("//link[@rel='preload' and @as='image']")
                    as IElement
                ) ?? throw new Exception("Элемент <link> не найден.");

            // 2. Извлекаем URL изображения
            var imageUrl = linkNode.GetAttribute("href") ?? "";
            if (string.IsNullOrEmpty(imageUrl))
            {
                throw new Exception("Атрибут href не содержит ссылки.");
            }

            // 3. Скачиваем изображение
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl, _cancellationToken);

            // 4. Создаем папку, если её нет
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }

            // 5. Формируем путь для сохранения (используем имя файла из URL)
            var fileName = Path.GetFileName(new Uri(imageUrl).LocalPath);
            var filePath = Path.Combine(saveDirectory, fileName);

            // 6. Сохраняем файл
            await File.WriteAllBytesAsync(filePath, imageBytes, _cancellationToken);

            return filePath;
        }
        catch (Exception ex)
        {
            // Логируем ошибку (можно заменить на Console.WriteLine или logger)
            throw new Exception($"Ошибка при сохранении изображения: {ex.Message}");
        }
    }

    private HttpRequestMessage GetMovieRequest(int id)
    {
        var newUri = new Uri(_site.AbsoluteUri + "movie/" + id);

        var msg = new HttpRequestMessage(HttpMethod.Get, newUri);
        return msg;
    }
}
