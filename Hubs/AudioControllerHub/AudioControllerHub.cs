using MARS.Server.Exstensions;
using MARS.Server.Services.Discord.TtsVoiceRelay;
using MARS.Shared.Hubs;
using MARS.Shared.Models.WaifuChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Hubs.AudioControllerHub;

[AllowAnonymous]
[SignalRHub("/hubs/audio-controller", AutoDiscover.MethodsAndParams)]
public class AudioControllerHub(
    AudioControllerCommandTracker tracker,
    IDiscordTtsVoiceRelayService discordRelayService,
    ITwitchClient client,
    ILogger<AudioControllerHub> logger
) : Hub<IAudioControllerHub>
{
    private const string GroupName = "audio-controllers";

    public async Task RegisterAsAudioController()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        logger.LogInformation("AudioController registered: {ConnectionId}", Context.ConnectionId);
    }

    public Task CommandResponse(string correlationId, bool success, string? data, string? error)
    {
        tracker.TryComplete(
            correlationId,
            new AudioControllerResponse
            {
                CorrelationId = correlationId,
                Success = success,
                Data = data,
                Error = error,
            }
        );
        return Task.CompletedTask;
    }

    public async Task SubmitAudioForRelay(
        byte[] pcmAudio,
        int sampleRate,
        int channels,
        string text
    )
    {
        logger.LogInformation(
            "Received audio for relay: {Text}, {SampleRate}Hz, {Channels}ch, {Size}B",
            text,
            sampleRate,
            channels,
            pcmAudio.Length
        );
        await discordRelayService.HandleRelayedAudioAsync(pcmAudio, sampleRate, channels, text);
    }

    public async Task WaifuChatResponse(WaifuChatResponse response)
    {
        logger.LogInformation(
            "WaifuChatResponse: {CorrelationId}, TwitchId={TwitchId}, Response={Response}",
            response.CorrelationId,
            response.TwitchId,
            response.Response
        );

        if (!string.IsNullOrWhiteSpace(response.Response))
        {
            if (!string.IsNullOrWhiteSpace(response.MessageId))
            {
                await client.SendReplyAsync(
                    response.TwitchId,
                    response.MessageId,
                    response.Response
                );
            }
            else
            {
                await client.SendMessageToMainTwitchAsync(
                    $"@{response.TwitchId}, {response.Response}",
                    logger
                );
            }
        }
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation(
            "Client connected to AudioControllerHub: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
        logger.LogInformation(
            "Client disconnected from AudioControllerHub: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnDisconnectedAsync(exception);
    }
}
