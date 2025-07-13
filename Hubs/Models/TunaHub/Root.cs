using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class Root
{
    [JsonProperty("searchRequestId")]
    [JsonPropertyName("searchRequestId")]
    public required string SearchRequestId;

    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public required string Text;

    [JsonProperty("misspellCorrected")]
    [JsonPropertyName("misspellCorrected")]
    public bool? MisspellCorrected;

    [JsonProperty("lastPage")]
    [JsonPropertyName("lastPage")]
    public bool? LastPage;

    [JsonProperty("total")]
    [JsonPropertyName("total")]
    public int? Total;

    [JsonProperty("perPage")]
    [JsonPropertyName("perPage")]
    public int? PerPage;

    [JsonProperty("results")]
    [JsonPropertyName("results")]
    public required List<Result> Results;

    [JsonProperty("responseType")]
    [JsonPropertyName("responseType")]
    public required string ResponseType;
}
