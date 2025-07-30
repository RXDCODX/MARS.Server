using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Framedata;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class ScrupFrameDataCommand(Tekken8FrameData frameData) : BaseCommand
{
    public override string CommandName => "scrupframedata";
    public override string Description => "Запускает парсинг фреймдаты Tekken 8 с сайта";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await Task.Factory.StartNew(
            async () =>
            {
                await frameData.StartScrupFrameData(null).ConfigureAwait(false);
            },
            cancellationToken
        );

        return "Парсинг запущен";
    }
}

