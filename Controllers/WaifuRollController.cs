using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Services;
using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WaifuRollController(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<ShikimoriClientOptions> shikiOptions,
    ILogger<WaifuRollController> logger
) : ControllerBase
{
    private readonly string _shikimoriSite = shikiOptions.Value.ShikimoriSite ?? "";

    private static string FixImageUrl(string? imageUrl, string shikimoriSite)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return imageUrl ?? "";
        }

        return imageUrl.StartsWith(shikimoriSite, StringComparison.OrdinalIgnoreCase)
            ? imageUrl
            : shikimoriSite + imageUrl;
    }

    private static string NormalizeImageUrl(string? imageUrl, string shikimoriSite)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return imageUrl ?? "";
        }

        return imageUrl.StartsWith(shikimoriSite, StringComparison.OrdinalIgnoreCase)
            ? imageUrl[shikimoriSite.Length..]
            : imageUrl;
    }

    #region Waifu Endpoints

    [HttpGet("waifus")]
    public async Task<ActionResult<OperationResult<List<WaifuDto>>>> GetAllWaifus(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<WaifuDto>>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var waifus = await db
                .Waifus.AsNoTracking()
                .Include(w => w.Audio)
                .OrderByDescending(w => w.WhenAdded)
                .Select(w => new WaifuDto
                {
                    ShikiId = w.ShikiId,
                    Name = w.Name,
                    Age = w.Age,
                    Anime = w.Anime,
                    Manga = w.Manga,
                    WhenAdded = w.WhenAdded,
                    LastOrder = w.LastOrder,
                    OrderCount = w.OrderCount,
                    IsPrivated = w.IsPrivated,
                    ImageUrl = FixImageUrl(w.ImageUrl, _shikimoriSite),
                    AudioId = w.AudioId,
                    AudioName = w.Audio != null ? w.Audio.Name : null,
                })
                .ToListAsync(cancellationToken);

            result = Ok(OperationResult<List<WaifuDto>>.Ok("Вайфу получены", waifus));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting waifus");
            result = Ok(OperationResult<List<WaifuDto>>.Bad("Ошибка при получении вайфу", []));
        }

        return result;
    }

    [HttpGet("waifus/{shikiId}")]
    public async Task<ActionResult<OperationResult<WaifuDto?>>> GetWaifu(
        string shikiId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<WaifuDto?>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var waifu = await db
                .Waifus.AsNoTracking()
                .Include(w => w.Audio)
                .Where(w => w.ShikiId == shikiId)
                .Select(w => new WaifuDto
                {
                    ShikiId = w.ShikiId,
                    Name = w.Name,
                    Age = w.Age,
                    Anime = w.Anime,
                    Manga = w.Manga,
                    WhenAdded = w.WhenAdded,
                    LastOrder = w.LastOrder,
                    OrderCount = w.OrderCount,
                    IsPrivated = w.IsPrivated,
                    ImageUrl = FixImageUrl(w.ImageUrl, _shikimoriSite),
                    AudioId = w.AudioId,
                    AudioName = w.Audio != null ? w.Audio.Name : null,
                })
                .FirstOrDefaultAsync(cancellationToken);

            result =
                waifu != null
                    ? Ok(OperationResult<WaifuDto?>.Ok("Вайфу найдена", waifu))
                    : Ok(OperationResult<WaifuDto?>.Bad($"Вайфу с ID {shikiId} не найдена", null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting waifu with ShikiId: {ShikiId}", shikiId);
            result = Ok(OperationResult<WaifuDto?>.Bad("Ошибка при получении вайфу", null));
        }

        return result;
    }

    [HttpPost("waifus")]
    public async Task<ActionResult<OperationResult<WaifuDto?>>> CreateWaifu(
        CreateWaifuRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<WaifuDto?>> result;
        try
        {
            if (request == null)
            {
                result = Ok(
                    OperationResult<WaifuDto?>.Bad("Тело запроса не может быть пустым", null)
                );
                return result;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var exists = await db
                .Waifus.AsNoTracking()
                .AnyAsync(w => w.ShikiId == request.ShikiId, cancellationToken);

            if (exists)
            {
                result = Ok(
                    OperationResult<WaifuDto?>.Bad(
                        $"Вайфу с ID {request.ShikiId} уже существует",
                        null
                    )
                );
                return result;
            }

            var waifu = new Waifu
            {
                ShikiId = request.ShikiId,
                Name = request.Name,
                Age = request.Age,
                Anime = request.Anime,
                Manga = request.Manga,
                ImageUrl = NormalizeImageUrl(request.ImageUrl, _shikimoriSite),
                AudioId = request.AudioId,
                WhenAdded = DateTime.Now,
                LastOrder = DateTime.MinValue,
            };

            db.Waifus.Add(waifu);
            await db.SaveChangesAsync(cancellationToken);

            var dto = new WaifuDto
            {
                ShikiId = waifu.ShikiId,
                Name = waifu.Name,
                Age = waifu.Age,
                Anime = waifu.Anime,
                Manga = waifu.Manga,
                WhenAdded = waifu.WhenAdded,
                LastOrder = waifu.LastOrder,
                OrderCount = waifu.OrderCount,
                IsPrivated = waifu.IsPrivated,
                ImageUrl = FixImageUrl(waifu.ImageUrl, _shikimoriSite),
                AudioId = waifu.AudioId,
            };

            result = Ok(OperationResult<WaifuDto?>.Ok("Вайфу успешно создана", dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating waifu");
            result = Ok(OperationResult<WaifuDto?>.Bad("Ошибка при создании вайфу", null));
        }

        return result;
    }

    [HttpPut("waifus/{shikiId}")]
    public async Task<ActionResult<OperationResult<WaifuDto?>>> UpdateWaifu(
        string shikiId,
        UpdateWaifuRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<WaifuDto?>> result;
        try
        {
            if (request == null)
            {
                result = Ok(
                    OperationResult<WaifuDto?>.Bad("Тело запроса не может быть пустым", null)
                );
                return result;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var waifu = await db.Waifus.FirstOrDefaultAsync(
                w => w.ShikiId == shikiId,
                cancellationToken
            );

            if (waifu == null)
            {
                result = Ok(
                    OperationResult<WaifuDto?>.Bad($"Вайфу с ID {shikiId} не найдена", null)
                );
                return result;
            }

            if (request.Name != null)
                waifu.Name = request.Name;
            if (request.Age.HasValue)
                waifu.Age = request.Age.Value;
            if (request.Anime != null)
                waifu.Anime = request.Anime;
            if (request.Manga != null)
                waifu.Manga = request.Manga;
            if (request.ImageUrl != null)
                waifu.ImageUrl = NormalizeImageUrl(request.ImageUrl, _shikimoriSite);
            if (request.IsPrivated.HasValue)
                waifu.IsPrivated = request.IsPrivated.Value;
            if (request.AudioId.HasValue || request.AudioId == null)
                waifu.AudioId = request.AudioId;

            await db.SaveChangesAsync(cancellationToken);

            var dto = new WaifuDto
            {
                ShikiId = waifu.ShikiId,
                Name = waifu.Name,
                Age = waifu.Age,
                Anime = waifu.Anime,
                Manga = waifu.Manga,
                WhenAdded = waifu.WhenAdded,
                LastOrder = waifu.LastOrder,
                OrderCount = waifu.OrderCount,
                IsPrivated = waifu.IsPrivated,
                ImageUrl = FixImageUrl(waifu.ImageUrl, _shikimoriSite),
                AudioId = waifu.AudioId,
            };

            result = Ok(OperationResult<WaifuDto?>.Ok("Вайфу успешно обновлена", dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating waifu with ShikiId: {ShikiId}", shikiId);
            result = Ok(OperationResult<WaifuDto?>.Bad("Ошибка при обновлении вайфу", null));
        }

        return result;
    }

    [HttpDelete("waifus/{shikiId}")]
    public async Task<ActionResult<OperationResult>> DeleteWaifu(
        string shikiId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var waifu = await db.Waifus.FirstOrDefaultAsync(
                w => w.ShikiId == shikiId,
                cancellationToken
            );

            if (waifu == null)
            {
                result = Ok(OperationResult.Bad($"Вайфу с ID {shikiId} не найдена"));
                return result;
            }

            db.Waifus.Remove(waifu);
            await db.SaveChangesAsync(cancellationToken);

            result = Ok(OperationResult.Ok("Вайфу успешно удалена"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting waifu with ShikiId: {ShikiId}", shikiId);
            result = Ok(OperationResult.Bad("Ошибка при удалении вайфу"));
        }

        return result;
    }

    #endregion

    #region Husband Endpoints

    [HttpGet("husbands")]
    public async Task<ActionResult<OperationResult<List<HusbandDto>>>> GetAllHusbands(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<HusbandDto>>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var husbands = await db
                .Husbands.AsNoTracking()
                .Include(h => h.TwitchUser)
                .OrderByDescending(h => h.WhenOrdered)
                .Select(h => new HusbandDto
                {
                    TwitchId = h.TwitchId,
                    DisplayName = h.TwitchUser != null ? h.TwitchUser.DisplayName : null,
                    ProfileImageUrl = h.TwitchUser != null ? h.TwitchUser.ProfileImageUrl : null,
                    WhenOrdered = h.WhenOrdered,
                    WaifuBrideId = h.WaifuBrideId,
                    IsPrivated = h.IsPrivated,
                    OrderCount = h.OrderCount,
                    WaifuRollId = h.WaifuRollId,
                    WhenPrivated = h.WhenPrivated,
                    LastWeddingCongratulatedMonths = h.LastWeddingCongratulatedMonths,
                })
                .ToListAsync(cancellationToken);

            result = Ok(OperationResult<List<HusbandDto>>.Ok("Мужи получены", husbands));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting husbands");
            result = Ok(OperationResult<List<HusbandDto>>.Bad("Ошибка при получении мужей", []));
        }

        return result;
    }

    [HttpGet("husbands/{twitchId}")]
    public async Task<ActionResult<OperationResult<HusbandDto?>>> GetHusband(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<HusbandDto?>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var husband = await db
                .Husbands.AsNoTracking()
                .Include(h => h.TwitchUser)
                .Where(h => h.TwitchId == twitchId)
                .Select(h => new HusbandDto
                {
                    TwitchId = h.TwitchId,
                    DisplayName = h.TwitchUser != null ? h.TwitchUser.DisplayName : null,
                    ProfileImageUrl = h.TwitchUser != null ? h.TwitchUser.ProfileImageUrl : null,
                    WhenOrdered = h.WhenOrdered,
                    WaifuBrideId = h.WaifuBrideId,
                    IsPrivated = h.IsPrivated,
                    OrderCount = h.OrderCount,
                    WaifuRollId = h.WaifuRollId,
                    WhenPrivated = h.WhenPrivated,
                    LastWeddingCongratulatedMonths = h.LastWeddingCongratulatedMonths,
                })
                .FirstOrDefaultAsync(cancellationToken);

            result =
                husband != null
                    ? Ok(OperationResult<HusbandDto?>.Ok("Муж найден", husband))
                    : Ok(OperationResult<HusbandDto?>.Bad($"Муж с ID {twitchId} не найден", null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting husband with TwitchId: {TwitchId}", twitchId);
            result = Ok(OperationResult<HusbandDto?>.Bad("Ошибка при получении мужа", null));
        }

        return result;
    }

    [HttpPut("husbands/{twitchId}")]
    public async Task<ActionResult<OperationResult<HusbandDto?>>> UpdateHusband(
        string twitchId,
        UpdateHusbandRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<HusbandDto?>> result;
        try
        {
            if (request == null)
            {
                result = Ok(
                    OperationResult<HusbandDto?>.Bad("Тело запроса не может быть пустым", null)
                );
                return result;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var husband = await db
                .Husbands.Include(h => h.TwitchUser)
                .FirstOrDefaultAsync(h => h.TwitchId == twitchId, cancellationToken);

            if (husband == null)
            {
                result = Ok(
                    OperationResult<HusbandDto?>.Bad($"Муж с ID {twitchId} не найден", null)
                );
                return result;
            }

            if (request.WaifuBrideId != null || request.WaifuBrideId == null)
                husband.WaifuBrideId = request.WaifuBrideId;
            if (request.IsPrivated.HasValue)
                husband.IsPrivated = request.IsPrivated.Value;
            if (request.WaifuRollId != null || request.WaifuRollId == null)
                husband.WaifuRollId = request.WaifuRollId;
            if (request.WhenPrivated.HasValue || request.WhenPrivated == null)
                husband.WhenPrivated = request.WhenPrivated;
            if (request.LastWeddingCongratulatedMonths.HasValue)
                husband.LastWeddingCongratulatedMonths = request.LastWeddingCongratulatedMonths;

            await db.SaveChangesAsync(cancellationToken);

            var dto = new HusbandDto
            {
                TwitchId = husband.TwitchId,
                DisplayName = husband.TwitchUser?.DisplayName,
                ProfileImageUrl = husband.TwitchUser?.ProfileImageUrl,
                WhenOrdered = husband.WhenOrdered,
                WaifuBrideId = husband.WaifuBrideId,
                IsPrivated = husband.IsPrivated,
                OrderCount = husband.OrderCount,
                WaifuRollId = husband.WaifuRollId,
                WhenPrivated = husband.WhenPrivated,
                LastWeddingCongratulatedMonths = husband.LastWeddingCongratulatedMonths,
            };

            result = Ok(OperationResult<HusbandDto?>.Ok("Муж успешно обновлен", dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating husband with TwitchId: {TwitchId}", twitchId);
            result = Ok(OperationResult<HusbandDto?>.Bad("Ошибка при обновлении мужа", null));
        }

        return result;
    }

    [HttpDelete("husbands/{twitchId}")]
    public async Task<ActionResult<OperationResult>> DeleteHusband(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var husband = await db.Husbands.FirstOrDefaultAsync(
                h => h.TwitchId == twitchId,
                cancellationToken
            );

            if (husband == null)
            {
                result = Ok(OperationResult.Bad($"Муж с ID {twitchId} не найден"));
                return result;
            }

            db.Husbands.Remove(husband);
            await db.SaveChangesAsync(cancellationToken);

            result = Ok(OperationResult.Ok("Муж успешно удален"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting husband with TwitchId: {TwitchId}", twitchId);
            result = Ok(OperationResult.Bad("Ошибка при удалении мужа"));
        }

        return result;
    }

    [HttpPost("husbands/{twitchId}/unmerge")]
    public async Task<ActionResult<OperationResult<object?>>> UnmergeHusband(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<object?>> result;
        try
        {
            if (!int.TryParse(twitchId, out var id))
            {
                result = Ok(OperationResult<object?>.Bad("TwitchId должен быть числовым", null));
                return result;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var host = await db
                .Husbands.Include(h => h.TwitchUser)
                .FirstOrDefaultAsync(h => h.TwitchId == id.ToString(), cancellationToken);

            if (host is not { IsPrivated: true })
            {
                result = Ok(OperationResult<object?>.Bad("Муж не найден или не в браке", null));
                return result;
            }

            var waifu = await db.Waifus.FirstOrDefaultAsync(
                w => w.ShikiId == host.WaifuBrideId,
                cancellationToken
            );

            if (waifu is not { IsPrivated: true })
            {
                result = Ok(
                    OperationResult<object?>.Bad(
                        $"Не удалось найти вайфу этого мужа ({host.TwitchId})",
                        null
                    )
                );
                return result;
            }

            waifu.IsPrivated = false;
            host.WaifuBrideId = null;
            host.IsPrivated = false;
            host.WhenPrivated = null;

            db.Waifus.Update(waifu);
            db.Husbands.Update(host);
            await db.SaveChangesAsync(cancellationToken);

            var data = new
            {
                host.TwitchId,
                hostDisplayName = host.TwitchUser?.DisplayName,
                waifuId = waifu.ShikiId,
                waifuName = waifu.Name,
            };

            result = Ok(
                OperationResult<object?>.Ok(
                    $"Развод между {host.TwitchUser?.DisplayName} и {waifu.Name} состоялся",
                    data
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unmerging husband with TwitchId: {TwitchId}", twitchId);
            result = Ok(OperationResult<object?>.Bad("Ошибка при разводе", null));
        }

        return result;
    }

    #endregion

    #region Audio Endpoints

    [HttpGet("audios")]
    public async Task<ActionResult<OperationResult<List<WaifuRollAudioDto>>>> GetAllAudios(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<WaifuRollAudioDto>>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var audios = await db
                .WaifuRollAudios.AsNoTracking()
                .OrderBy(a => a.Name)
                .Select(a => new WaifuRollAudioDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    FileExtension = a.FileExtension,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            result = Ok(OperationResult<List<WaifuRollAudioDto>>.Ok("Аудио получены", audios));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting audio tracks");
            result = Ok(
                OperationResult<List<WaifuRollAudioDto>>.Bad("Ошибка при получении аудио", [])
            );
        }

        return result;
    }

    [HttpPost("audios")]
    public async Task<ActionResult<OperationResult<WaifuRollAudioDto?>>> UploadAudio(
        IFormFile file,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<WaifuRollAudioDto?>> result;
        try
        {
            if (file == null || file.Length == 0)
            {
                result = Ok(
                    OperationResult<WaifuRollAudioDto?>.Bad("Файл не может быть пустым", null)
                );
                return result;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                result = Ok(
                    OperationResult<WaifuRollAudioDto?>.Bad("Имя не может быть пустым", null)
                );
                return result;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac"))
            {
                result = Ok(
                    OperationResult<WaifuRollAudioDto?>.Bad(
                        "Поддерживаемые форматы: mp3, wav, ogg, m4a, flac",
                        null
                    )
                );
                return result;
            }

            byte[] audioData;
            await using (var stream = file.OpenReadStream())
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                audioData = ms.ToArray();
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var audio = new WaifuRollAudio
            {
                Name = name,
                AudioData = audioData,
                FileExtension = extension,
            };

            db.WaifuRollAudios.Add(audio);
            await db.SaveChangesAsync(cancellationToken);

            var dto = new WaifuRollAudioDto
            {
                Id = audio.Id,
                Name = audio.Name,
                FileExtension = audio.FileExtension,
                CreatedAt = audio.CreatedAt,
            };

            result = Ok(OperationResult<WaifuRollAudioDto?>.Ok("Аудио успешно загружено", dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading audio");
            result = Ok(OperationResult<WaifuRollAudioDto?>.Bad("Ошибка при загрузке аудио", null));
        }

        return result;
    }

    [HttpGet("audios/{id:guid}/stream")]
    public async Task<IActionResult> StreamAudio(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var audio = await db
                .WaifuRollAudios.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (audio == null)
            {
                return NotFound();
            }

            var contentType = audio.FileExtension.ToLowerInvariant() switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                ".flac" => "audio/flac",
                _ => "application/octet-stream",
            };

            return File(audio.AudioData, contentType, $"{audio.Name}{audio.FileExtension}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming audio with Id: {Id}", id);
            return StatusCode(500);
        }
    }

    [HttpDelete("audios/{id:guid}")]
    public async Task<ActionResult<OperationResult>> DeleteAudio(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var audio = await db.WaifuRollAudios.FirstOrDefaultAsync(
                a => a.Id == id,
                cancellationToken
            );

            if (audio == null)
            {
                result = Ok(OperationResult.Bad("Аудио не найдено"));
                return result;
            }

            db.WaifuRollAudios.Remove(audio);
            await db.SaveChangesAsync(cancellationToken);

            result = Ok(OperationResult.Ok("Аудио успешно удалено"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting audio with Id: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении аудио"));
        }

        return result;
    }

    #endregion
}
