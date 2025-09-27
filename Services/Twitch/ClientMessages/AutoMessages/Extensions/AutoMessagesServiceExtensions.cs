using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Interfaces;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Services;

namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Extensions;

public static class AutoMessagesServiceExtensions
{
    /// <summary>
    /// Добавляет сервис для работы с автоматическими сообщениями
    /// </summary>
    public static IServiceCollection AddAutoMessagesService(this IServiceCollection services)
    {
        services.AddScoped<IAutoMessagesService, AutoMessagesService>();
        return services;
    }
}

