// ReSharper disable InconsistentNaming

using System.Text.Json.Serialization;

namespace MARS.Server.Configuration;

public class HoyolabConfiguration
{
    public static readonly string Section = "Hoyo";

    [JsonPropertyName("ltmid_v2")]
    public required string Ltmid_v2 { get; set; }

    [JsonPropertyName("ltoken_v2")]
    public required string Ltoken_v2 { get; set; }

    [JsonPropertyName("ltuid_v2")]
    public required string Ltuid_v2 { get; set; }
}
