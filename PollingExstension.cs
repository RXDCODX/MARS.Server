using Microsoft.Extensions.Options;

namespace MARS.Server;

public static class PollingExtensions
{
    public static T GetConfiguration<T>(this IServiceProvider serviceProvider)
        where T : class
    {
        var o = serviceProvider.GetService<IOptions<T>>();
        return o is null ? throw new ArgumentNullException(typeof(T).Name) : o.Value;
    }
}
