using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using Eksen.Core.Helpers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.SmartEnums.OpenApi;

public sealed class SmartEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var schemaClrType = context.JsonPropertyInfo?.AttributeProvider as Type
                            ?? context.JsonPropertyInfo?.PropertyType ?? context.JsonTypeInfo.Type;

        ProcessSchema(schema, schemaClrType);

        return Task.CompletedTask;
    }

    public static void ProcessSchema(
        OpenApiSchema schema,
        Type schemaClrType)
    {
        ProcessSchema(schema, schemaClrType, out _, out _, out _);
    }

    public static void ProcessSchema(
        OpenApiSchema schema,
        Type schemaClrType,
        out bool isNullable,
        out bool isCollection,
        out bool isNullableCollection)
    {
        schemaClrType = TypeHelper.GetUnderlyingType(schemaClrType, out isNullable, out isCollection, out isNullableCollection);

        if (schemaClrType is not { IsEnumeration: true })
        {
            var properties = schema.Properties ?? new Dictionary<string, IOpenApiSchema>();

            foreach (var propertyName in properties.Keys.ToList())
            {
                const BindingFlags bindingFlags = BindingFlags.Instance
                                                  | BindingFlags.Public
                                                  | BindingFlags.FlattenHierarchy;

                var clrMember = schemaClrType.GetProperty(propertyName, bindingFlags)
                                ?? (MemberInfo?)schemaClrType.GetField(propertyName, bindingFlags);

                if (clrMember is FieldInfo fieldInfo)
                {
                    schemaClrType = fieldInfo.FieldType;
                }
                else if (clrMember is PropertyInfo propertyInfo)
                {
                    schemaClrType = propertyInfo.PropertyType;
                }
                else
                {
                    continue;
                }

                ProcessSchema((OpenApiSchema)properties[propertyName], schemaClrType);
            }

            return;
        }

        if (isCollection)
        {
            schema.Type = JsonSchemaType.Array;
            if (isNullableCollection)
            {
                schema.Type |= JsonSchemaType.Null;
            }

            schema.Items ??= new OpenApiSchema();

            SetElementSchema((OpenApiSchema)schema.Items, schemaClrType, isNullable);
        }
        else
        {
            SetElementSchema(schema, schemaClrType, isNullable);
        }
    }

    private static void SetElementSchema(OpenApiSchema elementSchema, Type elementType, bool isNullable)
    {
        elementSchema.Type = JsonSchemaType.String;

        if (isNullable)
        {
            elementSchema.Type |= JsonSchemaType.Null;
        }

        elementSchema.Enum ??= new List<JsonNode>();
        elementSchema.Enum.Clear();

        var method = elementType.GetMethod(nameof(Enumeration<>.GetValues),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
        var allValuesEnumerable = (IEnumerable)method.Invoke(obj: null, Array.Empty<object>())!;
        var codePropertyGetter = elementType
            .GetProperty(nameof(Enumeration<>.Code), BindingFlags.Instance | BindingFlags.Public)!
            .GetGetMethod()!;

        var enumerator = allValuesEnumerable.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                var code = codePropertyGetter.Invoke(enumerator.Current, []);
                elementSchema.Enum.Add(JsonValue.Create(code)!);
            }
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }
}