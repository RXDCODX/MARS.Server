using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MARS.Server.Swagger;

// Swashbuckle filter: cleans operations and removes System/Reflection schemas
public sealed class DotNetTypesDocumentFilter : IDocumentFilter
{
    private static readonly HashSet<string> BlockedSchemaNames = new(StringComparer.Ordinal)
    {
        // Core reflection and system types that should not appear in public schema
        nameof(Exception),
        nameof(MethodBase),
        nameof(MethodInfo),
        nameof(ConstructorInfo),
        nameof(MemberInfo),
        nameof(PropertyInfo),
        nameof(FieldInfo),
        nameof(EventInfo),
        nameof(ParameterInfo),
        nameof(Type),
        nameof(TypeInfo),
        nameof(Assembly),
        nameof(Module),
        nameof(ModuleHandle),
        nameof(RuntimeMethodHandle),
        nameof(RuntimeFieldHandle),
        nameof(RuntimeTypeHandle),
        nameof(IntPtr),
        nameof(ICustomAttributeProvider),
        nameof(CustomAttributeData),
        nameof(CustomAttributeNamedArgument),
        nameof(CustomAttributeTypedArgument),
        nameof(StructLayoutAttribute),
        // Frequently leaked framework models
        nameof(System.Drawing.Color),
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Paths != null)
        {
            foreach (var path in swaggerDoc.Paths.Values)
            {
                if (path.Operations == null)
                {
                    continue;
                }

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
                    if (operation.RequestBody?.Content != null)
                    {
                        foreach (var mt in operation.RequestBody.Content.Values)
                        {
                            if (IsRefToBlocked(mt.Schema))
                            {
                                mt.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
                            }
                        }
                    }

                    // Sanitize responses
                    if (operation.Responses != null)
                    {
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
                                    mt.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
                                }
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

    private static bool IsRefToBlocked(IOpenApiSchema? schema)
    {
        if (schema == null)
        {
            return false;
        }

        // Check if it's a reference
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var id = schemaRef.Reference?.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                // Fallback: parse last segment of ref (e.g., #/components/schemas/Exception)
                var r = schemaRef.Reference?.ReferenceV3 ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(r))
                {
                    id = r.Split('/')[^1];
                }
            }
            return IsBlockedSchemaName(id);
        }

        return false;
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
