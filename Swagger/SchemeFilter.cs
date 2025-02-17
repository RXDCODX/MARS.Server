using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mars.Server.Swagger;

public class SchemaFilter : ISchemaFilter
{
    public string ToPascalCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return char.ToUpper(str[0]) + str.Substring(1);
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var requiredProps = context
            .Type.GetProperties()
            .Where(x => x.IsNonNullableReferenceType())
            .ToList();

        var requiredJsonProps = schema
            .Properties.Where(j => requiredProps.Any(p => p.Name == ToPascalCase(j.Key)))
            .ToList();

        schema.Required = requiredJsonProps.Select(x => x.Key).ToHashSet();

        foreach (var requiredJsonProp in requiredJsonProps)
            requiredJsonProp.Value.Nullable = false;
    }
}
