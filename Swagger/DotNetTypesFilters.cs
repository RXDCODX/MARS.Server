using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MARS.Server.Swagger;

// Swashbuckle filter: cleans operations and removes System/Reflection schemas
public sealed class DotNetTypesDocumentFilter : IDocumentFilter
{
    private static readonly HashSet<string> BlockedSchemaNames = new(StringComparer.Ordinal)
    {
        // Core reflection and system types that should not appear in public schema
        "Exception",
        "MethodBase",
        "MethodInfo",
        "ConstructorInfo",
        "MemberInfo",
        "PropertyInfo",
        "FieldInfo",
        "EventInfo",
        "ParameterInfo",
        "Type",
        "TypeInfo",
        "Assembly",
        "Module",
        "ModuleHandle",
        "RuntimeMethodHandle",
        "RuntimeFieldHandle",
        "RuntimeTypeHandle",
        "IntPtr",
        "ICustomAttributeProvider",
        "CustomAttributeData",
        "CustomAttributeNamedArgument",
        "CustomAttributeTypedArgument",
        "StructLayoutAttribute",
        // Frequently leaked framework models
        "Color",
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Paths != null)
        {
            foreach (var path in swaggerDoc.Paths.Values)
            {
                foreach (var operation in path.Operations.Values)
                {
                    // Remove parameters that reference blocked schemas
                    if (operation.Parameters is { Count: > 0 })
                    {
                        operation.Parameters =
                        [
                            .. operation.Parameters.Where(p => !IsRefToBlocked(p.Schema)),
                        ];
                    }

                    // Sanitize request bodies
                    if (operation.RequestBody != null)
                    {
                        foreach (var mt in operation.RequestBody.Content.Values)
                        {
                            if (IsRefToBlocked(mt.Schema))
                            {
                                mt.Schema = new OpenApiSchema { Type = "string" };
                            }
                        }
                    }

                    // Sanitize responses
                    foreach (var response in operation.Responses.Values)
                    {
                        if (response.Content == null)
                        {
                            continue;
                        }

                        foreach (var mt in response.Content.Values)
                        {
                            if (IsRefToBlocked(mt.Schema))
                            {
                                mt.Schema = new OpenApiSchema { Type = "string" };
                            }
                        }
                    }
                }
            }
        }

        // Remove blocked component schemas entirely
        if (swaggerDoc.Components?.Schemas != null)
        {
            var toRemove = swaggerDoc
                .Components.Schemas.Where(kv => IsBlockedSchemaName(kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                swaggerDoc.Components.Schemas.Remove(key);
            }
        }
    }

    private static bool IsRefToBlocked(OpenApiSchema? schema)
    {
        if (schema?.Reference == null)
        {
            return false;
        }

        var id = schema.Reference.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            // Fallback: parse last segment of ref (e.g., #/components/schemas/Exception)
            var r = schema.Reference.ReferenceV3;
            if (!string.IsNullOrWhiteSpace(r))
            {
                id = r.Split('/')[^1];
            }
        }
        return IsBlockedSchemaName(id);
    }

    private static bool IsBlockedSchemaName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (BlockedSchemaNames.Contains(name))
        {
            return true;
        }

        // Be defensive: any obvious System.* names sneaking in as keys
        return name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
