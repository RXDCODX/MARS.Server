using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.Annotations;

namespace MARS.Server.Services.Framedata.Entitys;

/// <summary>
/// Represents a move in Tekken, including its command, properties, and effects.
/// </summary>
public class Move
{
    [Key]
    public required string CharacterName { get; set; }

    [Key]
    public required string Command { get; set; }

    [JsonIgnore]
    [SwaggerIgnore]
    public TekkenCharacter? Character { get; set; }
    public bool IsFromStance => !string.IsNullOrWhiteSpace(StanceCode);
    public string StanceCode { get; set; } = string.Empty;
    public string? StanceName { get; set; } = string.Empty;
    public bool HeatEngage { get; set; }
    public bool HeatSmash { get; set; }
    public bool PowerCrush { get; set; }
    public bool Throw { get; set; }
    public bool Homing { get; set; }
    public bool Tornado { get; set; }
    public bool HeatBurst { get; set; }
    public bool RequiresHeat { get; set; }
    public string? HitLevel { get; set; }
    public string? Damage { get; set; }
    public string? StartUpFrame { get; set; }
    public string? BlockFrame { get; set; }
    public string? HitFrame { get; set; }
    public string? CounterHitFrame { get; set; }
    public string? Notes { get; set; }
}
