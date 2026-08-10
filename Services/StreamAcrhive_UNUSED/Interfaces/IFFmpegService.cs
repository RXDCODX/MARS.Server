using MARS.Server.Services.StreamAcrhive_UNUSED.Models;

namespace MARS.Server.Services.StreamAcrhive_UNUSED.Interfaces;

public interface IFFmpegService
{
    /// <summary>
    /// Разбивает видеофайл на части заданного размера
    /// </summary>
    /// <param name="inputPath">Путь к исходному файлу</param>
    /// <param name="outputDirectory">Директория для сохранения частей</param>
    /// <param name="maxChunkSizeBytes">Максимальный размер части в байтах</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список путей к созданным частям</returns>
    Task<List<string>> SplitVideoFileAsync(
        string inputPath,
        string outputDirectory,
        long maxChunkSizeBytes,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получает информацию о видеофайле
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Информация о видеофайле</returns>
    Task<VideoInfo?> GetVideoInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Проверяет доступность FFmpeg
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если FFmpeg доступен</returns>
    Task<bool> IsFFmpegAvailableAsync(CancellationToken cancellationToken = default);
}
