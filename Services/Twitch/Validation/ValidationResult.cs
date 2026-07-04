using System.Collections.Generic;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class ValidationResult
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Errors => _errors;
    public bool IsValid => _errors.Count == 0;
    public bool IsInvalid => !IsValid;

    public void AddError(string error)
    {
        _errors.Add(error);
    }
}
