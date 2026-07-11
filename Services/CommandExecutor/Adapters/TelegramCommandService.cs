using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Сервис для обработки команд в Telegram
/// </summary>
public class TelegramCommandService(
    ICommandService commandService,
    ITelegramBotClient botClient,
    ILogger<TelegramCommandService> logger
) : PlatformCommandServiceBase<long>
{
    private const int InlineCacheTimeSeconds = 45;
    private const int InlineMaxResults = 30;
    private static readonly TimeSpan InlineResultPayloadTtl = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, InlineResultPayload> _inlineResultPayloads = new(
        StringComparer.Ordinal
    );

    public override Platform Platform => Platform.Telegram;

    protected override int DefaultMaxResponseLength => 4096;

    public override char[] CommandPrefixes => ['/'];

    public override IEnumerable<string> UserCommands =>
        commandService.GetUserCommands(Platform.Telegram);

    public override IEnumerable<string> AdminCommands =>
        commandService.GetAdminCommands(Platform.Telegram);

    public override Func<long, bool> IsAdmin { get; } = x => x.Equals(TelegramExstension.Rxdcodx);

    /// <summary>
    /// Проверить, доступна ли команда на платформе Telegram
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public virtual bool IsCommandAvailable(string commandName)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            result = commandService.IsCommandAvailable(commandName, Platform.Telegram);
        }

        return result;
    }

    public override string ValidateResponse(string response)
    {
        var result = response ?? string.Empty;

        if (!string.IsNullOrEmpty(response))
        {
            var maxLength = GetMaxResponseLength();

            if (response.Length > maxLength)
            {
                // Для Telegram используем более аккуратную обрезку
                var truncated = response.Substring(0, maxLength - 10);

                // Ищем последний полный символ (для поддержки Unicode)
                var lastNewLine = truncated.LastIndexOf('\n');
                if (lastNewLine > maxLength - 50) // Если последний перенос строки не слишком далеко
                {
                    truncated = truncated.Substring(0, lastNewLine);
                }

                result = truncated + "\n\n[Сообщение обрезано...]";
            }
        }

        return result;
    }

    public async Task HandMessage(ITelegramBotClient _, Update update)
    {
        if (update?.Type == UpdateType.Message)
        {
            var message = update.Message;

            if (
                message is { Type: MessageType.Text, Text: { } messageText }
                && StartsWithCommandPrefix(messageText)
            )
            {
                await Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        var commandParts = messageText.Split(
                            ' ',
                            2,
                            StringSplitOptions.RemoveEmptyEntries
                        );
                        if (commandParts.Length > 0)
                        {
                            var commandName = NormalizeCommandName(commandParts[0]);
                            var input = commandParts.Length > 1 ? commandParts[1] : "";

                            // Обработка специальной команды commands
                            if (commandName.Equals("commands", StringComparison.OrdinalIgnoreCase))
                            {
                                var includeAdminCommands = IsUserAdmin(message.Chat.Id);

                                var commandsList = GetCommandsList(
                                    message.From?.Id ?? 0,
                                    UserCommands,
                                    AdminCommands,
                                    includeAdminCommands
                                );
                                await SendMessage(message.Chat.Id, commandsList);
                            }
                            // Обработка специальной команды с (краткий список)
                            else if (commandName.Equals("c", StringComparison.OrdinalIgnoreCase))
                            {
                                var includeAdminCommands = IsUserAdmin(message.Chat.Id);

                                var shortCommandsList = GetShortCommandsList(
                                    message.From?.Id ?? 0,
                                    includeAdminCommands
                                );
                                await SendMessage(message.Chat.Id, shortCommandsList);
                            }
                            else
                            {
                                // Проверяем, существует ли команда
                                var commandInfo = commandService.GetCommandParameters(commandName);
                                if (commandInfo is not null)
                                {
                                    // Проверяем количество обязательных параметров
                                    var requiredParams = commandInfo
                                        .Where(p =>
                                            p.Required
                                            && !(
                                                p.Type == nameof(Message)
                                                && p.Name.Equals(
                                                    "message",
                                                    StringComparison.OrdinalIgnoreCase
                                                )
                                            )
                                        )
                                        .ToArray();

                                    var parameters = commandService.ParseParameters(
                                        input,
                                        commandInfo
                                    );

                                    parameters["message"] = message;

                                    if (parameters.Count >= requiredParams.Length)
                                    {
                                        // Проверяем права доступа для админских команд
                                        var isAdminCommand = commandService.IsAdminCommand(
                                            commandName
                                        );
                                        if (!isAdminCommand || IsUserAdmin(message.From?.Id ?? 0))
                                        {
                                            // Выполняем команду через новый сервис
                                            try
                                            {
                                                var result =
                                                    await commandService.ExecuteCommandAsync(
                                                        commandName,
                                                        parameters,
                                                        Platform.Telegram
                                                    );

                                                // Отправляем результат
                                                await SendMessage(message.Chat.Id, result);

                                                logger.LogInformation(
                                                    "Команда '{CommandName}' выполнена пользователем '{Username}' (ID: {UserId}) с результатом: {Result}",
                                                    commandName,
                                                    message.From?.Username ?? "Unknown",
                                                    message.From?.Id,
                                                    result
                                                );
                                            }
                                            catch (Exception ex)
                                            {
                                                logger.LogError(
                                                    ex,
                                                    "Ошибка при выполнении команды '{CommandName}' пользователем '{Username}' (ID: {UserId})",
                                                    commandName,
                                                    message.From?.Username ?? "Unknown",
                                                    message.From?.Id
                                                );
                                                await SendMessage(
                                                    message.Chat.Id,
                                                    $"Ошибка при выполнении команды '{commandName}': {ex.Message}"
                                                );
                                            }
                                        }
                                        else
                                        {
                                            await SendMessage(
                                                message.Chat.Id,
                                                $"Команда '{commandName}' доступна только администраторам."
                                            );
                                        }
                                    }
                                    else
                                    {
                                        var missingParam = requiredParams[parameters.Count];
                                        await SendMessage(
                                            message.Chat.Id,
                                            $"Не хватает параметра '{missingParam.Name}'. Использование: /{commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}"
                                        );
                                    }
                                }
                                else
                                {
                                    await SendMessage(
                                        message.Chat.Id,
                                        $"Команда '{commandName}' не найдена. Используйте /commands для списка доступных команд."
                                    );
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ошибка при обработке команды Telegram");
                        await SendMessage(
                            message.Chat.Id,
                            "Произошла ошибка при выполнении команды. Попробуйте позже."
                        );
                    }
                });
            }
        }
    }

    public async Task HandInlineQuery(ITelegramBotClient _, Update update)
    {
        if (update is { Type: UpdateType.InlineQuery, InlineQuery: { } inlineQuery })
        {
            try
            {
                var results = await BuildInlineResults(inlineQuery);

                await botClient.AnswerInlineQuery(
                    inlineQuery.Id,
                    results,
                    InlineCacheTimeSeconds,
                    true
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка при обработке inline-запроса Telegram от пользователя {UserId}",
                    inlineQuery.From.Id
                );

                await botClient.AnswerInlineQuery(
                    inlineQuery.Id,
                    Array.Empty<InlineQueryResult>(),
                    0,
                    true
                );
            }
        }
    }

    public async Task HandChosenInlineResult(ITelegramBotClient _, Update update)
    {
        if (
            update is
            { Type: UpdateType.ChosenInlineResult, ChosenInlineResult: { } chosenInlineResult }
        )
        {
            try
            {
                var executionResult = await ExecuteInlineCommand(chosenInlineResult);

                if (!string.IsNullOrWhiteSpace(chosenInlineResult.InlineMessageId))
                {
                    var responseText = ValidateResponse(executionResult);
                    await botClient.EditMessageText(
                        inlineMessageId: chosenInlineResult.InlineMessageId,
                        text: responseText
                    );
                }

                logger.LogInformation(
                    "Inline команда выполнена пользователем {UserId}. Query: {Query}",
                    chosenInlineResult.From.Id,
                    chosenInlineResult.Query
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка при выполнении выбранного inline-результата пользователем {UserId}. Query: {Query}",
                    chosenInlineResult.From.Id,
                    chosenInlineResult.Query
                );
            }
        }
    }

    private Task<InlineQueryResult[]> BuildInlineResults(InlineQuery inlineQuery)
    {
        var result = Array.Empty<InlineQueryResult>();

        if (inlineQuery?.From is not null)
        {
            var userId = inlineQuery.From.Id;
            var query = inlineQuery.Query?.Trim() ?? string.Empty;
            var commands = GetInlineCommands(userId);
            var responseItems = new List<InlineQueryResult>();

            if (string.IsNullOrWhiteSpace(query))
            {
                responseItems.AddRange(BuildDefaultInlineResults(commands, userId));
            }
            else
            {
                var mediaResult = BuildMediaInlineResult(query, userId);
                if (mediaResult is not null)
                {
                    responseItems.Add(mediaResult);
                }

                responseItems.AddRange(BuildInlineQueryResults(query, commands, userId));
            }

            if (responseItems.Count > 0)
            {
                result = [.. responseItems.Take(InlineMaxResults)];
            }
            else
            {
                responseItems.Add(
                    new InlineQueryResultArticle(
                        "inline_help",
                        "Команда не найдена",
                        new InputTextMessageContent(
                            "Используйте формат: /команда параметры или начните вводить имя команды."
                        )
                    )
                    {
                        Description = "Попробуйте ввести часть имени команды",
                    }
                );

                result = [.. responseItems];
            }
        }

        return Task.FromResult(result);
    }

    private IEnumerable<InlineQueryResult> BuildDefaultInlineResults(
        IEnumerable<BaseCommand> commands,
        long userId
    )
    {
        var result = new List<InlineQueryResult>();
        CleanupInlineResultPayloads();

        foreach (var command in commands.Take(InlineMaxResults))
        {
            var message = $"/{command.CommandName}";
            var resultId = CreateInlineResultId(userId, message);
            result.Add(
                new InlineQueryResultArticle(
                    resultId,
                    message,
                    new InputTextMessageContent(message)
                )
                {
                    Description = command.Description,
                }
            );
        }

        return result;
    }

    private IEnumerable<InlineQueryResult> BuildInlineQueryResults(
        string query,
        IEnumerable<BaseCommand> commands,
        long userId
    )
    {
        var result = new List<InlineQueryResult>();
        var normalizedQuery = query.Trim();
        CleanupInlineResultPayloads();

        ParseInlineCommand(normalizedQuery, out var commandNameCandidate, out _);

        var directCommand = commands.FirstOrDefault(c =>
            c.CommandName.Equals(commandNameCandidate, StringComparison.OrdinalIgnoreCase)
            || c.Aliases.Contains(commandNameCandidate, StringComparer.OrdinalIgnoreCase)
        );

        if (directCommand is not null)
        {
            var commandText = StartsWithCommandPrefix(normalizedQuery)
                ? normalizedQuery
                : $"/{normalizedQuery}";
            var resultId = CreateInlineResultId(userId, commandText);

            result.Add(
                new InlineQueryResultArticle(
                    resultId,
                    $"Выполнить {commandText}",
                    new InputTextMessageContent(commandText)
                )
                {
                    Description = directCommand.Description,
                }
            );
        }

        var searchTerm = TrimCommandPrefix(normalizedQuery);
        var matchedCommands = commands
            .Where(c =>
                c.CommandName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || c.Aliases.Any(a => a.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                || c.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            )
            .Take(InlineMaxResults);

        foreach (var command in matchedCommands)
        {
            var commandText = $"/{command.CommandName}";
            var resultId = CreateInlineResultId(userId, commandText);
            result.Add(
                new InlineQueryResultArticle(
                    resultId,
                    commandText,
                    new InputTextMessageContent(commandText)
                )
                {
                    Description = command.Description,
                }
            );
        }

        return result;
    }

    private InlineQueryResult? BuildMediaInlineResult(string query, long userId)
    {
        InlineQueryResult? result = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var mediaUrl = ExtractMediaUrl(query);

            if (!string.IsNullOrWhiteSpace(mediaUrl))
            {
                var resultId = CreateInlineResultId(userId, query);

                if (IsGifUrl(mediaUrl))
                {
                    result = new InlineQueryResultGif(resultId, mediaUrl, mediaUrl)
                    {
                        Caption = "GIF из inline-запроса",
                    };
                }
                else if (IsImageUrl(mediaUrl))
                {
                    result = new InlineQueryResultPhoto(resultId, mediaUrl, mediaUrl)
                    {
                        Caption = "Изображение из inline-запроса",
                    };
                }
                else if (IsVideoUrl(mediaUrl))
                {
                    result = new InlineQueryResultVideo(
                        resultId,
                        mediaUrl,
                        "video/mp4",
                        mediaUrl,
                        new InputTextMessageContent(mediaUrl)
                    )
                    {
                        Title = "Видео из inline-запроса",
                        Caption = "Видео из inline-запроса",
                    };
                }
            }
        }

        return result;
    }

    private string ExtractMediaUrl(string query)
    {
        var result = string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var urlCandidate = parts.FirstOrDefault(part =>
                Uri.TryCreate(part, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            );

            if (!string.IsNullOrWhiteSpace(urlCandidate))
            {
                result = urlCandidate;
            }
        }

        return result;
    }

    private bool IsImageUrl(string url)
    {
        return url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsGifUrl(string url)
    {
        return url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsVideoUrl(string url)
    {
        return url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ExecuteInlineCommand(ChosenInlineResult chosenInlineResult)
    {
        var result =
            "Не удалось выполнить inline-команду. Используйте формат: @bot /команда параметры.";

        if (chosenInlineResult.From is not null)
        {
            var query = chosenInlineResult.Query?.Trim() ?? string.Empty;
            var payloadQuery = ResolveInlinePayloadQuery(
                chosenInlineResult.ResultId,
                chosenInlineResult.From.Id
            );

            if (!string.IsNullOrWhiteSpace(payloadQuery))
            {
                query = payloadQuery;
            }

            ParseInlineCommand(query, out var commandName, out var input);

            if (!string.IsNullOrWhiteSpace(commandName))
            {
                var commandInfo = commandService.GetCommandParameters(commandName);
                if (commandInfo is not null)
                {
                    var requiredParams = commandInfo
                        .Where(p =>
                            p.Required
                            && !(
                                p.Type == nameof(Message)
                                && p.Name.Equals("message", StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        .ToArray();

                    var parameters = commandService.ParseParameters(input, commandInfo);

                    if (parameters.Count >= requiredParams.Length)
                    {
                        var isAdminCommand = commandService.IsAdminCommand(commandName);
                        if (!isAdminCommand || IsUserAdmin(chosenInlineResult.From.Id))
                        {
                            result = await commandService.ExecuteCommandAsync(
                                commandName,
                                parameters,
                                Platform.Telegram
                            );
                        }
                        else
                        {
                            result = $"Команда '{commandName}' доступна только администраторам.";
                        }
                    }
                    else
                    {
                        var missingParam = requiredParams[parameters.Count];
                        result =
                            $"Не хватает параметра '{missingParam.Name}'. Использование: /{commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}";
                    }
                }
                else
                {
                    result =
                        $"Команда '{commandName}' не найдена. Используйте /commands для списка доступных команд.";
                }
            }
        }

        return result;
    }

    private string CreateInlineResultId(long userId, string query)
    {
        var resultId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = DateTime.Now.Add(InlineResultPayloadTtl);
        var payload = new InlineResultPayload(userId, query, expiresAtUtc);

        _inlineResultPayloads[resultId] = payload;

        return resultId;
    }

    private string? ResolveInlinePayloadQuery(string? resultId, long userId)
    {
        string? result = null;

        if (!string.IsNullOrWhiteSpace(resultId))
        {
            if (_inlineResultPayloads.TryGetValue(resultId, out var payload))
            {
                if (payload.UserId == userId && payload.ExpiresAtUtc >= DateTime.Now)
                {
                    result = payload.Query;
                }

                _inlineResultPayloads.TryRemove(resultId, out _);
            }
        }

        return result;
    }

    private void CleanupInlineResultPayloads()
    {
        var utcNow = DateTime.Now;

        foreach (var payload in _inlineResultPayloads)
        {
            if (payload.Value.ExpiresAtUtc < utcNow)
            {
                _inlineResultPayloads.TryRemove(payload.Key, out _);
            }
        }
    }

    private void ParseInlineCommand(string query, out string commandName, out string input)
    {
        commandName = string.Empty;
        input = string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var commandParts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (commandParts.Length > 0)
            {
                commandName = NormalizeCommandName(commandParts[0]);

                input = commandParts.Length > 1 ? commandParts[1].Trim() : string.Empty;
            }
        }
    }

    private string NormalizeCommandName(string commandText)
    {
        var result = commandText;

        if (!string.IsNullOrWhiteSpace(result))
        {
            if (StartsWithCommandPrefix(result))
            {
                result = TrimCommandPrefix(result);
            }

            var atIndex = result.IndexOf('@');
            if (atIndex > 0)
            {
                result = result[..atIndex];
            }
        }

        return result;
    }

    private IEnumerable<BaseCommand> GetInlineCommands(long userId)
    {
        var result = commandService.GetInlineCommandsInfo(Platform.Telegram).AsEnumerable();

        if (IsUserAdmin(userId))
        {
            var adminInline = commandService
                .GetAdminCommandsInfo(Platform.Telegram)
                .Where(c =>
                    c.IsVisibleIn(CommandVisibility.Inline)
                    && (c.SupportsInline || c.SupportsMediaInline)
                );

            result = result.Concat(adminInline);
        }

        result = result
            .Where(c => c.IsVisibleIn(CommandVisibility.ShortList))
            .GroupBy(c => c.CommandName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.CommandName, StringComparer.OrdinalIgnoreCase);

        return result;
    }

    /// <summary>
    /// Получить краткий список команд для пользователя
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="includeAdminCommands">Включить админские команды</param>
    /// <returns>Краткий список команд</returns>
    private string GetShortCommandsList(long userId, bool includeAdminCommands = false)
    {
        var isAdmin = IsUserAdmin(userId);

        if (includeAdminCommands && !isAdmin)
        {
            return "У вас нет прав для просмотра админских команд.";
        }

        var commands = new List<string>();

        // Получаем команды через CommandExecutorService и фильтруем по видимости
        var allCommands = commandService.GetUserCommandsInfo(Platform.Telegram);
        var adminCommands = commandService.GetAdminCommandsInfo(Platform.Telegram);

        // Фильтруем пользовательские команды по видимости
        foreach (var command in allCommands)
        {
            if (command.IsVisibleIn(CommandVisibility.ShortList))
            {
                commands.Add($"/{command.CommandName}");
            }
        }

        // Добавляем админские команды если запрошено и пользователь админ
        if (includeAdminCommands && isAdmin)
        {
            foreach (var command in adminCommands)
            {
                if (command.IsVisibleIn(CommandVisibility.ShortList))
                {
                    commands.Add($"/{command.CommandName}");
                }
            }
        }

        if (commands.Count == 0)
        {
            return "Нет доступных команд для вашей роли.";
        }

        var result = "Команды: ";

        result += string.Join(" | ", commands);

        return result;
    }

    private async Task SendMessage(long chatId, string message)
    {
        try
        {
            var validatedMessage = ValidateResponse(message);
            var sentMessage = await botClient.SendMessage(chatId, validatedMessage);

            logger.LogInformation(
                "Сообщение отправлено с id: {SentMessageId}",
                sentMessage.MessageId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке сообщения в Telegram");
        }
    }

    private sealed class InlineResultPayload(long userId, string query, DateTime expiresAtUtc)
    {
        public long UserId { get; } = userId;
        public string Query { get; } = query;
        public DateTime ExpiresAtUtc { get; } = expiresAtUtc;
    }
}
