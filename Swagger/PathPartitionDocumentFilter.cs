using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mars.Server.Swagger;

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
            title.Contains("API", StringComparison.OrdinalIgnoreCase)
            && !title.Contains("Hub", StringComparison.OrdinalIgnoreCase);
        var isHubsDoc = title.Contains("Hub", StringComparison.OrdinalIgnoreCase);

        if (!isApiDoc && !isHubsDoc)
        {
            return;
        }

        var keep = new Dictionary<string, OpenApiPathItem>(StringComparer.OrdinalIgnoreCase);
        var tagsKeep = new Dictionary<string, OpenApiTag>(StringComparer.OrdinalIgnoreCase);

        foreach ((var path, OpenApiPathItem? value) in swaggerDoc.Paths)
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

        foreach (var openApiTag in swaggerDoc.Tags)
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

        swaggerDoc.Paths.Clear();
        foreach (var kv in keep)
        {
            swaggerDoc.Paths.Add(kv.Key, kv.Value);
        }

        swaggerDoc.Tags.Clear();
        foreach (var kv in tagsKeep)
        {
            swaggerDoc.Tags.Add(kv.Value);
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
            return path.StartsWith("/hubs/");
        }

        bool IsHubTag(string name)
        {
            return name.EndsWith("Hub");
        }

        HashSet<string> CollectReferencedSchemas(OpenApiDocument doc)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);

            foreach (var path in doc.Paths.Values)
            {
                foreach (var op in path.Operations.Values)
                {
                    foreach (var p in op.Parameters)
                    {
                        EnqueueSchema(p.Schema);
                    }

                    var rb = op.RequestBody;
                    if (rb?.Content != null)
                    {
                        foreach (var mt in rb.Content.Values)
                        {
                            EnqueueSchema(mt.Schema);
                        }
                    }
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

            void EnqueueSchema(OpenApiSchema? schema)
            {
                if (schema == null)
                {
                    return;
                }

                if (schema.Reference?.Id is { Length: > 0 } id)
                {
                    referenced.Add(id);
                }
                if (schema.Items != null)
                {
                    EnqueueSchema(schema.Items);
                }

                if (schema.Not != null)
                {
                    EnqueueSchema(schema.Not);
                }

                foreach (var s in schema.AllOf)
                {
                    EnqueueSchema(s);
                }

                foreach (var s in schema.AnyOf)
                {
                    EnqueueSchema(s);
                }

                foreach (var s in schema.OneOf)
                {
                    EnqueueSchema(s);
                }

                if (schema.AdditionalProperties != null)
                {
                    EnqueueSchema(schema.AdditionalProperties);
                }

                if (schema.Properties != null)
                {
                    foreach (var prop in schema.Properties.Values)
                    {
                        EnqueueSchema(prop);
                    }
                }
            }
        }
    }
}
