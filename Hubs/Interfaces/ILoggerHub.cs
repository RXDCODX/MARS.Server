using MARS.Server.Hubs.Models.LoggerHub;

namespace MARS.Server.Hubs.Interfaces;

public interface ILoggerHub
{
    /// <summary>
    /// Отправляет лог сообщение всем подключенным клиентам
    /// </summary>
    /// <param name="logMessage">Сообщение лога</param>
    Task Log(LogMessageDto logMessage);
}
