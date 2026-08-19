using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.Adhd.Entities;

public class AdhdLayoutConfig
{
    [Key]
    public int Id { get; set; }

    public bool ShowRainEffect { get; set; } = true;
    public bool ShowDVDLogos { get; set; } = true;
    public bool ShowBreakingNews { get; set; } = true;
    public bool ShowStreamerVideo { get; set; } = true;
    public bool ShowFitnessVideo { get; set; } = true;
    public bool ShowGTAVideo { get; set; } = true;
    public bool ShowHydraulicMobileVideo { get; set; } = true;
    public bool ShowSlimeVideo { get; set; } = true;
    public bool ShowMukbangVideo { get; set; } = true;
    public bool ShowQuiz { get; set; } = true;
    public bool ShowSurfer { get; set; } = true;
    public bool ShowLOFIGirl { get; set; } = true;
    public bool ShowCatisa { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool ShowTimer { get; set; } = true;
    public int DvdLogosCount { get; set; } = 12;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}
