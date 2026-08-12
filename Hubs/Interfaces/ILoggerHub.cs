using MARS.Server.Hubs.Models.LoggerHub;
using SignalRSwaggerGen.Attributes;

namespace MARS.Server.Hubs.Interfaces;

[SignalRHub("/hubs/logger")]
public interface ILoggerHub
{
    /// <summary>
    /// Отправляет лог сообщение всем подключенным клиентам
    /// </summary>
    /// <param name="logMessage">Сообщение лога</param>
    Task Log(LogMessageDto logMessage);
}
