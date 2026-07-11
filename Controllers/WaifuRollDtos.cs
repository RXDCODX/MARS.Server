using System;
using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Controllers;

public record WaifuDto
{
    public required string ShikiId { get; init; }
    public required string Name { get; init; }
    public long Age { get; init; }
    public string? Anime { get; init; }
    public string? Manga { get; init; }
    public DateTime WhenAdded { get; init; }
    public DateTime LastOrder { get; init; }
    public int OrderCount { get; init; }
    public bool IsPrivated { get; init; }
    public required string ImageUrl { get; init; }
    public Guid? AudioId { get; init; }
    public string? AudioName { get; init; }
}

public record CreateWaifuRequest
{
    [Required]
    [MaxLength(20)]
    public required string ShikiId { get; init; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    public long Age { get; init; }
    public string? Anime { get; init; }
    public string? Manga { get; init; }

    [Required]
    [MaxLength(200)]
    public required string ImageUrl { get; init; }

    public Guid? AudioId { get; init; }
}

public record UpdateWaifuRequest
{
    [MaxLength(200)]
    public string? Name { get; init; }

    public long? Age { get; init; }
    public string? Anime { get; init; }
    public string? Manga { get; init; }

    [MaxLength(200)]
    public string? ImageUrl { get; init; }

    public bool? IsPrivated { get; init; }
    public Guid? AudioId { get; init; }
}

public record HusbandDto
{
    public required string TwitchId { get; init; }
    public string? DisplayName { get; init; }
    public string? ProfileImageUrl { get; init; }
    public DateTime WhenOrdered { get; init; }
    public string? WaifuBrideId { get; init; }
    public bool IsPrivated { get; init; }
    public long OrderCount { get; init; }
    public string? WaifuRollId { get; init; }
    public DateTime? WhenPrivated { get; init; }
    public int? LastWeddingCongratulatedMonths { get; init; }
}

public record UpdateHusbandRequest
{
    public string? WaifuBrideId { get; init; }
    public bool? IsPrivated { get; init; }
    public string? WaifuRollId { get; init; }
    public DateTime? WhenPrivated { get; init; }
    public int? LastWeddingCongratulatedMonths { get; init; }
}

public record WaifuRollAudioDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string FileExtension { get; init; }
    public DateTime CreatedAt { get; init; }
}
