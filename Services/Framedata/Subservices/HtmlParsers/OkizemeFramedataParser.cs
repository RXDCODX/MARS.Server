using System.Text.Json;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Subservices.Entitys;

namespace MARS.Server.Services.Framedata.Subservices.HtmlParsers;

/// <summary>
/// Парсер фреймдаты для сайта Okizeme (JSON API)
/// </summary>
public class OkizemeFramedataParser : BaseFramedataParser
{
    private readonly Uri _basePath = new("https://okizeme.gg");
    private readonly HttpClient _httpClient;

    public OkizemeFramedataParser(
        ILogger logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        FramedataStagingService? stagingService,
        CancellationToken cancellationToken,
        FramedataParserOptions? options = null
    )
        : base(logger, dbContextFactory, stagingService, cancellationToken, options)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Options.HttpTimeoutSeconds) };
    }

    public override async Task<List<string>> ParseCharactersAndMoves(
        List<string>? characterNamesToParse = null
    )
    {
        var parsedCharacters = new List<string>();
        var characterSlugs = await GetCharacterSlugs();

        foreach (var slug in characterSlugs)
        {
            await DelayBetweenRequests();

            var name = NormalizeCharacterName(slug);
            if (
                string.IsNullOrWhiteSpace(name)
                || name.Equals("mokujin", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            if (
                characterNamesToParse != null
                && !characterNamesToParse.Contains(name, StringComparer.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            try
            {
                var character = CreateCharacterFromSlug(slug);

                if (Options.ParseMoves)
                {
                    var moveList = await GetMoveList(character);
                    var consolidatedMoves = await ConsolidateMoveGroups(moveList);
                    await SaveCharacterAndMoves(character, consolidatedMoves);
                }
                else
                {
                    await SaveCharacter(character);
                }

                parsedCharacters.Add(character.Name);
                Logger.LogInformation(
                    "Успешно распарсил персонажа из Okizeme: {CharacterName}",
                    character.Name
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Ошибка при парсинге персонажа из Okizeme: {CharacterName}",
                    name
                );
            }

            await DelayBetweenCharacters();
        }

        return parsedCharacters;
    }

    public override async Task<List<string>> ParseCharactersOnly(
        List<string>? characterNamesToParse = null
    )
    {
        var originalParseMoves = Options.ParseMoves;
        Options.ParseMoves = false;

        try
        {
            return await ParseCharactersAndMoves(characterNamesToParse);
        }
        finally
        {
            Options.ParseMoves = originalParseMoves;
        }
    }

    public override async Task<List<Move>> GetMoveList(TekkenCharacter character)
    {
        var moveList = new List<Move>();
        var slug = ToSlug(character.Name);
        var endpoint = new Uri(_basePath, $"/api/{slug}");

        try
        {
            var json = await _httpClient.GetStringAsync(endpoint, CancellationToken);
            var apiMoves = JsonSerializer.Deserialize<List<OkizemeMoveDto>>(json);

            if (apiMoves == null || apiMoves.Count == 0)
            {
                Logger.LogWarning(
                    "Okizeme вернул пустой мувлист для персонажа {CharacterName}",
                    character.Name
                );
                return moveList;
            }

            foreach (var apiMove in apiMoves)
            {
                if (string.IsNullOrWhiteSpace(apiMove.Command))
                {
                    continue;
                }

                var normalizedCommand = NormalizeCommand(apiMove.Command);

                if (string.IsNullOrWhiteSpace(normalizedCommand))
                {
                    continue;
                }

                var notes = BuildNotes(apiMove);
                var move = new Move
                {
                    Character = character,
                    CharacterName = character.Name,
                    Command = normalizedCommand,
                    HitLevel = apiMove.HitLevel?.Trim().ToLower(),
                    Damage = apiMove.Damage?.Trim().ToLower(),
                    StartUpFrame = apiMove.Startup?.Trim().ToLower(),
                    BlockFrame = apiMove.Block?.Trim().ToLower(),
                    HitFrame = apiMove.Hit?.Trim().ToLower(),
                    CounterHitFrame = apiMove.Counter?.Trim().ToLower(),
                    VideoUrl = BuildMoveVideoUrl(character.PageUrl, apiMove.Command),
                    Notes = notes.Length > 0 ? notes : null,
                };

                ParseMoveProperties(move, apiMove.Tags);
                moveList.Add(move);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Ошибка при получении мувлиста из Okizeme для персонажа {CharacterName}",
                character.Name
            );
        }

        return moveList;
    }

    private async Task<List<string>> GetCharacterSlugs()
    {
        var result = new List<string>();

        try
        {
            var sitemap = await _httpClient.GetStringAsync(
                new Uri(_basePath, "/sitemap-0.xml"),
                CancellationToken
            );

            var matches = System.Text.RegularExpressions.Regex.Matches(
                sitemap,
                @"<loc>https://okizeme\.gg/database/([^<]+)</loc>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var slug = match.Groups[1].Value.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    result.Add(slug);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при получении списка персонажей из sitemap Okizeme");
        }

        return [.. result.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private TekkenCharacter CreateCharacterFromSlug(string slug)
    {
        var normalizedSlug = slug.Trim().ToLower();
        var characterName = NormalizeCharacterName(normalizedSlug);

        return new TekkenCharacter
        {
            Name = characterName,
            LinkToImage = new Uri(
                _basePath,
                $"/assets/images/{normalizedSlug}-portrait.png"
            ).AbsoluteUri,
            PageUrl = new Uri(_basePath, $"/database/{normalizedSlug}").AbsoluteUri,
        };
    }

    private static string NormalizeCharacterName(string slug)
    {
        return slug.Equals("jack-8", StringComparison.OrdinalIgnoreCase)
            ? "jack 8"
            : slug.Replace('-', ' ').Trim().ToLower();
    }

    private static string ToSlug(string characterName)
    {
        var normalizedName = characterName.Trim().ToLower();
        return normalizedName.Equals("jack 8", StringComparison.OrdinalIgnoreCase)
            ? "jack-8"
            : normalizedName.Replace(' ', '-');
    }

    private static string NormalizeCommand(string command)
    {
        return command.Trim().ToLower().Replace('.', ' ');
    }

    private static string? BuildMoveVideoUrl(string pageUrl, string? sourceCommand)
    {
        var result = default(string?);

        if (!string.IsNullOrWhiteSpace(pageUrl) && !string.IsNullOrWhiteSpace(sourceCommand))
        {
            var commandHash = Uri.EscapeDataString(sourceCommand.Trim());
            result = $"{pageUrl}#{commandHash}";
        }

        return result;
    }

    private static string[] BuildNotes(OkizemeMoveDto move)
    {
        var notes = new List<string>();

        if (!string.IsNullOrWhiteSpace(move.Notes))
        {
            var lines = move
                .Notes.Split(
                    '\n',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Select(e => e.TrimStart('*', ' ').Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.ToLower());

            notes.AddRange(lines);
        }

        if (!string.IsNullOrWhiteSpace(move.Transitions))
        {
            notes.Add($"transitions: {move.Transitions.ToLower()}");
        }

        return [.. notes.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void ParseMoveProperties(Move move, string? tags)
    {
        var notes = move.Notes != null ? string.Join(" ", move.Notes) : string.Empty;
        var normalizedTags = tags?.ToLower() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(notes) || !string.IsNullOrWhiteSpace(normalizedTags))
        {
            var combined = $"{notes} {normalizedTags}";

            if (combined.Contains("power crush") || combined.Contains("pc"))
            {
                move.PowerCrush = true;
            }

            if (combined.Contains("heat burst") || combined.Contains("hb"))
            {
                move.HeatBurst = true;
            }

            if (combined.Contains("heat engager") || combined.Contains("he"))
            {
                move.HeatEngage = true;
            }

            if (combined.Contains("heat smash") || combined.Contains("hs"))
            {
                move.HeatSmash = true;
            }

            if (combined.Contains("tornado") || combined.Contains("trn"))
            {
                move.Tornado = true;
            }

            if (combined.Contains("homing") || combined.Contains("hom"))
            {
                move.Homing = true;
            }

            if (move.Command.StartsWith('h'))
            {
                move.RequiresHeat = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(move.HitLevel))
        {
            if (move.HitLevel.Contains('t') || move.HitLevel.Contains("th"))
            {
                move.Throw = true;
            }
        }

        var pair = Aliases.Stances.FirstOrDefault(
            e => move.Command.StartsWith(e.Key, StringComparison.OrdinalIgnoreCase),
            new KeyValuePair<string, string>(string.Empty, string.Empty)
        );

        if (!string.IsNullOrWhiteSpace(pair.Key))
        {
            move.StanceCode = pair.Key;
            move.StanceName = pair.Value;
        }
    }

    private sealed class OkizemeMoveDto
    {
        public string? Command { get; set; }
        public string? HitLevel { get; set; }
        public string? Damage { get; set; }
        public string? Startup { get; set; }
        public string? Block { get; set; }
        public string? Hit { get; set; }
        public string? Counter { get; set; }
        public string? Notes { get; set; }
        public string? Tags { get; set; }
        public string? Transitions { get; set; }
    }
}
