namespace MARS.Server.Services.CommandExecutor.Commands;

public class SongCommand(StateManager statemanager) : BaseCommand
{
    public override string CommandName => "song";
    public override string Description => "Информация о песне";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var state = statemanager.GetState();

        if (state == null || state.CurrentQueueItem == null || state.State == PlaybackState.Stopped)
        {
            return Task.FromResult("Сейчас ничего не воспроизводится");
        }

        var track = state.CurrentQueueItem.Track;
        if (track == null)
        {
            return Task.FromResult("Нет информации о текущем треке");
        }

        static string FormatTime(TimeSpan t) =>
            t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

        var progress = state.CurrentTrackProgress ?? TimeSpan.Zero;
        var duration = track.Duration;

        var progressText = FormatTime(progress);
        var durationText = duration == TimeSpan.Zero ? "?" : FormatTime(duration);

        var percent =
            duration.TotalSeconds > 0
                ? Math.Clamp(progress.TotalSeconds / duration.TotalSeconds * 100.0, 0.0, 100.0)
                : 0.0;

        var volumeText = state.IsMuted ? "выключен" : $"{Math.Round(state.Volume)}%";

        var requestedBy =
            state.CurrentQueueItem.RequestedByTwitchUser?.DisplayName
            ?? state.CurrentQueueItem.RequestedByTwitchId;

        var title = !string.IsNullOrWhiteSpace(track.Title) ? track.Title : track.TrackName;

        var result = new System.Text.StringBuilder();
        result.AppendLine($"Текущий трек: {title}. ");
        result.AppendLine(
            $"Длительность: {durationText} — прогресс: {progressText} ({Math.Round(percent)}%). "
        );
        result.AppendLine($"Громкость: {volumeText}. ");
        result.Append($"Заказал: {requestedBy}");

        return Task.FromResult(result.ToString());
    }
}
