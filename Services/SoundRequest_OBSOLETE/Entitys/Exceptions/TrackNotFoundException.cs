namespace MARS.Server.Services.SoundRequest.Entitys.Exceptions;

public class TrackNotFoundException(string? message = default) : Exception
{
    public new string? Message { get; init; } = message;
}
