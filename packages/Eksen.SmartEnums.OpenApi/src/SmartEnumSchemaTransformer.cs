using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.SmartEnums.OpenApi;

public sealed class SmartEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly ConditionalWeakTable<IOpenApiSchema, object> ProcessedSchemas = new();

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var schemaClrType = context.JsonPropertyInfo?.AttributeProvider as Type
                            ?? context.JsonPropertyInfo?.PropertyType ?? context.JsonTypeInfo.Type;
        schemaClrType = Nullable.GetUnderlyingType(schemaClrType) ?? schemaClrType;

        ProcessSchema(schema, schemaClrType);

        return Task.CompletedTask;
    }

    public static void ProcessSchema(OpenApiSchema schema, Type schemaClrType)
    {
        if (ProcessedSchemas.TryGetValue(schema, out _))
        {
            return;
        }

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

                schemaClrType = Nullable.GetUnderlyingType(schemaClrType) ?? schemaClrType;

                ProcessSchema((OpenApiSchema)properties[propertyName], schemaClrType);
            }


            return;
        }

        schema.Type |= JsonSchemaType.String;
        schema.Type &= ~JsonSchemaType.Object;

        schema.Enum ??= new List<JsonNode>();
        schema.Enum.Clear();

        var method = schemaClrType.GetMethod(nameof(Enumeration<>.GetValues),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
        var allValuesEnumerable = (IEnumerable)method.Invoke(obj: null, Array.Empty<object>())!;
        var codePropertyGetter = schemaClrType
            .GetProperty(nameof(Enumeration<>.Code), BindingFlags.Instance | BindingFlags.Public)!
            .GetGetMethod()!;

        var enumerator = allValuesEnumerable.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                var code = codePropertyGetter.Invoke(enumerator.Current, []);
                schema.Enum.Add(JsonValue.Create(code)!);
            }
        }
        finally
        {
            ProcessedSchemas.TryAdd(schema, new object());
            (enumerator as IDisposable)?.Dispose();
        }
    }
}