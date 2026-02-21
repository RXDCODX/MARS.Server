using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MARS.Server.Configuration;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.TtsMultilingual;

public class HttpMultilingualTtsService(
    IHttpClientFactory httpClientFactory,
    IOptions<MultilingualTtsConfiguration> options,
    ILogger<HttpMultilingualTtsService> logger
) : IMultilingualTtsService
{
    public const string HttpClientName = "multilingual_tts";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<HttpMultilingualTtsService> _logger = logger;
    private readonly MultilingualTtsConfiguration _configuration = options.Value;

    public async Task<OperationResult<MultilingualTtsAudioResult>> SynthesizeAsync(
        string text,
        string? language,
        string? speaker,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<MultilingualTtsAudioResult>.Bad(
            "Мультиязычный TTS не выполнил синтез"
        );

        if (!_configuration.Enabled)
        {
            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                "Мультиязычный TTS отключен в конфигурации"
            );
        }
        else if (string.IsNullOrWhiteSpace(_configuration.BaseUrl))
        {
            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                "Не задан BaseUrl для мультиязычного TTS"
            );
        }
        else if (string.IsNullOrWhiteSpace(text))
        {
            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                "Текст для синтеза не может быть пустым"
            );
        }
        else
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var path = string.IsNullOrWhiteSpace(_configuration.SynthesisPath)
                    ? "/api/tts"
                    : _configuration.SynthesisPath;

                var payload = new
                {
                    text,
                    language = string.IsNullOrWhiteSpace(language)
                        ? _configuration.DefaultLanguage
                        : language,
                    speaker = string.IsNullOrWhiteSpace(speaker)
                        ? _configuration.DefaultSpeaker
                        : speaker,
                    format = _configuration.AudioFormat,
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, path)
                {
                    Content = JsonContent.Create(payload),
                };

                if (
                    !string.IsNullOrWhiteSpace(_configuration.ApiKey)
                    && !string.IsNullOrWhiteSpace(_configuration.ApiKeyHeader)
                )
                {
                    request.Headers.Remove(_configuration.ApiKeyHeader);
                    request.Headers.Add(_configuration.ApiKeyHeader, _configuration.ApiKey);
                }

                using var response = await client.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var mediaType = response.Content.Headers.ContentType?.MediaType;
                    var bodyBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.StartsWith("audio/"))
                    {
                        result = OperationResult<MultilingualTtsAudioResult>.Ok(
                            "Аудио успешно синтезировано",
                            new MultilingualTtsAudioResult
                            {
                                AudioBytes = bodyBytes,
                                ContentType = mediaType,
                            }
                        );
                    }
                    else
                    {
                        var textPayload = Encoding.UTF8.GetString(bodyBytes);
                        var parseResult = TryExtractAudioFromJson(textPayload);

                        if (parseResult.Success)
                        {
                            result = OperationResult<MultilingualTtsAudioResult>.Ok(
                                "Аудио успешно синтезировано",
                                parseResult.Data
                            );
                        }
                        else
                        {
                            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                                parseResult.Message
                            );
                        }
                    }
                }
                else
                {
                    var errorPayload = await response.Content.ReadAsStringAsync(cancellationToken);
                    result = OperationResult<MultilingualTtsAudioResult>.Bad(
                        $"Провайдер TTS вернул ошибку {(int)response.StatusCode}: {errorPayload}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обращении к мультиязычному TTS провайдеру");
                result = OperationResult<MultilingualTtsAudioResult>.Bad(
                    $"Ошибка обращения к TTS провайдеру: {ex.Message}"
                );
            }
        }

        return result;
    }

    private static OperationResult<MultilingualTtsAudioResult> TryExtractAudioFromJson(string payload)
    {
        var result = OperationResult<MultilingualTtsAudioResult>.Bad(
            "Ответ провайдера не содержит аудио-данных"
        );

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            var base64Audio = string.Empty;

            if (root.TryGetProperty("audio", out var audioProperty))
            {
                base64Audio = audioProperty.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("audioBase64", out var audioBase64Property))
            {
                base64Audio = audioBase64Property.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("wavBase64", out var wavBase64Property))
            {
                base64Audio = wavBase64Property.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("data", out var dataProperty))
            {
                base64Audio = dataProperty.GetString() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(base64Audio))
            {
                var audioBytes = Convert.FromBase64String(base64Audio);
                var contentType = "audio/wav";

                if (root.TryGetProperty("contentType", out var contentTypeProperty))
                {
                    contentType = contentTypeProperty.GetString() ?? contentType;
                }
                else if (root.TryGetProperty("mimeType", out var mimeTypeProperty))
                {
                    contentType = mimeTypeProperty.GetString() ?? contentType;
                }

                result = OperationResult<MultilingualTtsAudioResult>.Ok(
                    "Аудио успешно извлечено из JSON",
                    new MultilingualTtsAudioResult
                    {
                        AudioBytes = audioBytes,
                        ContentType = contentType,
                    }
                );
            }
        }
        catch (Exception ex)
        {
            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                $"Не удалось разобрать JSON-ответ провайдера: {ex.Message}"
            );
        }

        return result;
    }
}