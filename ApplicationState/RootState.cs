namespace MARS.Server.ApplicationState;

public partial class RootState
{
    [Key]
    public required string Name { get; set; }

    public string Value { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string TypeDescription { get; set; } = string.Empty;
}

public static class RootStateKeys
{
    public const string RandomMemeOnlineIsStop = "RandomMemeOnlineIsStop";
    public const string PuntoSwitcherFilterEnabled = "PuntoSwitcherFilterEnabled";
    public const string WaifuRollCooldownMinutes = "WaifuRollCooldownMinutes";
}
