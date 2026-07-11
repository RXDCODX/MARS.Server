using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.Entitys;

[Table("MikuModules")]
public class MikuModule
{
    [Key]
    public int PageId { get; set; }

    [Required]
    [MaxLength(300)]
    public required string Title { get; set; }

    [MaxLength(300)]
    public string? JapaneseName { get; set; }

    [MaxLength(200)]
    public string? Designer { get; set; }

    [Required]
    [MaxLength(500)]
    public required string ThumbnailUrl { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Songs { get; set; }

    public DateTimeOffset WhenAdded { get; set; }

    public DateTimeOffset LastOrder { get; set; }

    public int OrderCount { get; set; }
}
