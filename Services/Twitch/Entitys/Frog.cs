using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.Entitys;

[Table("Frogs")]
public class Frog
{
    [Key]
    public int Pid { get; set; }

    [Required]
    [MaxLength(200)]
    public required string CommonName { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ScientificName { get; set; }

    [MaxLength(100)]
    public string? Family { get; set; }

    [MaxLength(200)]
    public string? RussianName { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ThumbnailUrl { get; set; }

    [MaxLength(50)]
    public string? Size { get; set; }

    [MaxLength(100)]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [MaxLength(500)]
    public string? Habits { get; set; }

    public DateTime WhenAdded { get; set; }

    public DateTime LastOrder { get; set; }

    public int OrderCount { get; set; }
}
