namespace MARS.Server.Services.BooruShared;

public static class TagValidator
{
    public const int MaxTags = 2;

    public static bool IsValidTagCount(string tags, int maxTags = MaxTags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return true;
        }

        var tagCount = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return tagCount <= maxTags;
    }

    public static string? GetValidationError(string tags, int maxTags = MaxTags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return null;
        }

        var tagCount = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (tagCount > maxTags)
        {
            return $"Максимальное количество тегов: {maxTags}. Указано: {tagCount}";
        }

        return null;
    }
}
