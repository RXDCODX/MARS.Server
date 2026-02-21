using System.Diagnostics;
using MARS.Server.Configuration;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.TtsMultilingual;

public class LocalSherpaCliMultilingualTtsService(
    IOptions<MultilingualTtsConfiguration> options,
    IHostEnvironment hostEnvironment,
    ILogger<LocalSherpaCliMultilingualTtsService> logger
) : IMultilingualTtsService
{
    private readonly MultilingualTtsConfiguration _configuration = options.Value;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly ILogger<LocalSherpaCliMultilingualTtsService> _logger = logger;

    public async Task<OperationResult<MultilingualTtsAudioResult>> SynthesizeAsync(
        string text,
        string? language,
        string? speaker,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<MultilingualTtsAudioResult>.Bad(
            "Локальный TTS не выполнил синтез"
        );

        if (!_configuration.Enabled)
        {
            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                "Мультиязычный TTS отключен в конфигурации"
            );
        }
        else if (!OperatingSystem.IsWindows())
        {
            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                "Локальный sherpa-onnx CLI сейчас настроен только для Windows"
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
                var executablePath = ResolvePath(_configuration.LocalSherpaExecutablePath);
                var modelDirectory = ResolveModelDirectory(language);
                var modelPath = Path.Combine(modelDirectory, "model.onnx");
                var tokensPath = Path.Combine(modelDirectory, "tokens.txt");

                if (!File.Exists(executablePath))
                {
                    result = OperationResult<MultilingualTtsAudioResult>.Bad(
                        $"Не найден sherpa CLI: {executablePath}"
                    );
                }
                else if (!File.Exists(modelPath))
                {
                    result = OperationResult<MultilingualTtsAudioResult>.Bad(
                        $"Не найден файл модели: {modelPath}"
                    );
                }
                else if (!File.Exists(tokensPath))
                {
                    result = OperationResult<MultilingualTtsAudioResult>.Bad(
                        $"Не найден файл токенов: {tokensPath}"
                    );
                }
                else
                {
                    var tempOutputPath = Path.Combine(
                        Path.GetTempPath(),
                        $"mars-tts-{Guid.NewGuid():N}.wav"
                    );

                    try
                    {
                        var arguments =
                            $"--vits-model=\"{modelPath}\" "
                            + $"--vits-tokens=\"{tokensPath}\" "
                            + $"--output-filename=\"{tempOutputPath}\" "
                            + $"\"{NormalizeText(text)}\"";

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = executablePath,
                            Arguments = arguments,
                            WorkingDirectory = Path.GetDirectoryName(executablePath)
                                ?? _hostEnvironment.ContentRootPath,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        };

                        using var process = Process.Start(startInfo);

                        if (process is null)
                        {
                            result = OperationResult<MultilingualTtsAudioResult>.Bad(
                                "Не удалось запустить процесс sherpa-onnx-offline-tts"
                            );
                        }
                        else
                        {
                            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                            await process.WaitForExitAsync(cancellationToken);

                            var standardOutput = await standardOutputTask;
                            var standardError = await standardErrorTask;

                            if (process.ExitCode != 0)
                            {
                                result = OperationResult<MultilingualTtsAudioResult>.Bad(
                                    $"Sherpa завершился с кодом {process.ExitCode}. stderr: {standardError}"
                                );
                            }
                            else if (!File.Exists(tempOutputPath))
                            {
                                result = OperationResult<MultilingualTtsAudioResult>.Bad(
                                    $"Sherpa не создал выходной файл. stdout: {standardOutput}"
                                );
                            }
                            else
                            {
                                var audioBytes = await File.ReadAllBytesAsync(
                                    tempOutputPath,
                                    cancellationToken
                                );

                                result = OperationResult<MultilingualTtsAudioResult>.Ok(
                                    "Аудио успешно синтезировано локальной моделью",
                                    new MultilingualTtsAudioResult
                                    {
                                        AudioBytes = audioBytes,
                                        ContentType = "audio/wav",
                                    }
                                );
                            }
                        }
                    }
                    finally
                    {
                        TryDeleteFile(tempOutputPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка локального синтеза через Sherpa CLI");
                result = OperationResult<MultilingualTtsAudioResult>.Bad(
                    $"Ошибка локального синтеза: {ex.Message}"
                );
            }
        }

        return result;
    }

    private string ResolveModelDirectory(string? language)
    {
        var languageCode = NormalizeLanguage(language);
        var modelRoot = ResolvePath(_configuration.LocalModelsRootPath);
        var modelFolder = languageCode switch
        {
            "en" => "vits-mms-eng",
            "es" => "vits-mms-spa",
            _ => "vits-mms-rus",
        };

        return Path.Combine(modelRoot, modelFolder);
    }

    private string ResolvePath(string path)
    {
        var result = path;

        if (!Path.IsPathFullyQualified(result))
        {
            result = Path.Combine(_hostEnvironment.ContentRootPath, result);
        }

        return Path.GetFullPath(result);
    }

    private static string NormalizeLanguage(string? language)
    {
        var result = "ru";

        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalizedLanguage = language.Trim().ToLowerInvariant();
            if (normalizedLanguage.StartsWith("en"))
            {
                result = "en";
            }
            else if (normalizedLanguage.StartsWith("es"))
            {
                result = "es";
            }
            else if (normalizedLanguage.StartsWith("ru"))
            {
                result = "ru";
            }
        }

        return result;
    }

    private static string NormalizeText(string text)
    {
        return text.Replace("\"", "'").Trim();
    }

    private void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось удалить временный файл синтеза {Path}", path);
            }
        }
    }
}