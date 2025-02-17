using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages;

public class AutoMessagesController
{
    private const string Channel = TwitchExstension.Channel;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<AutoMessagesController> _logger;

    /// <summary>
    /// Не делать меньше 2
    /// </summary>
    private const int Capacity = 3;
    private readonly Queue<AutoMessage> _queue = new(Capacity);

    public AutoMessagesController(
        ITwitchClient client,
        ILogger<AutoMessagesController> logger,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _client = client;
        _logger = logger;
        _dbContextFactory = dbContextFactory;

        if (
            !_client.JoinedChannels.Any(e =>
                e.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            _client.JoinChannel(Channel);
        }
    }

    private int MessagesCounter { get; set; }
    private DateTimeOffset LastPostDateTime { get; set; } = DateTimeOffset.MinValue;

    public async void OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        await Task.Run(async () =>
        {
            if (args.ChatMessage.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase))
            {
                MessagesCounter++;

                if (
                    MessagesCounter >= 70
                    && LastPostDateTime.Add(TimeSpan.FromMinutes(45)) < DateTimeOffset.Now
                )
                {
                    try
                    {
                        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                        var messages = dbContext
                            .AutoMessages.AsNoTracking()
                            .AsEnumerable()
                            .Where(e => _queue.All(message => message.Id != e.Id))
                            .ToArray();

                        if (messages.Length != 0)
                        {
                            var index = Random.Shared.Next(0, messages.Length - 1);
                            var message = messages.ElementAt(index);

                            _client.SendMessage(Channel, message.Message);

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
                                $"нету сообщений почему то в {nameof(AutoMessagesController)}"
                            );
                        }
                    }
                    catch (Exception exception)
                    {
                        _logger.LogException(exception);
                    }
                }
            }
        });
    }
}
