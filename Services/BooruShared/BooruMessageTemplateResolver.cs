using System;
using System.Collections.Generic;

namespace MARS.Server.Services.BooruShared;

public static class BooruMessageTemplateResolver
{
    public static string Resolve(string template, Dictionary<string, string?> variables)
    {
        var result = template;

        foreach (var (key, value) in variables)
        {
            result = result.Replace(
                $"{{{key}}}",
                value ?? string.Empty,
                StringComparison.OrdinalIgnoreCase
            );
        }

        return result;
    }
}
