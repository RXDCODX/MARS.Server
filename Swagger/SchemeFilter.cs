using System.Linq;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MARS.Server.Swagger;

public class SchemaFilter : ISchemaFilter
{
    public string ToPascalCase(string str)
    {
        return string.IsNullOrEmpty(str) ? str : char.ToUpper(str[0]) + str.Substring(1);
    }

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Cast to concrete type to access properties
        if (schema is not OpenApiSchema concreteSchema)
        {
            return;
        }

        if (concreteSchema.Properties == null || concreteSchema.Properties.Count == 0)
        {
            return;
        }

        var requiredProps = context
            .Type.GetProperties()
            .Where(x => x.IsNonNullableReferenceType())
            .ToList();

        var requiredJsonProps = concreteSchema
            .Properties.Where(j => requiredProps.Any(p => p.Name == ToPascalCase(j.Key)))
            .ToList();

        concreteSchema.Required = requiredJsonProps.Select(x => x.Key).ToHashSet();

        foreach (var requiredJsonProp in requiredJsonProps)
        {
            // In OpenAPI 2.0.0, nullable is determined by checking if Type includes JsonSchemaType.Null
            // For non-nullable, we don't need to set anything special
            if (requiredJsonProp.Value is OpenApiSchema requiredSchema)
            {
                // Ensure the schema is explicitly non-nullable by not including null in the type
                // property handles this appropriately in the OpenAPI 2.0.0 model
            }
        }
    }
}
