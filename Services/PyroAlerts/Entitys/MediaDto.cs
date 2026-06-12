using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace MARS.Server.Services.PyroAlerts.Entitys;

[method: SetsRequiredMembers]
public struct MediaDto(MediaInfo mediaInfo)
{
    [Required]
    public MediaInfo MediaInfo { get; init; } = mediaInfo;

    public DateTime UploadStartTime { get; set; } = DateTime.Now;
}
