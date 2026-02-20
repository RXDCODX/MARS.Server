namespace MARS.Server.ApplicationState;

public partial class RootState
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public bool RandomMemeOnlineIsStop { get; set; }
    public bool PuntoSwitcherFilterEnabled { get; set; } = true;
    public long WaifuRollCooldownMinutes { get; set; } = 20;
}
