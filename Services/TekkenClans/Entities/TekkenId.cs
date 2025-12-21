using System.Text;

namespace MARS.Server.Services.TekkenClans.Entities;

public readonly struct TekkenId : IEquatable<TekkenId>
{
    private const int Length = 14; // "XXXX-XXXX-XXXX" (14 символов)
    private const int LengthWithoutDashes = 12; // "XXXXXXXXXXXX" (12 символов)
    private const int SegmentCount = 3; // 3 сегмента
    private const int SegmentLength = 4; // 4 символа на сегмент
    private const string ValidChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private readonly byte[] _a;
    private readonly byte[] _b;
    private readonly byte[] _c;

    public string Value { get; }

    public TekkenId(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));
        }

        // Если строка без тире (например, "2jDRYgYEBjH4"), форматируем её
        if (!value.Contains('-'))
        {
            if (value.Length != LengthWithoutDashes)
            {
                throw new ArgumentException(
                    $"Invalid length. Expected {LengthWithoutDashes} chars without dashes.",
                    nameof(value)
                );
            }

            // Вставляем тире каждые 4 символа: "2jDRYgYEBjH4" -> "2jDR-YgYE-BjH4"
            value = string.Join("-", value[..4], value.Substring(4, 4), value.Substring(8, 4));
        }

        if (!IsValid(value))
        {
            throw new ArgumentException("Invalid TekkenId format.", nameof(value));
        }

        Value = value;
        var segments = value.Split('-');
        _a = Encoding.ASCII.GetBytes(segments[0]);
        _b = Encoding.ASCII.GetBytes(segments[1]);
        _c = Encoding.ASCII.GetBytes(segments[2]);
    }

    public static TekkenId Parse(string input)
    {
        return new TekkenId(input);
    }

    public static bool TryParse(string input, out TekkenId result)
    {
        try
        {
            result = new TekkenId(input);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    public static TekkenId NewId()
    {
        var random = new Random();
        var sb = new StringBuilder();

        for (var i = 0; i < SegmentCount; i++)
        {
            if (i > 0)
            {
                sb.Append('-');
            }

            for (var j = 0; j < SegmentLength; j++)
            {
                var index = random.Next(ValidChars.Length);
                sb.Append(ValidChars[index]);
            }
        }

        return new TekkenId(sb.ToString());
    }

    public static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Если строка без тире, проверяем длину 12 символов
        if (!value.Contains('-'))
        {
            if (value.Length != LengthWithoutDashes)
            {
                return false;
            }

            // Проверяем, что все символы допустимы
            return value.All(c => ValidChars.Contains(c));
        }

        // Стандартная проверка для формата с тире
        if (value.Length != Length)
        {
            return false;
        }

        var segments = value.Split('-');
        return segments.Length == SegmentCount
            && segments.All(s => s.Length == SegmentLength)
            && value.All(c => c == '-' || ValidChars.Contains(c));
    }

    public string ToStringWithoutDashes() => Value.Replace("-", "");

    public bool Equals(TekkenId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is TekkenId other && Equals(other);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value;

    public static bool operator ==(TekkenId left, TekkenId right) => left.Equals(right);

    public static bool operator !=(TekkenId left, TekkenId right) => !left.Equals(right);

    public ReadOnlySpan<byte> GetSegment1() => _a;

    public ReadOnlySpan<byte> GetSegment2() => _b;

    public ReadOnlySpan<byte> GetSegment3() => _c;
}
