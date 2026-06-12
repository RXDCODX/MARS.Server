using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MARS.Server.Swagger;

/// <summary>
/// Фильтр для оптимизации схем OperationResult в Swagger документе.
/// Заменяет множественные конкретные типы OperationResult на использование базового generic типа.
/// </summary>
public sealed class OperationResultSchemaFilter : IDocumentFilter
{
    private const string BaseOperationResultSchemaName = "OperationResult";

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Components?.Schemas == null)
        {
            return;
        }

        // Находим все схемы, которые являются OperationResult с конкретными типами
        // ВАЖНО: исключаем базовый "OperationResult" без суффикса типа
        var operationResultSchemas = swaggerDoc
            .Components.Schemas.Where(kv =>
                kv.Key.EndsWith("OperationResult", StringComparison.Ordinal)
                && kv.Key != BaseOperationResultSchemaName // Исключаем базовый тип
                && IsOperationResultSchema(kv.Key, kv.Value)
            )
            .ToList();

        if (operationResultSchemas.Count == 0)
        {
            return;
        }

        // Создаем базовую generic схему OperationResult<TData>
        var baseOperationResultSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "Обобщенный результат операции",
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["success"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Boolean,
                    Description = "Флаг успешности операции",
                },
                ["message"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "Сообщение о результате операции",
                },
                ["data"] = new OpenApiSchema
                {
                    Description = "Данные результата операции",
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        ["x-generic-type"] = new JsonValueExtension("TData"),
                    },
                },
            },
            Required = new HashSet<string> { "success" },
            AdditionalPropertiesAllowed = false,
            // Добавляем маркер generic типа
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-is-generic"] = new JsonValueExtension(true),
                ["x-generic-parameters"] = new JsonValueExtension(new JsonArray { "TData" }),
            },
        };

        // Заменяем или добавляем базовую схему
        swaggerDoc.Components.Schemas[BaseOperationResultSchemaName] = baseOperationResultSchema;

        // Для каждой конкретной схемы OperationResult создаем упрощенную версию
        // используя allOf для наследования от базового типа
        foreach (var (schemaName, schema) in operationResultSchemas)
        {
            // Извлекаем тип данных из свойства data
            var dataProperty = schema.Properties?["data"];
            if (dataProperty == null)
            {
                continue;
            }

            // Создаем новую схему используя allOf с ссылкой на базовый тип
            var newSchema = new OpenApiSchema
            {
                AllOf =
                [
                    new OpenApiSchemaReference(BaseOperationResultSchemaName, swaggerDoc, null),
                ],
                Description =
                    schema.Description
                    ?? $"Результат операции с данными типа {GetDataTypeName(dataProperty)}",
                // Добавляем информацию о типе данных для генераторов клиентов
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-generic-type-argument"] = new JsonValueExtension(
                        GetDataTypeName(dataProperty)
                    ),
                },
            };

            // Заменяем схему
            swaggerDoc.Components.Schemas[schemaName] = newSchema;
        }
    }

    /// <summary>
    /// Проверяет, является ли схема OperationResult
    /// </summary>
    private static bool IsOperationResultSchema(string schemaName, IOpenApiSchema schema)
    {
        // Проверяем по имени
        if (!schemaName.EndsWith("OperationResult", StringComparison.Ordinal))
        {
            return false;
        }

        if (schema is not OpenApiSchema concreteSchema)
        {
            return false;
        }

        // Проверяем структуру: должны быть поля success, message, data
        if (concreteSchema.Properties == null || concreteSchema.Properties.Count == 0)
        {
            return false;
        }

        var hasSuccess = concreteSchema.Properties.ContainsKey("success");
        var hasMessage = concreteSchema.Properties.ContainsKey("message");
        var hasData = concreteSchema.Properties.ContainsKey("data");

        return hasSuccess && hasMessage && hasData;
    }

    /// <summary>
    /// Извлекает имя типа данных из схемы свойства data
    /// </summary>
    private static string GetDataTypeName(IOpenApiSchema dataSchema)
    {
        // Check for reference
        if (dataSchema is OpenApiSchemaReference { Id: not null } schemaRef)
        {
            return schemaRef.Id;
        }

        if (dataSchema is not OpenApiSchema concreteSchema)
        {
            return "unknown";
        }

        // Если это allOf со ссылкой
        if (concreteSchema.AllOf?.Count > 0)
        {
            var firstRef = concreteSchema.AllOf.FirstOrDefault(s => s is OpenApiSchemaReference);
            if (firstRef is OpenApiSchemaReference { Id: not null } refSchema)
            {
                return refSchema.Id;
            }
        }

        // Если это массив
        if (concreteSchema is { Type: JsonSchemaType.Array, Items: not null })
        {
            var itemTypeName = GetDataTypeName(concreteSchema.Items);
            return $"{itemTypeName}[]";
        }

        // Если это примитивный тип
        return concreteSchema.Type.HasValue
            ? concreteSchema.Type.Value switch
            {
                JsonSchemaType.String => "string",
                JsonSchemaType.Integer => "number",
                JsonSchemaType.Number => "number",
                JsonSchemaType.Boolean => "boolean",
                JsonSchemaType.Object => "object",
                JsonSchemaType.Array => "array",
                JsonSchemaType.Null => "null",
                _ => "any",
            }
            : "any";
    }
}

/// <summary>
/// Custom extension to handle JSON values in extensions dictionary
/// </summary>
internal class JsonValueExtension : IOpenApiExtension
{
    private readonly JsonNode? _value;

    public JsonValueExtension(string value) => _value = JsonNode.Parse($"\"{value}\"");

    public JsonValueExtension(bool value) => _value = JsonNode.Parse(value.ToString().ToLower());

    public JsonValueExtension(JsonNode value) => _value = value;

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
        if (_value != null)
        {
            writer.WriteRaw(_value.ToJsonString());
        }
    }
}
