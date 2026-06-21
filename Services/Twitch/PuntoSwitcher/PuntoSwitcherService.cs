using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch.PuntoSwitcher;

public class PuntoSwitcherService : BackgroundService, IPuntoSwitcherService
{
    private readonly ITwitchClient? _twitchClient;
    private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;

    public bool IsFilterEnabled { get; set; } = true;

    public PuntoSwitcherService() { }

    public PuntoSwitcherService(
        ITwitchClient twitchClient,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _twitchClient = twitchClient;
        _dbContextFactory = dbContextFactory;
    }

    private static readonly HashSet<string> ProtectedTokens =
    [
        "Kappa",
        "PogChamp",
        "KEKW",
        "LUL",
        "OMEGALUL",
        "monkaS",
        "PepeHands",
        "EZ",
        "GG",
        "GLHF",
        "F",
    ];

    private static readonly HashSet<char> LatinVowels = ['a', 'e', 'i', 'o', 'u', 'y'];
    private static readonly HashSet<char> CyrillicVowels =
    [
        'а',
        'е',
        'ё',
        'и',
        'о',
        'у',
        'ы',
        'э',
        'ю',
        'я',
    ];

    private static readonly Dictionary<char, char> EnToRuMap = new()
    {
        ['q'] = 'й',
        ['w'] = 'ц',
        ['e'] = 'у',
        ['r'] = 'к',
        ['t'] = 'е',
        ['y'] = 'н',
        ['u'] = 'г',
        ['i'] = 'ш',
        ['o'] = 'щ',
        ['p'] = 'з',
        ['['] = 'х',
        [']'] = 'ъ',
        ['a'] = 'ф',
        ['s'] = 'ы',
        ['d'] = 'в',
        ['f'] = 'а',
        ['g'] = 'п',
        ['h'] = 'р',
        ['j'] = 'о',
        ['k'] = 'л',
        ['l'] = 'д',
        [';'] = 'ж',
        ['\''] = 'э',
        ['z'] = 'я',
        ['x'] = 'ч',
        ['c'] = 'с',
        ['v'] = 'м',
        ['b'] = 'и',
        ['n'] = 'т',
        ['m'] = 'ь',
        [','] = 'б',
        ['.'] = 'ю',
        ['`'] = 'ё',
    };

    private static readonly Dictionary<char, char> RuToEnMap = EnToRuMap.ToDictionary(
        pair => pair.Value,
        pair => pair.Key
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_dbContextFactory is not null)
        {
            await InitializePuntoSwitcherStateAsync(stoppingToken);
        }

        if (_twitchClient is not null)
        {
            _twitchClient.OnMessageReceived += OnMessageReceived;
            stoppingToken.Register(() => _twitchClient.OnMessageReceived -= OnMessageReceived);
        }
    }

    private async Task InitializePuntoSwitcherStateAsync(CancellationToken stoppingToken)
    {
        await using var db = await _dbContextFactory!.CreateDbContextAsync(stoppingToken);
        var rootStateValue = await db
            .RootState.AsNoTracking()
            .Where(e => e.Name == RootStateKeys.PuntoSwitcherFilterEnabled)
            .Select(e => e.Value)
            .FirstOrDefaultAsync(stoppingToken);

        if (bool.TryParse(rootStateValue, out var isEnabled))
        {
            IsFilterEnabled = isEnabled;
        }
    }

    private Task OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if (IsFilterEnabled)
        {
            if (
                args.ChatMessage.Channel.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                )
                && !TwitchExstension.BlackList.Logins.Any(t =>
                    t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var fixedMessage = TryFixMessage(args.ChatMessage.Message);
                if (fixedMessage is { Success: true, Data.HasChanges: true })
                {
                    TryOverrideMessage(args.ChatMessage, fixedMessage.Data.CorrectedMessage);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static ChatMessage TryOverrideMessage(ChatMessage source, string correctedMessage)
    {
        var result = source;

        if (!string.IsNullOrWhiteSpace(correctedMessage))
        {
            var backingField = source
                .GetType()
                .GetField(
                    "<Message>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

            backingField?.SetValue(source, correctedMessage);

            result = source;
        }

        return result;
    }

    public OperationResult<PuntoSwitchSuggestion> TryFixMessage(string? message)
    {
        var result = OperationResult<PuntoSwitchSuggestion>.Bad("Не удалось обработать сообщение");

        if (!string.IsNullOrWhiteSpace(message))
        {
            var trimmed = message.Trim();
            var defaultSuggestion = new PuntoSwitchSuggestion
            {
                OriginalMessage = message,
                CorrectedMessage = message,
                ReplacedTokens = 0,
            };

            if (ShouldTryFixMessage(trimmed))
            {
                var correction = BuildCorrection(message);

                if (correction.HasChanges)
                {
                    result = OperationResult<PuntoSwitchSuggestion>.Ok(
                        "Сообщение исправлено",
                        correction
                    );
                }
                else
                {
                    result = OperationResult<PuntoSwitchSuggestion>.Ok(
                        "Исправление не требуется",
                        defaultSuggestion
                    );
                }
            }
            else
            {
                result = OperationResult<PuntoSwitchSuggestion>.Ok(
                    "Сообщение пропущено по правилам",
                    defaultSuggestion
                );
            }
        }
        else
        {
            result = OperationResult<PuntoSwitchSuggestion>.Bad("Сообщение пустое");
        }

        return result;
    }

    private static bool ShouldTryFixMessage(string message)
    {
        var result = false;

        if (!message.StartsWith('!') && !message.StartsWith('/') && !message.StartsWith('.'))
        {
            if (
                !message.Contains("http://", StringComparison.OrdinalIgnoreCase)
                && !message.Contains("https://", StringComparison.OrdinalIgnoreCase)
            )
            {
                result = true;
            }
        }

        return result;
    }

    private static PuntoSwitchSuggestion BuildCorrection(string message)
    {
        var result = new PuntoSwitchSuggestion
        {
            OriginalMessage = message,
            CorrectedMessage = message,
            ReplacedTokens = 0,
        };

        var parts = message.Split(' ');
        if (parts.Length > 0)
        {
            var replaced = 0;

            for (var i = 0; i < parts.Length; i++)
            {
                var sourceToken = parts[i];
                var fixedToken = TryFixToken(sourceToken);
                if (!string.Equals(sourceToken, fixedToken, StringComparison.Ordinal))
                {
                    parts[i] = fixedToken;
                    replaced++;
                }
            }

            if (replaced > 0)
            {
                result = new PuntoSwitchSuggestion
                {
                    OriginalMessage = message,
                    CorrectedMessage = string.Join(' ', parts),
                    ReplacedTokens = replaced,
                };
            }
        }

        return result;
    }

    private static string TryFixToken(string token)
    {
        var result = token;

        if (IsConvertibleToken(token))
        {
            var prefixLength = GetLeadingNonWordLength(token);
            var suffixLength = GetTrailingNonWordLength(token);
            var coreLength = token.Length - prefixLength - suffixLength;

            if (coreLength > 1)
            {
                var prefix = token[..prefixLength];
                var core = token.Substring(prefixLength, coreLength);
                var suffix = token[(prefixLength + coreLength)..];

                if (!ProtectedTokens.Contains(core))
                {
                    var converted = ConvertCore(core);
                    if (
                        !string.Equals(core, converted, StringComparison.Ordinal)
                        && LooksLikeMistypedLayout(core, converted)
                    )
                    {
                        result = $"{prefix}{converted}{suffix}";
                    }
                }
            }
        }

        return result;
    }

    private static bool IsConvertibleToken(string token)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(token) && token.Length >= 3)
        {
            if (!token.StartsWith('@') && !token.StartsWith('#'))
            {
                if (
                    !token.Contains("http://", StringComparison.OrdinalIgnoreCase)
                    && !token.Contains("https://", StringComparison.OrdinalIgnoreCase)
                    && !token.Contains("www.", StringComparison.OrdinalIgnoreCase)
                )
                {
                    result = token.Any(char.IsLetter) && !token.Any(char.IsDigit);
                }
            }
        }

        return result;
    }

    private static int GetLeadingNonWordLength(string token)
    {
        var result = 0;

        while (result < token.Length && !char.IsLetter(token[result]))
        {
            result++;
        }

        return result;
    }

    private static int GetTrailingNonWordLength(string token)
    {
        var result = 0;

        while (result < token.Length && !char.IsLetter(token[token.Length - 1 - result]))
        {
            result++;
        }

        return result;
    }

    private static string ConvertCore(string core)
    {
        var result = core;
        var latinCount = core.Count(IsLatinLetter);
        var cyrillicCount = core.Count(IsCyrillicLetter);

        if (latinCount > 0 && cyrillicCount == 0)
        {
            result = ConvertByMap(core, EnToRuMap);
        }
        else if (cyrillicCount > 0 && latinCount == 0)
        {
            result = ConvertByMap(core, RuToEnMap);
        }

        return result;
    }

    private static bool LooksLikeMistypedLayout(string source, string converted)
    {
        var result = false;

        if (!string.Equals(source, converted, StringComparison.Ordinal))
        {
            var sourceLatin = source.Count(IsLatinLetter);
            var sourceCyrillic = source.Count(IsCyrillicLetter);
            var convertedLatin = converted.Count(IsLatinLetter);
            var convertedCyrillic = converted.Count(IsCyrillicLetter);

            if (sourceLatin > 0 && sourceCyrillic == 0 && convertedCyrillic > 0)
            {
                var sourceVowelRate = GetVowelRate(source, LatinVowels);
                var convertedVowelRate = GetVowelRate(converted, CyrillicVowels);
                result = sourceVowelRate < 0.2 && convertedVowelRate >= 0.25;
            }
            else if (sourceCyrillic > 0 && sourceLatin == 0 && convertedLatin > 0)
            {
                var sourceVowelRate = GetVowelRate(source, CyrillicVowels);
                var convertedVowelRate = GetVowelRate(converted, LatinVowels);
                result = sourceVowelRate < 0.2 && convertedVowelRate >= 0.25;
            }
        }

        return result;
    }

    private static double GetVowelRate(string token, HashSet<char> vowels)
    {
        var result = 0d;
        var letters = token.Where(char.IsLetter).ToArray();

        if (letters.Length > 0)
        {
            var vowelCount = letters.Count(ch => vowels.Contains(char.ToLowerInvariant(ch)));
            result = (double)vowelCount / letters.Length;
        }

        return result;
    }

    private static string ConvertByMap(string text, IReadOnlyDictionary<char, char> map)
    {
        var result = text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var builder = new StringBuilder(text.Length);

            foreach (var symbol in text)
            {
                var lower = char.ToLowerInvariant(symbol);
                if (map.TryGetValue(lower, out var mapped))
                {
                    builder.Append(char.IsUpper(symbol) ? char.ToUpperInvariant(mapped) : mapped);
                }
                else
                {
                    builder.Append(symbol);
                }
            }

            result = builder.ToString();
        }

        return result;
    }

    private static bool IsLatinLetter(char value)
    {
        var result = (value is >= 'A' and <= 'Z') || (value is >= 'a' and <= 'z');
        return result;
    }

    private static bool IsCyrillicLetter(char value)
    {
        var result = (value is >= 'А' and <= 'я') || value is 'Ё' or 'ё';
        return result;
    }
}
