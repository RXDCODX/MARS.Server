namespace MARS.Server.Services.SoundRequest_OBSOLETE.Entitys.Exceptions;

public class TrackNotFoundException(string? message = default) : Exception
{
    public new string? Message { get; init; } = message;
}
