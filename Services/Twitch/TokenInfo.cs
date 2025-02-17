namespace MARS.Server.Services.Twitch;

public class TokenInfo
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [Key]
    [Required]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccessToken { get; set; }

    [Required]
    public string RefreshToken { get; set; }

    [Required]
    public TimeSpan ExpiresIn { get; set; }
    public DateTimeOffset WhenCreated { get; set; }
    public DateTimeOffset WhenExpires => WhenCreated.Add(ExpiresIn);
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
