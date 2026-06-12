using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MARS.Server.Swagger;

// Splits paths into two Swagger documents: "api" (only /api/*) and "hubs" (SignalR-like paths)
public sealed class PathPartitionDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Paths == null || swaggerDoc.Info == null)
        {
            return;
        }

        var title = swaggerDoc.Info.Title ?? string.Empty;
        var isApiDoc =
            title.Contains("api", StringComparison.OrdinalIgnoreCase)
            && !title.Contains("hubs", StringComparison.OrdinalIgnoreCase);
        var isHubsDoc = title.Contains("hub", StringComparison.OrdinalIgnoreCase);

        if (!isApiDoc && !isHubsDoc)
        {
            return;
        }

        var keep = new Dictionary<string, IOpenApiPathItem>(StringComparer.OrdinalIgnoreCase);
        var tagsKeep = new Dictionary<string, OpenApiTag>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, value) in swaggerDoc.Paths)
        {
            if (isApiDoc)
            {
                if (!IsHubPath(path))
                {
                    keep[path] = value;
                }
            }
            else if (isHubsDoc)
            {
                if (IsHubPath(path))
                {
                    keep[path] = value;
                }
            }
        }

        if (swaggerDoc.Tags != null)
        {
            foreach (var openApiTag in swaggerDoc.Tags)
            {
                if (!string.IsNullOrEmpty(openApiTag.Name))
                {
                    if (isApiDoc)
                    {
                        if (!IsHubTag(openApiTag.Name))
                        {
                            tagsKeep[openApiTag.Name] = openApiTag;
                        }
                    }
                    else if (isHubsDoc)
                    {
                        if (IsHubTag(openApiTag.Name))
                        {
                            tagsKeep[openApiTag.Name] = openApiTag;
                        }
                    }
                }
            }
        }

        swaggerDoc.Paths.Clear();
        foreach (var kv in keep)
        {
            swaggerDoc.Paths.Add(kv.Key, kv.Value);
        }

        if (swaggerDoc.Tags != null)
        {
            swaggerDoc.Tags.Clear();
            foreach (var kv in tagsKeep)
            {
                swaggerDoc.Tags.Add(kv.Value);
            }
        }

        // Filter component schemas to only those referenced by the kept paths
        if (swaggerDoc.Components?.Schemas is { Count: > 0 })
        {
            var referenced = CollectReferencedSchemas(swaggerDoc);
            var toRemove = swaggerDoc
                .Components.Schemas.Keys.Where(name => !referenced.Contains(name))
                .ToList();
            foreach (var name in toRemove)
            {
                swaggerDoc.Components.Schemas.Remove(name);
            }
        }

        return;

        bool IsHubPath(string path)
        {
            return path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase);
        }

        bool IsHubTag(string name)
        {
            return name.EndsWith("hub", StringComparison.OrdinalIgnoreCase);
        }

        HashSet<string> CollectReferencedSchemas(OpenApiDocument doc)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);

            foreach (var path in doc.Paths.Values)
            {
                if (path.Operations == null)
                {
                    continue;
                }

                foreach (var op in path.Operations.Values)
                {
                    if (op.Parameters != null)
                    {
                        foreach (var p in op.Parameters)
                        {
                            EnqueueSchema(p.Schema);
                        }
                    }

                    var rb = op.RequestBody;
                    if (rb?.Content != null)
                    {
                        foreach (var mt in rb.Content.Values)
                        {
                            EnqueueSchema(mt.Schema);
                        }
                    }

                    if (op.Responses != null)
                    {
                        foreach (var resp in op.Responses.Values)
                        {
                            if (resp.Content == null)
                            {
                                continue;
                            }

                            foreach (var mt in resp.Content.Values)
                            {
                                EnqueueSchema(mt.Schema);
                            }
                        }
                    }
                }
            }

            // Expand transitive references from components
            if (doc.Components?.Schemas != null)
            {
                var queue = new Queue<string>(referenced);
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (queue.Count > 0)
                {
                    var name = queue.Dequeue();
                    if (!visited.Add(name))
                    {
                        continue;
                    }

                    if (doc.Components.Schemas.TryGetValue(name, out var schema))
                    {
                        var before = referenced.Count;
                        EnqueueSchema(schema);
                        if (referenced.Count > before)
                        {
                            foreach (var newly in referenced.Where(x => !visited.Contains(x)))
                            {
                                queue.Enqueue(newly);
                            }
                        }
                    }
                }
            }

            return referenced;

            void EnqueueSchema(IOpenApiSchema? schema)
            {
                if (schema == null)
                {
                    return;
                }

                // Handle reference type
                if (schema is OpenApiSchemaReference { Reference.Id: { Length: > 0 } id })
                {
                    referenced.Add(id);
                    return;
                }

                // Cast to concrete type to access properties
                if (schema is not OpenApiSchema concreteSchema)
                {
                    return;
                }

                if (concreteSchema.Items != null)
                {
                    EnqueueSchema(concreteSchema.Items);
                }

                if (concreteSchema.Not != null)
                {
                    EnqueueSchema(concreteSchema.Not);
                }

                if (concreteSchema.AllOf != null)
                {
                    foreach (var s in concreteSchema.AllOf)
                    {
                        EnqueueSchema(s);
                    }
                }

                if (concreteSchema.AnyOf != null)
                {
                    foreach (var s in concreteSchema.AnyOf)
                    {
                        EnqueueSchema(s);
                    }
                }

                if (concreteSchema.OneOf != null)
                {
                    foreach (var s in concreteSchema.OneOf)
                    {
                        EnqueueSchema(s);
                    }
                }

                if (concreteSchema.AdditionalProperties != null)
                {
                    EnqueueSchema(concreteSchema.AdditionalProperties);
                }

                if (concreteSchema.Properties != null)
                {
                    foreach (var prop in concreteSchema.Properties.Values)
                    {
                        EnqueueSchema(prop);
                    }
                }
            }
        }
    }
}
