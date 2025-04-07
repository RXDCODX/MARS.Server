using System.Net;
using System.Xml.XPath;
using HtmlAgilityPack;
using MARS.Server.Services._365Genius.Entitys;
using TL;
using WTelegram;
using InputMediaDocument = TL.InputMediaDocument;

namespace MARS.Server.Services._365Genius;

public class Worker365(
    IOptions<Config365> options,
    IHttpClientFactory httpClientFactory,
    IHostApplicationLifetime lifetime,
    IDbContextFactory<AppDbContext> appDbContextFactory,
    Client botClient
) : IHostedService
{
    private readonly Uri _site = new(options.Value.Site ?? throw new NullReferenceException());

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public async Task Main()
    {
        ValidateConfig(options.Value);

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

        var pageContent = await pageResponse.Content.ReadAsStreamAsync(_cancellationToken);

        var doc = new HtmlDocument();
        doc.Load(pageContent);

        ValidateFavouriteCountVideos(doc);

        var pageNumbers = GetFavouritePagesCount(doc);

        var videoList = new List<Video365>();
        for (var i = pageNumbers; i >= 1; i--)
        {
            var pageDoc = await GetFavouritePageHtmlDocument(httpClient, i);

            var result = await GetVideos365(httpClient, pageDoc);
            videoList.AddRange(result.Item1);

            if (result.Item2)
            {
                break;
            }
        }

        await UploadVideos(httpClient, videoList);
    }

    private void ValidateConfig(Config365? optionsValue)
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

    private async Task UploadVideos(HttpClient http, List<Video365> videoList)
    {
        await using var dbContext = await appDbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );

        foreach (var video in videoList)
        {
            try
            {
                var bb = await UploadVideo(http, video);
                dbContext.Videos365.Add(bb);
            }
            catch (Exception)
            {
                dbContext.Videos365.Add(video);
                // ignored
            }

            await dbContext.SaveChangesAsync(_cancellationToken);
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
            bytesRead = await responseStream.ReadAsync(buffer, 0, bufferSize, _cancellationToken);
            await outputFileStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
        } while (bytesRead > 0);

        responseStream.Close();
        outputFileStream.Close();

        var bb = File.OpenRead(path);

        var chats = await botClient.Messages_GetAllChats();
        var channel = chats.chats[options.Value.TelegramChannelId];
        var file = await botClient.UploadFileAsync(bb, video.Id.ToString());
        await botClient.Messages_SendMedia(
            channel,
            new InputMediaUploadedDocument()
            {
                file = file,
                mime_type = "video/mp4",
                attributes =
                [
                    new DocumentAttributeVideo()
                    {
                        duration = video.Duration.TotalSeconds,
                        w = video.VideoWidth,
                        h = video.VideoHeight,
                        flags = DocumentAttributeVideo.Flags.supports_streaming,
                    },
                ],
            },
            video.Title,
            Random.Shared.NextInt64()
        );

        video.IsUploaded = true;
        File.Delete(path);
        return video;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Main();
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

    private void ValidateFavouriteCountVideos(HtmlDocument doc)
    {
        var favouriteNode = doc.DocumentNode.SelectSingleNode(
            XPathExpression.Compile("//a[@class=\"fav_a\"]")
        );

        var count = favouriteNode.SelectSingleNode("//span[@class=\"user_fav_count\"]").InnerText;

        if (!int.TryParse(count, out var aa))
        {
            throw new NullReferenceException();
        }

        if (aa == 0)
        {
            throw new NullReferenceException();
        }
    }

    private int GetFavouritePagesCount(HtmlDocument document)
    {
        var pagenav = document.GetElementbyId("pagenav");
        if (pagenav == null)
        {
            throw new NullReferenceException();
        }

        var lastPageNode = pagenav.SelectSingleNode(".//ul/li[last()-1]");
        if (lastPageNode == null)
        {
            throw new NullReferenceException();
        }

        var lastPageText = lastPageNode.InnerText.Trim();

        if (int.TryParse(lastPageText, out var pageCount))
        {
            return pageCount;
        }

        throw new NullReferenceException();
    }

    private async Task<HtmlDocument> GetFavouritePageHtmlDocument(HttpClient httpClient, int i)
    {
        var requestMessage = GetFavoritePageRequestMessage(i);
        var favPageResponse = await httpClient.SendAsync(requestMessage, _cancellationToken);

        if (!favPageResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        var favPageContent = await favPageResponse.Content.ReadAsStreamAsync(_cancellationToken);

        var pageDoc = new HtmlDocument();
        pageDoc.Load(favPageContent);

        return pageDoc;
    }

    private async Task<(ICollection<Video365>, bool)> GetVideos365(
        HttpClient httpClient,
        HtmlDocument document
    )
    {
        var rr = false;
        await using var dbConext = await appDbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );
        var liNodes = document
            .DocumentNode.SelectNodes(
                "//div[@id='video-content']//li[contains(@class, 'video_block')]"
            )
            .Reverse()
            .ToList();
        var list = new List<Video365>();

        foreach (var node in liNodes)
        {
            var id = node.GetAttributeValue("id", 0);
            var link = node.SelectSingleNode(".//a[@class=\"image\"]")
                .GetAttributeValue("href", string.Empty);

            var isUploaded = await dbConext.Videos365.AnyAsync(
                e => e.SiteId == id,
                cancellationToken: _cancellationToken
            );

            if (!isUploaded)
            {
                try
                {
                    var video = await GetVideo365Information(httpClient, link, id);
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
                catch (HttpRequestException)
                {
                    rr = true;
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

        return (list, rr);
    }

    private async Task<Video365> GetVideo365Information(HttpClient httpClient, string link, int id)
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

        var stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
        var document = new HtmlDocument();
        document.Load(stream);

        var title = document.DocumentNode.SelectSingleNode("//head/title")!.InnerText.Trim();
        var discription = document
            .DocumentNode.SelectSingleNode("//div[@class=\"story_desription\"]")
            .InnerText;
        var playerUrl = document
            .DocumentNode.SelectSingleNode("//video[@playsinline]")
            .GetAttributeValue("src", string.Empty);
        var downloadUrl = document
            .DocumentNode.SelectSingleNode("//ul[@class=\"download_ul\"]")
            .SelectSingleNode("//a[@title]")
            .GetAttributeValue("href", string.Empty);
        var duration = document
            .DocumentNode.SelectSingleNode("//meta[@property='video:duration']")
            .GetAttributeValue("content", 0);
        var width = document
            .DocumentNode.SelectSingleNode("//meta[@property='og:video:width']")
            .GetAttributeValue("content", 0);
        var height = document
            .DocumentNode.SelectSingleNode("//meta[@property='og:video:height']")
            .GetAttributeValue("content", 0);

        if (
            string.IsNullOrWhiteSpace(discription)
            || string.IsNullOrWhiteSpace(playerUrl)
            || string.IsNullOrWhiteSpace(downloadUrl)
            || duration == 0
        )
        {
            throw new NullReferenceException();
        }

        return new Video365()
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
        };
    }

    private HttpRequestMessage GetMovieRequest(int id)
    {
        var newUri = new Uri(_site.AbsoluteUri + "movie/" + id);

        var msg = new HttpRequestMessage(HttpMethod.Get, newUri);
        return msg;
    }
}
