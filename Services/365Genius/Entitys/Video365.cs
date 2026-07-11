using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services._365Genius.Entitys;

/// <summary>
/// Represents a video entity for the 365Genius service.
/// </summary>
public class Video365
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public int SiteId { get; set; }
    public required string Title { get; set; }
    public required string PlayerUrl { get; set; }
    public required string DirectLinkUrl { get; set; }
    public required string Description { get; set; }
    public required string DownloadUrl { get; set; }
    public bool IsUploaded { get; set; }
    public DateTime DateUpload { get; set; }
    public TimeSpan Duration { get; set; }
    public long TelegramMessageId { get; set; }
    public required int VideoWidth { get; set; }
    public required int VideoHeight { get; set; }

    [NotMapped]
    public required string ThumbnailFilePath { get; set; }
}
