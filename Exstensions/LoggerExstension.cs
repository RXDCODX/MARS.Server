using System.Text;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MARS.Server.Exstensions;

public static class LoggerExstension
{
    public static void LogException(this ILogger logger, Exception exception)
    {
        var stackTrace = exception.StackTrace;
        Exception? innerException = exception;

        while (innerException.InnerException != null)
        {
            innerException = innerException.InnerException;
        }

        logger.LogError("{0} # {1}", innerException.Message, stackTrace);
    }

    public static void LogException<T>(this ILogger<T> logger, Exception exception)
    {
        var stackTrace = exception.StackTrace;
        Exception? innerException = exception;

        var sb = new StringBuilder(exception.Message);

        while (innerException.InnerException != null)
        {
            innerException = innerException.InnerException;
            sb.Append(" + " + innerException.Message);
        }

        logger.LogError("({0}): {1} # {2}", nameof(T), sb.ToString(), stackTrace);
    }
}
