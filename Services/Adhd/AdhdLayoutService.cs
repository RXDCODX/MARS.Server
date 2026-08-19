using MARS.Server.DataBaseContext;
using MARS.Server.Services.Adhd.Entities;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Adhd;

public class AdhdLayoutService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<AdhdLayoutConfigDto> GetCurrentConfigAsync()
    {
        var result = CreateDefaultConfig();

        await using var context = await factory.CreateDbContextAsync();

        var config = await context.AdhdLayoutConfigs.AsNoTracking().SingleOrDefaultAsync();

        if (config is not null)
        {
            result = MapToDto(config);
        }

        return result;
    }

    public async Task<AdhdLayoutConfigDto> UpdateConfigAsync(AdhdLayoutConfigDto? dto)
    {
        var result = CreateDefaultConfig();

        if (dto is not null)
        {
            await using var context = await factory.CreateDbContextAsync();

            var config = await context.AdhdLayoutConfigs.SingleOrDefaultAsync();

            if (config is null)
            {
                config = new AdhdLayoutConfig();
                context.AdhdLayoutConfigs.Add(config);
            }

            MapToEntity(dto, config);
            config.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
            result = dto;
        }

        return result;
    }

    private static AdhdLayoutConfigDto MapToDto(AdhdLayoutConfig config)
    {
        return new AdhdLayoutConfigDto
        {
            ShowRainEffect = config.ShowRainEffect,
            ShowDVDLogos = config.ShowDVDLogos,
            ShowBreakingNews = config.ShowBreakingNews,
            ShowStreamerVideo = config.ShowStreamerVideo,
            ShowFitnessVideo = config.ShowFitnessVideo,
            ShowGTAVideo = config.ShowGTAVideo,
            ShowHydraulicMobileVideo = config.ShowHydraulicMobileVideo,
            ShowSlimeVideo = config.ShowSlimeVideo,
            ShowMukbangVideo = config.ShowMukbangVideo,
            ShowQuiz = config.ShowQuiz,
            ShowSurfer = config.ShowSurfer,
            ShowLOFIGirl = config.ShowLOFIGirl,
            ShowCatisa = config.ShowCatisa,
            ShowNotifications = config.ShowNotifications,
        };
    }

    private static void MapToEntity(AdhdLayoutConfigDto dto, AdhdLayoutConfig config)
    {
        config.ShowRainEffect = dto.ShowRainEffect;
        config.ShowDVDLogos = dto.ShowDVDLogos;
        config.ShowBreakingNews = dto.ShowBreakingNews;
        config.ShowStreamerVideo = dto.ShowStreamerVideo;
        config.ShowFitnessVideo = dto.ShowFitnessVideo;
        config.ShowGTAVideo = dto.ShowGTAVideo;
        config.ShowHydraulicMobileVideo = dto.ShowHydraulicMobileVideo;
        config.ShowSlimeVideo = dto.ShowSlimeVideo;
        config.ShowMukbangVideo = dto.ShowMukbangVideo;
        config.ShowQuiz = dto.ShowQuiz;
        config.ShowSurfer = dto.ShowSurfer;
        config.ShowLOFIGirl = dto.ShowLOFIGirl;
        config.ShowCatisa = dto.ShowCatisa;
        config.ShowNotifications = dto.ShowNotifications;
    }

    private static AdhdLayoutConfigDto CreateDefaultConfig()
    {
        return new AdhdLayoutConfigDto();
    }
}
