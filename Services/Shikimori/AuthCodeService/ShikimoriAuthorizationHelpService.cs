using System.Text.Json;
using MARS.Server.Services.Shikimori.Entitys;
using Newtonsoft.Json;

namespace MARS.Server.Services.Shikimori.AuthCodeService;

public class ShikimoriAuthorizationHelpService : ITelegramusService
{
    private readonly ShikimoriClientOptions _options;
    private readonly string _shikiAuthUrl;

    public ShikimoriAuthorizationHelpService(IOptions<ShikimoriClientOptions> configuration)
    {
        _options = configuration.Value ?? throw new NullReferenceException();
        _shikiAuthUrl = _options.ShikimoriSite + "/oauth/token";
    }

    public async Task<ShikiAccessToken> GetCodeFromFile()
    {
        if (File.Exists(_options.AuthFilePath))
        {
            var text = await File.ReadAllTextAsync(_options.AuthFilePath);
            var token = JsonConvert.DeserializeObject<ShikiAccessToken>(text);

            if (token is null)
                throw new NullReferenceException();

            return token;
        }

        throw new FileNotFoundException();
    }

    public async Task<ShikiAccessToken> RefreshToken(HttpClient client, ShikiAccessToken oldToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, _shikiAuthUrl);

        message.Headers.Add("User-Agent", _options.ClientName);

        var content = new MultipartFormDataContent
        {
            { new StringContent("refresh_token"), "grant_type" },
            { new StringContent(_options.ClientId), "client_id" },
            { new StringContent(_options.ClientSecret), "client_secret" },
            { new StringContent(oldToken.RefreshToken), "refresh_token" },
        };
        message.Content = content;

        var response = await client.SendAsync(message);

        if (response.IsSuccessStatusCode)
        {
            var responsecontent = await response.Content.ReadFromJsonAsync<ShikiAccessToken>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = false }
            );

            return responsecontent
                ?? throw new HttpRequestException("RefreshToken() вернул null-контент");
        }

        throw new BadHttpRequestException("Запрос к серверу авторизации Shiki вернул ошибку");
    }
}
