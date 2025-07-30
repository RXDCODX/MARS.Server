namespace MARS.Server.Services.CommandExecutor.Entitys;

/// <summary>
/// Платформы, на которых могут выполняться команды
/// </summary>
[Flags]
public enum Platform
{
    None,
    Api,
    Telegram,
    Twitch,
    Discord,
    Vk,
}
