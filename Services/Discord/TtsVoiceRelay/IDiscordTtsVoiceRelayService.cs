namespace MARS.Server.Services.Discord.TtsVoiceRelay;

public interface IDiscordTtsVoiceRelayService
{
    bool IsVoiceRoutingEnabled { get; }

    Task HandleRelayedAudioAsync(
        byte[] pcmAudio,
        int sampleRate,
        int channels,
        string text,
        CancellationToken cancellationToken = default
    );
}
