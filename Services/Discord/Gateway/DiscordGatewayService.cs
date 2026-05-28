using System.Collections.Generic;
using System.Threading;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerDiscordConfiguration = MARS.Server.Configuration.DiscordConfiguration;

namespace MARS.Server.Services.Discord.Gateway;

public class DiscordGatewayService(
    IOptions<ServerDiscordConfiguration> configuration,
    ILogger<DiscordGatewayService> logger
) : IDiscordGatewayService, IHostedService
{
    private readonly ServerDiscordConfiguration _configuration = configuration.Value;
    private readonly List<Func<DiscordClient, MessageCreatedEventArgs, Task>> _messageHandlers = [];
    private readonly List<
        Func<DiscordClient, VoiceStateUpdatedEventArgs, Task>
    > _voiceStateHandlers = [];
    private readonly List<
        Func<DiscordClient, InteractionCreatedEventArgs, Task>
    > _interactionHandlers = [];
    private readonly List<
        Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task>
    > _componentInteractionHandlers = [];
    private readonly Lock _handlersLock = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public bool IsConnected { get; private set; }

    public DiscordClient? Client { get; private set; }

    public void RegisterMessageCreatedHandler(
        Func<DiscordClient, MessageCreatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _messageHandlers.Add(handler);
        }
    }

    public void RegisterVoiceStateUpdatedHandler(
        Func<DiscordClient, VoiceStateUpdatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _voiceStateHandlers.Add(handler);
        }
    }

    public void RegisterInteractionCreatedHandler(
        Func<DiscordClient, InteractionCreatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _interactionHandlers.Add(handler);
        }
    }

    public void RegisterComponentInteractionCreatedHandler(
        Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _componentInteractionHandlers.Add(handler);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Token))
        {
            logger.LogWarning("Discord token не задан, интеграция Discord отключена");
        }
        else
        {
            await EnsureConnectedAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Client is not null)
        {
            try
            {
                await Client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка при отключении Discord клиента");
            }
            finally
            {
                IsConnected = false;
                Client = null;
            }
        }
    }

    public async Task<OperationResult> SendMessageAsync(
        ulong channelId,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Discord клиент недоступен");

        if (channelId != 0 && !string.IsNullOrWhiteSpace(message))
        {
            try
            {
                var client = await EnsureConnectedAsync(cancellationToken);
                if (client is not null)
                {
                    var channel = await client.GetChannelAsync(channelId);
                    if (channel is not null)
                    {
                        await channel.SendMessageAsync(message);
                        result = OperationResult.Ok("Сообщение отправлено в Discord");
                    }
                    else
                    {
                        result = OperationResult.Bad("Discord канал не найден");
                    }
                }
                else
                {
                    result = OperationResult.Bad("Не удалось инициализировать Discord клиент");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка отправки сообщения в Discord канал {ChannelId}",
                    channelId
                );
                result = OperationResult.Bad($"Ошибка Discord отправки: {ex.Message}");
            }
        }
        else
        {
            result = OperationResult.Bad("Неверные параметры для отправки в Discord");
        }

        return result;
    }

    public async Task<DiscordClient?> EnsureConnectedAsync(
        CancellationToken cancellationToken = default
    )
    {
        DiscordClient? result = Client;

        if (result is null)
        {
            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                result = Client;
                if (result is null)
                {
                    var intents =
                        DiscordIntents.Guilds
                        | DiscordIntents.GuildMessages
                        | DiscordIntents.MessageContents
                        | DiscordIntents.GuildVoiceStates;

                    var builder = DiscordClientBuilder
                        .CreateDefault(_configuration.Token, intents)
                        .ConfigureEventHandlers(events =>
                        {
                            events.HandleMessageCreated(HandleMessageCreatedAsync);
                            events.HandleVoiceStateUpdated(HandleVoiceStateUpdatedAsync);
                            events.HandleInteractionCreated(HandleInteractionCreatedAsync);
                            events.HandleComponentInteractionCreated(
                                HandleComponentInteractionCreatedAsync
                            );
                        })
                        .UseVoiceNext(new VoiceNextConfiguration())
                        .SetLogLevel(LogLevel.Information);

                    result = builder.Build();
                    await result.ConnectAsync();

                    Client = result;
                    IsConnected = true;

                    logger.LogInformation("Discord клиент подключен");
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                _connectLock.Release();
            }
        }

        return result;
    }

    private async Task HandleMessageCreatedAsync(DiscordClient client, MessageCreatedEventArgs args)
    {
        List<Func<DiscordClient, MessageCreatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _messageHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки MessageCreated обработчиком");
            }
        }
    }

    private async Task HandleVoiceStateUpdatedAsync(
        DiscordClient client,
        VoiceStateUpdatedEventArgs args
    )
    {
        List<Func<DiscordClient, VoiceStateUpdatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _voiceStateHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки VoiceStateUpdated обработчиком");
            }
        }
    }

    private async Task HandleInteractionCreatedAsync(
        DiscordClient client,
        InteractionCreatedEventArgs args
    )
    {
        List<Func<DiscordClient, InteractionCreatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _interactionHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки InteractionCreated обработчиком");
            }
        }
    }

    private async Task HandleComponentInteractionCreatedAsync(
        DiscordClient client,
        ComponentInteractionCreatedEventArgs args
    )
    {
        List<Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _componentInteractionHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки ComponentInteractionCreated обработчиком");
            }
        }
    }
}
