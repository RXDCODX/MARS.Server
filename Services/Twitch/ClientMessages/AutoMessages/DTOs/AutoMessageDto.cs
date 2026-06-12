using System;

namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages.DTOs;

public class AutoMessageDto
{
    public Guid Id { get; set; }
    public required string Message { get; set; }
}
