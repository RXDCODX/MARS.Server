using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
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
            Type = "object",
            Description = "Обобщенный результат операции",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["success"] = new() { Type = "boolean", Description = "Флаг успешности операции" },
                ["message"] = new()
                {
                    Type = "string",
                    Nullable = true,
                    Description = "Сообщение о результате операции",
                },
                ["data"] = new()
                {
                    Description = "Данные результата операции",
                    Nullable = true,
                    // Используем расширение для указания на generic параметр
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        ["x-generic-type"] = new OpenApiString("TData"),
                    },
                },
            },
            Required = new HashSet<string> { "success" },
            AdditionalPropertiesAllowed = false,
            // Добавляем маркер generic типа
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-is-generic"] = new OpenApiBoolean(true),
                ["x-generic-parameters"] = new OpenApiArray { new OpenApiString("TData") },
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

            // Создаем новую схему используя allOf
            var newSchema = new OpenApiSchema
            {
                AllOf =
                [
                    new()
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.Schema,
                            Id = BaseOperationResultSchemaName,
                        },
                    },
                ],
                Description =
                    schema.Description
                    ?? $"Результат операции с данными типа {GetDataTypeName(dataProperty)}",
                // Добавляем информацию о типе данных для генераторов клиентов
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-generic-type-argument"] = new OpenApiString(GetDataTypeName(dataProperty)),
                },
            };

            // Заменяем схему
            swaggerDoc.Components.Schemas[schemaName] = newSchema;
        }
    }

    /// <summary>
    /// Проверяет, является ли схема OperationResult
    /// </summary>
    private static bool IsOperationResultSchema(string schemaName, OpenApiSchema schema)
    {
        // Проверяем по имени
        if (!schemaName.EndsWith("OperationResult", StringComparison.Ordinal))
        {
            return false;
        }

        // Проверяем структуру: должны быть поля success, message, data
        if (schema.Properties == null || schema.Properties.Count == 0)
        {
            return false;
        }

        var hasSuccess = schema.Properties.ContainsKey("success");
        var hasMessage = schema.Properties.ContainsKey("message");
        var hasData = schema.Properties.ContainsKey("data");

        return hasSuccess && hasMessage && hasData;
    }

    /// <summary>
    /// Извлекает имя типа данных из схемы свойства data
    /// </summary>
    private static string GetDataTypeName(OpenApiSchema dataSchema)
    {
        // Если есть ссылка на схему
        if (dataSchema.Reference != null)
        {
            return dataSchema.Reference.Id ?? "unknown";
        }

        // Если это allOf со ссылкой
        if (dataSchema.AllOf?.Count > 0)
        {
            var firstRef = dataSchema.AllOf.FirstOrDefault(s => s.Reference != null);
            if (firstRef?.Reference != null)
            {
                return firstRef.Reference.Id ?? "unknown";
            }
        }

        // Если это массив
        if (dataSchema is { Type: "array", Items: not null })
        {
            var itemTypeName = GetDataTypeName(dataSchema.Items);
            return $"{itemTypeName}[]";
        }

        // Если это примитивный тип
        return !string.IsNullOrEmpty(dataSchema.Type)
            ? dataSchema.Type switch
            {
                "string" => "string",
                "integer" => "number",
                "number" => "number",
                "boolean" => "boolean",
                "object" => "object",
                _ => dataSchema.Type,
            }
            : "any";
    }
}
