using System;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class ValidationException(string message) : Exception(message) { }
