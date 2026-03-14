using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.YouTube;

namespace MARS.Server.Services.Discord.PlayRequest;

public class DiscordPlayRequestService(
    IDiscordGatewayService gatewayService,
    YouTubeResolver youTubeResolver,
    DiscordPlayAudioCacheService audioCacheService,
    ILogger<DiscordPlayRequestService> logger
) : IHostedService
{
    private const string PlayCommandName = "play";
    private const string QueryOptionName = "query";
    private const string PlayComponentPrefix = "discord-play:";
    private const int MaxSearchResults = 10;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, DiscordPlaySelectionSession> _sessions =
        new(StringComparer.Ordinal);

    private bool _isHandlerRegistered;
    private bool _isSlashCommandRegistered;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_isHandlerRegistered)
        {
            gatewayService.RegisterInteractionCreatedHandler(HandleInteractionCreatedAsync);
            gatewayService.RegisterComponentInteractionCreatedHandler(
                HandleComponentInteractionCreatedAsync
            );
            _isHandlerRegistered = true;
        }

        if (!_isSlashCommandRegistered)
        {
            await RegisterSlashCommandsAsync(cancellationToken);
            _isSlashCommandRegistered = true;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessions.Clear();

        return Task.CompletedTask;
    }

    public async Task<bool> TryHandleMessageAsync(
        DiscordClient client,
        MessageCreatedEventArgs args,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        CleanupExpiredSessions();

        if (args.Author is not null && !args.Author.IsBot)
        {
            var messageText = (args.Message.Content ?? string.Empty).Trim();

            if (TryGetPlayQuery(messageText, out var query))
            {
                result = true;

                if (!string.IsNullOrWhiteSpace(query))
                {
                    await CreateSelectionMessageAsync(
                        client,
                        args.Channel,
                        args.Author.Id,
                        query,
                        cancellationToken
                    );
                }
                else
                {
                    await args.Channel.SendMessageAsync(
                        "Использование: /play поисковый запрос. После этого выбери трек в dropdown ниже."
                    );
                }
            }
        }

        return result;
    }

    private async Task HandleInteractionCreatedAsync(
        DiscordClient client,
        InteractionCreatedEventArgs args
    )
    {
        CleanupExpiredSessions();

        var interaction = args.Interaction;
        if (
            interaction.Type == DiscordInteractionType.ApplicationCommand
            && TryGetSlashPlayQuery(interaction, out var query)
        )
        {
            await CreateSlashSelectionMessageAsync(interaction, query);
        }
    }

    private async Task HandleComponentInteractionCreatedAsync(
        DiscordClient client,
        ComponentInteractionCreatedEventArgs args
    )
    {
        var customId = args.Interaction.Data.CustomId ?? string.Empty;
        if (TryGetSessionId(customId, out var sessionId))
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.IsExpired(SessionLifetime))
                {
                    _sessions.TryRemove(sessionId, out _);

                    var expiredBuilder = new DiscordInteractionResponseBuilder();
                    expiredBuilder.WithContent(BuildExpiredMessage(session));
                    expiredBuilder.ClearComponents();

                    await args.Interaction.CreateResponseAsync(
                        DiscordInteractionResponseType.UpdateMessage,
                        expiredBuilder
                    );
                }
                else if (args.User.Id != session.UserId)
                {
                    var forbiddenBuilder = new DiscordInteractionResponseBuilder();
                    forbiddenBuilder.WithContent("Выбирать трек может только автор этого запроса.");
                    forbiddenBuilder.AsEphemeral(true);

                    await args.Interaction.CreateResponseAsync(
                        DiscordInteractionResponseType.ChannelMessageWithSource,
                        forbiddenBuilder
                    );
                }
                else
                {
                    await ProcessSelectionAsync(client, args, session);
                }
            }
            else
            {
                var missingBuilder = new DiscordInteractionResponseBuilder();
                missingBuilder.WithContent("Выбор устарел. Запусти /play ещё раз.");
                missingBuilder.AsEphemeral(true);

                await args.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.ChannelMessageWithSource,
                    missingBuilder
                );
            }
        }
    }

    private async Task CreateSelectionMessageAsync(
        DiscordClient client,
        DiscordChannel channel,
        ulong userId,
        string query,
        CancellationToken cancellationToken
    )
    {
        var tracks = await youTubeResolver.SearchTracksAsync(query, MaxSearchResults, cancellationToken);

        if (tracks.Length > 0)
        {
            var session = CreateSession(channel.Id, userId, query, tracks);
            using var builder = BuildMessageBuilder(session);
            var message = await client.SendMessageAsync(channel, builder);

            session.MessageId = message.Id;
            _sessions[session.SessionId] = session;
        }
        else
        {
            await channel.SendMessageAsync("Ничего не нашёл по этому запросу.");
        }
    }

    private async Task CreateSlashSelectionMessageAsync(DiscordInteraction interaction, string query)
    {
        var tracks = await youTubeResolver.SearchTracksAsync(query, MaxSearchResults, CancellationToken.None);

        if (tracks.Length > 0)
        {
            var session = CreateSession(interaction.ChannelId, interaction.User.Id, query, tracks);
            var builder = BuildInteractionResponseBuilder(session);

            await interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                builder
            );

            var originalMessage = await interaction.GetOriginalResponseAsync();
            session.MessageId = originalMessage.Id;
            _sessions[session.SessionId] = session;
        }
        else
        {
            var notFoundBuilder = new DiscordInteractionResponseBuilder();
            notFoundBuilder.WithContent("Ничего не нашёл по этому запросу.");
            notFoundBuilder.AsEphemeral(true);

            await interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                notFoundBuilder
            );
        }
    }

    private async Task ProcessSelectionAsync(
        DiscordClient client,
        ComponentInteractionCreatedEventArgs args,
        DiscordPlaySelectionSession session
    )
    {
        var selectedIndex = ParseSelectedIndex(args.Values);

        if (selectedIndex >= 0 && selectedIndex < session.Tracks.Count)
        {
            _sessions.TryRemove(session.SessionId, out _);

            var selectedTrack = session.Tracks[selectedIndex];
            var updateBuilder = new DiscordInteractionResponseBuilder();
            updateBuilder.WithContent(BuildSelectedMessage(session, selectedTrack));
            updateBuilder.ClearComponents();

            await args.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.UpdateMessage,
                updateBuilder
            );

            await SendTrackFileAsync(
                client,
                args.Interaction,
                args.Channel,
                args.Message.Id,
                selectedTrack,
                CancellationToken.None
            );
        }
        else
        {
            var invalidBuilder = new DiscordInteractionResponseBuilder();
            invalidBuilder.WithContent("Не удалось определить выбранный трек.");
            invalidBuilder.AsEphemeral(true);

            await args.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                invalidBuilder
            );
        }
    }

    private async Task SendTrackFileAsync(
        DiscordClient client,
        DiscordInteraction interaction,
        DiscordChannel channel,
        ulong replyMessageId,
        BaseTrackInfo track,
        CancellationToken cancellationToken
    )
    {
        var attachmentLimit = ResolveAttachmentLimit(interaction.AttachmentSizeLimit);
        var preparedAudioResult = await audioCacheService.PrepareAudioAsync(
            track,
            attachmentLimit,
            cancellationToken
        );

        if (preparedAudioResult.Success)
        {
            try
            {
                await using var fileStream = new FileStream(
                    preparedAudioResult.Data.FilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                using var builder = new DiscordMessageBuilder();

                builder.WithContent($"Вот аудиодорожка: {TrimText(track.Title, 120)}");
                builder.WithReply(replyMessageId, false, false);
                builder.AddFile(preparedAudioResult.Data.FileName, fileStream, true);

                await client.SendMessageAsync(channel, builder);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка отправки Discord play аудиофайла для {VideoId}", track.VideoId);

                var followupBuilder = new DiscordFollowupMessageBuilder();
                followupBuilder.WithContent(
                    "Аудиофайл подготовился, но не отправился в канал. Попробуй ещё раз позже."
                );
                followupBuilder.AsEphemeral(true);

                await interaction.CreateFollowupMessageAsync(followupBuilder);
            }
        }
        else
        {
            var followupBuilder = new DiscordFollowupMessageBuilder();
            followupBuilder.WithContent(
                string.IsNullOrWhiteSpace(preparedAudioResult.Message)
                    ? "Не удалось подготовить файл для отправки."
                    : preparedAudioResult.Message
            );
            followupBuilder.AsEphemeral(true);

            await interaction.CreateFollowupMessageAsync(followupBuilder);
        }
    }

    private async Task RegisterSlashCommandsAsync(CancellationToken cancellationToken)
    {
        var client = await gatewayService.EnsureConnectedAsync(cancellationToken);

        if (client is not null)
        {
            var playCommand = BuildPlayCommand();

            foreach (var guild in client.Guilds.Values)
            {
                try
                {
                    var existingCommands = await guild.GetApplicationCommandsAsync(false);
                    var existingPlayCommand = existingCommands.FirstOrDefault(command =>
                        command.Name.Equals(PlayCommandName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (existingPlayCommand is null)
                    {
                        await guild.CreateApplicationCommandAsync(playCommand);
                    }
                    else if (!IsSamePlayCommand(existingPlayCommand))
                    {
                        await client.DeleteGuildApplicationCommandAsync(
                            guild.Id,
                            existingPlayCommand.Id
                        );
                        await guild.CreateApplicationCommandAsync(playCommand);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Не удалось зарегистрировать slash-команду /play для guild {GuildId}",
                        guild.Id
                    );
                }
            }
        }
    }

    private static DiscordApplicationCommand BuildPlayCommand()
    {
        var queryOption = new DiscordApplicationCommandOption(
            QueryOptionName,
            "Поисковый запрос для YouTube",
            DiscordApplicationCommandOptionType.String,
            true
        );
        var result = new DiscordApplicationCommand(
            PlayCommandName,
            "Найти 10 треков на YouTube и выбрать один",
            [queryOption]
        );

        return result;
    }

    private static bool IsSamePlayCommand(DiscordApplicationCommand command)
    {
        var result = false;

        if (command.Name.Equals(PlayCommandName, StringComparison.OrdinalIgnoreCase))
        {
            var option = command.Options.FirstOrDefault();
            result =
                command.Description == "Найти 10 треков на YouTube и выбрать один"
                && command.Options.Count == 1
                && option is not null
                && option.Name == QueryOptionName
                && option.Type == DiscordApplicationCommandOptionType.String
                && option.Required == true;
        }

        return result;
    }

    private static DiscordPlaySelectionSession CreateSession(
        ulong channelId,
        ulong userId,
        string query,
        IReadOnlyList<BaseTrackInfo> tracks
    )
    {
        var result = new DiscordPlaySelectionSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            UserId = userId,
            Query = query,
            Tracks = tracks,
        };

        return result;
    }

    private static DiscordMessageBuilder BuildMessageBuilder(DiscordPlaySelectionSession session)
    {
        var result = new DiscordMessageBuilder();

        result.WithContent(BuildSearchResultsMessage(session));
        result.AddActionRowComponent(BuildSelectComponent(session));

        return result;
    }

    private static DiscordInteractionResponseBuilder BuildInteractionResponseBuilder(
        DiscordPlaySelectionSession session
    )
    {
        var result = new DiscordInteractionResponseBuilder();

        result.WithContent(BuildSearchResultsMessage(session));
        result.AddActionRowComponent(BuildSelectComponent(session));

        return result;
    }

    private static DiscordSelectComponent BuildSelectComponent(DiscordPlaySelectionSession session)
    {
        var options = session
            .Tracks.Select((track, index) =>
                new DiscordSelectComponentOption(
                    TrimText(string.Concat(index + 1, ". ", track.TrackName), 100),
                    index.ToString(CultureInfo.InvariantCulture),
                    TrimText(BuildOptionDescription(track), 100)
                )
            )
            .ToArray();
        var result = new DiscordSelectComponent(
            string.Concat(PlayComponentPrefix, session.SessionId),
            "Выбери трек",
            options,
            false,
            1,
            1
        );

        return result;
    }

    private void CleanupExpiredSessions()
    {
        foreach (var session in _sessions)
        {
            if (session.Value.IsExpired(SessionLifetime))
            {
                _sessions.TryRemove(session.Key, out _);
            }
        }
    }

    private static bool TryGetPlayQuery(string messageText, out string query)
    {
        var result = false;
        query = string.Empty;

        if (!string.IsNullOrWhiteSpace(messageText) && (messageText.StartsWith('/') || messageText.StartsWith('!')))
        {
            var commandParts = messageText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (commandParts.Length > 0)
            {
                var commandName = commandParts[0].TrimStart('/', '!');
                if (commandName.Equals(PlayCommandName, StringComparison.OrdinalIgnoreCase))
                {
                    result = true;

                    if (commandParts.Length > 1)
                    {
                        query = commandParts[1].Trim();
                    }
                }
            }
        }

        return result;
    }

    private static bool TryGetSlashPlayQuery(DiscordInteraction interaction, out string query)
    {
        var result = false;
        query = string.Empty;

        if (
            interaction.Data is not null
            && interaction.Data.Name.Equals(PlayCommandName, StringComparison.OrdinalIgnoreCase)
        )
        {
            var queryOption = interaction.Data.Options?.FirstOrDefault(option =>
                option.Name.Equals(QueryOptionName, StringComparison.OrdinalIgnoreCase)
            );

            if (queryOption?.Value is string stringValue)
            {
                query = stringValue.Trim();
            }
            else if (queryOption?.Value is not null)
            {
                query = queryOption.Value.ToString()?.Trim() ?? string.Empty;
            }

            result = true;
        }

        return result;
    }

    private static bool TryGetSessionId(string customId, out string sessionId)
    {
        var result = false;
        sessionId = string.Empty;

        if (!string.IsNullOrWhiteSpace(customId) && customId.StartsWith(PlayComponentPrefix))
        {
            sessionId = customId[PlayComponentPrefix.Length..];
            result = !string.IsNullOrWhiteSpace(sessionId);
        }

        return result;
    }

    private static int ParseSelectedIndex(IReadOnlyList<string> values)
    {
        var result = -1;

        if (values.Count > 0)
        {
            var rawValue = values[0];
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                result = index;
            }
        }

        return result;
    }

    private static long ResolveAttachmentLimit(long attachmentSizeLimit)
    {
        var result = DiscordPlayAudioCacheService.DefaultMaxAttachmentSizeBytes;

        if (attachmentSizeLimit > 0)
        {
            result = attachmentSizeLimit;
        }

        return result;
    }

    private static string BuildSearchResultsMessage(DiscordPlaySelectionSession session)
    {
        var result = "Ничего не найдено.";

        if (session.Tracks.Count > 0)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Найдено {session.Tracks.Count} треков по запросу: {session.Query}");

            for (var index = 0 ; index < session.Tracks.Count ; index++)
            {
                var track = session.Tracks[index];
                builder.Append(index + 1);
                builder.Append(". ");
                builder.Append(TrimText(track.Title, 110));
                builder.Append(" [");
                builder.Append(FormatDuration(track.Duration));
                builder.AppendLine("]");
            }

            builder.Append("Выбери трек в dropdown ниже. Список живёт 10 минут.");

            result = builder.ToString();
        }

        return result;
    }

    private static string BuildSelectedMessage(
        DiscordPlaySelectionSession session,
        BaseTrackInfo selectedTrack
    )
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Запрос: {session.Query}");
        builder.Append("Выбран трек: ");
        builder.Append(TrimText(selectedTrack.Title, 140));
        builder.Append(" [");
        builder.Append(FormatDuration(selectedTrack.Duration));
        builder.Append(']');
        builder.AppendLine();
        builder.Append("Готовлю аудиофайл...");

        return builder.ToString();
    }

    private static string BuildExpiredMessage(DiscordPlaySelectionSession session)
    {
        var result = string.Concat(
            "Список по запросу '",
            session.Query,
            "' устарел. Запусти /play ещё раз."
        );

        return result;
    }

    private static string BuildOptionDescription(BaseTrackInfo track)
    {
        var author = track.Authors?.FirstOrDefault();
        var durationText = FormatDuration(track.Duration);
        var result = !string.IsNullOrWhiteSpace(author)
            ? string.Concat(author, " | ", durationText)
            : durationText;

        return result;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var result = "??:??";

        if (duration > TimeSpan.Zero)
        {
            result = duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                : $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
        }

        return result;
    }

    private static string TrimText(string text, int maxLength)
    {
        var result = string.Empty;

        if (!string.IsNullOrWhiteSpace(text))
        {
            result = text.Trim();
            if (result.Length > maxLength)
            {
                result = string.Concat(result[..(maxLength - 3)].TrimEnd(), "...");
            }
        }

        return result;
    }
}