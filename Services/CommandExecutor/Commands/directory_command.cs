namespace MARS.Server.Services.CommandExecutor.Commands;

public class DirectoryCommand : BaseCommand
{
    public override string CommandName => "directory";
    public override string Description => "Показывает структуру директорий";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord, Platform.Vk, Platform.Twitch];

    public override CommandVisibility Visibility => CommandVisibility.FullList; // Скрываем из краткого списка

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var usage = Directory.GetCurrentDirectory();

        return Task.FromResult(usage);
    }
}
