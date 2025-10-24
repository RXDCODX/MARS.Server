using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;

namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages;

public class AutoMessagesHandler(
    ITwitchClient client,
    ILogger<AutoMessagesHandler> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime applicationLifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext
) : BackgroundService
{
    private const string Channel = TwitchExstension.Channel;

    /// <summary>
    /// Не делать меньше 2
    /// </summary>
    private const int Capacity = 3;
    private readonly Queue<AutoMessage> _queue = new(Capacity);

    private int MessagesCounter { get; set; }
    private DateTimeOffset LastPostDateTime { get; set; } = DateTimeOffset.MinValue;

    public async void OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase)
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            MessagesCounter++;

            if (
                MessagesCounter >= 70
                && LastPostDateTime.Add(TimeSpan.FromMinutes(45)) < DateTimeOffset.Now
            )
            {
                await ExecuteAutoMessage();
            }
        }
    }

    internal async Task ExecuteAutoMessage()
    {
        await Task.Run(async () =>
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var messages = dbContext
                    .AutoMessages.AsNoTracking()
                    .AsEnumerable()
                    .Where(e => _queue.All(message => message.Id != e.Id))
                    .ToArray();

                if (messages.Length != 0)
                {
                    var index = Random.Shared.Next(0, messages.Length - 1);
                    var message = messages.ElementAt(index);

                    client.SendMessage(Channel, message.Message);

                    // Отправляем сообщение через SignalR для отображения в OBS
                    await hubContext.Clients.All.AutoMessage(message.Message);

                    while (_queue.Count > Capacity - 1)
                    {
                        _queue.Dequeue();
                    }

                    _queue.Enqueue(message);

                    LastPostDateTime = DateTimeOffset.Now;
                    MessagesCounter = 0;
                }
                else
                {
                    throw new NullReferenceException(
                        $"нету сообщений почему то в {nameof(AutoMessagesHandler)}"
                    );
                }
            }
            catch (Exception exception)
            {
                logger.LogException(exception);
            }
        });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!client.IsConnected)
        {
            client.OnConnected += Connect;
        }
        else
        {
            Connect(client, new OnConnectedArgs());
        }

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += OnMessageReceived;
        });
        return Task.CompletedTask;

        void Connect(object? sender, OnConnectedArgs onConnectedArgs)
        {
            if (
                !client.JoinedChannels.Any(e =>
                    e.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                client.JoinChannel(Channel);
                client.OnConnected -= Connect;
            }
        }
    }
}
