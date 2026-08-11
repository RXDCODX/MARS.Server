namespace MARS.Server.Services.Twitch.Validation;

public sealed class ValidationResult
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Errors => _errors;

    public string? FirstError => _errors.Count > 0 ? _errors[0] : null;
    public bool IsValid => _errors.Count == 0 && !HasSilentFailure;
    public bool IsInvalid => !IsValid;
    public bool HasSilentFailure { get; set; }

    public void AddError(string error)
    {
        _errors.Add(error);
    }
}
