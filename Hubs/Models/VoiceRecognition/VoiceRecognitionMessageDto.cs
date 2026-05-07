namespace MARS.Server.Hubs.Models.VoiceRecognition;

/// <summary>
/// Message received from voice recognition system.
/// </summary>
public class VoiceRecognitionMessageDto
{
    /// <summary>
    /// Recognized text from speech.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Detected language code ('ru', 'en', etc).
    /// </summary>
    public string Language { get; set; } = "unknown";

    /// <summary>
    /// Recognition confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// ISO 8601 timestamp of recognition.
    /// </summary>
    public string? Timestamp { get; set; }
}
