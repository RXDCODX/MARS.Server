﻿namespace MARS.Server.Services.Twitch.Synthesizer.Enitity;

public class MessageToSynthezid
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string Message { get; set; }
    public string Name { get; set; }
    public DateTimeOffset CreationDateTime { get; set; }
    public Guid Guid { get; set; }
}
