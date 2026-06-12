using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.StreamAcrhive_UNUSED.Interfaces;

public interface IStreamArchiveService
{
    /// <summary>
    /// Запускает процесс архивирования потоков
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Останавливает процесс архивирования потоков
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task StopAsync(CancellationToken cancellationToken);
}
