using System.Globalization;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Text;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

[SupportedOSPlatform("windows")]
public class SyntheziaVoicer : IVoicer
{
    private readonly Dictionary<string, InstalledVoice> _linkedVoices = [];
    private readonly ILogger<IVoicer> _logger;
    private readonly SpeechSynthesizer _speechSynthesizer = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    public bool IsActive { get; set; }

    public SyntheziaVoicer(ILogger<IVoicer> logger)
    {
        _logger = logger;
        if (OperatingSystem.IsWindows())
        {
            _speechSynthesizer.SetOutputToDefaultAudioDevice();
        }
    }

    public int GetVolume()
    {
        return _speechSynthesizer.Volume;
    }

    public void ChangeVolume(int volume)
    {
        if (OperatingSystem.IsWindows())
        {
            _speechSynthesizer.Volume = volume;
        }
    }

    public async Task Sound(MessageToSynthezid message)
    {
        if (!OperatingSystem.IsWindows() || !IsActive)
        {
            return;
        }

        try
        {
            var isExist = _linkedVoices.ContainsKey(message.Name);

            var sb = new StringBuilder();

            foreach (var word in message.Message.Split(' '))
            {
                if (Uri.TryCreate(word, UriKind.Absolute, out var result))
                {
                    sb.Append(result.Host);
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(word);
                    sb.Append(' ');
                }
            }

            await _semaphore.WaitAsync();

            if (isExist)
            {
                var isGet = _linkedVoices.TryGetValue(message.Name, out var voice);
                if (isGet && voice is not null)
                {
                    var builder = new PromptBuilder();
                    builder.StartVoice(voice.VoiceInfo.Name);
                    builder.AppendText(sb.ToString());
                    builder.EndVoice();
                    _speechSynthesizer.SpeakAsync(builder);
                }
            }
            else
            {
                var voices = _speechSynthesizer.GetInstalledVoices(new CultureInfo("ru-RU"));
                var index = Random.Shared.Next(voices.Count);
                var voice = voices[index];
                _linkedVoices.Add(message.Name, voice);
                var builder = new PromptBuilder();
                builder.StartVoice(voice.VoiceInfo.Name);
                builder.AppendText(
                    $"Привет, {message.Name}! Для тебя был выбран голос {voice.VoiceInfo.Name}"
                );
                builder.AppendBreak(TimeSpan.FromSeconds(1));
                builder.AppendText(sb.ToString());
                builder.EndVoice();

                _speechSynthesizer.SpeakAsync(builder);
            }

            _semaphore.Release();
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
        }
    }

    public Task Stop()
    {
        return Task.FromResult(_speechSynthesizer.SpeakAsyncCancelAll);
    }

    public Task Block()
    {
        IsActive = false;

        return Stop();
    }

    public Task Unlock()
    {
        IsActive = true;
        return Task.CompletedTask;
    }
}
