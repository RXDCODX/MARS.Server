using System;
using System.Text.Json.Serialization;

namespace MARS.Server.Hubs.Models.LoggerHub;

public class LogMessageDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; set; }

    [JsonPropertyName("logLevel")]
    public required string LogLevel { get; set; }

    [JsonPropertyName("category")]
    public required string Category { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }

    [JsonPropertyName("eventId")]
    public int? EventId { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
}
