using FFMpegCore;

namespace MARS.Server.Services.Media;

public class FfprobeMediaInspector : IMediaInspector
{
    private readonly ILogger<FfprobeMediaInspector> _logger;

    public FfprobeMediaInspector(ILogger<FfprobeMediaInspector> logger)
    {
        _logger = logger;
    }

    public async Task<(long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate)> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = (BitrateKbps: (long?)null, AverageFrameRate: (double?)null, RawFrameRate: (double?)null);

        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try
            {
                var analysis = await FFProbe.AnalyseAsync(filePath, cancellationToken: cancellationToken);
                var primaryVideoStream = analysis.PrimaryVideoStream;
                var primaryAudioStream = analysis.PrimaryAudioStream;

                if (primaryVideoStream is not null)
                {
                    result = (
                        BitrateKbps: primaryVideoStream.BitRate > 0 ? primaryVideoStream.BitRate / 1000 : null,
                        AverageFrameRate: primaryVideoStream.AverageFrameRate is > 0 ? primaryVideoStream.AverageFrameRate : null,
                        RawFrameRate: primaryVideoStream.FrameRate is > 0 ? primaryVideoStream.FrameRate : null
                    );
                }
                else if (primaryAudioStream is not null)
                {
                    result = (
                        BitrateKbps: primaryAudioStream.BitRate > 0 ? primaryAudioStream.BitRate / 1000 : null,
                        AverageFrameRate: null,
                        RawFrameRate: null
                    );
                }
                else if (analysis.Format.BitRate > 0)
                {
                    result = (BitrateKbps: (long)Math.Round(analysis.Format.BitRate / 1000d), AverageFrameRate: null, RawFrameRate: null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ffprobe analysis failed for file {FilePath}", filePath);
            }
        }

        return result;
    }

}
