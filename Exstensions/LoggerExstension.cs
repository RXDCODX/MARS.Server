using System.Diagnostics;
using System.Text;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MARS.Server.Exstensions;

public static class LoggerExstension
{
    public static void LogException(this ILogger logger, Exception exception)
    {
        var stackTrace = exception.Demystify().StackTrace;
        Exception? innerException = exception.Demystify();

        while (innerException.InnerException != null)
        {
            innerException = innerException.InnerException;
        }

        logger.LogError("{Message} # {StackTrace}", innerException.Message, stackTrace);
    }

    public static void LogException<T>(this ILogger<T> logger, Exception exception)
    {
        var stackTrace = exception.Demystify().StackTrace;
        Exception? innerException = exception.Demystify();

        var sb = new StringBuilder(exception.Message);

        while (innerException.InnerException != null)
        {
            innerException = innerException.InnerException;
            sb.Append(" + " + innerException.Message);
        }

        logger.LogError(
            "({ClassName}): {Message} # {StackTrace}",
            typeof(T).Name,
            sb.ToString(),
            stackTrace
        );
    }
}
