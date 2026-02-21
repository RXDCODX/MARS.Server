namespace MARS.Server.Configuration;

public class MultilingualTtsConfiguration
{
    public const string SectionName = "MultilingualTts";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "local-sherpa-cli";
    public string BaseUrl { get; set; } = string.Empty;
    public string SynthesisPath { get; set; } = "/api/tts";
    public string DefaultLanguage { get; set; } = "ru";
    public string DefaultSpeaker { get; set; } = string.Empty;
    public string AudioFormat { get; set; } = "wav";
    public string ApiKeyHeader { get; set; } = "X-API-Key";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;

    public string LocalSherpaExecutablePath { get; set; } =
        "tools/tts/sherpa/sherpa-onnx-v1.12.25-win-x64-shared-MT-Release/bin/sherpa-onnx-offline-tts.exe";

    public string LocalModelsRootPath { get; set; } = "models/tts";
}