using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

public sealed class NotMappedSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, 
        OpenApiSchemaTransformerContext context, 
        CancellationToken cancellationToken)
    {
        if (schema.Type != JsonSchemaType.Object)
        {
            return Task.CompletedTask;
        }

        var properties = schema.Properties;
        if (properties == null)
        {
            return Task.CompletedTask;
        }

        var schemaClrType = context.JsonTypeInfo.Type;
        

        foreach (var propertyName in properties.Keys.ToList())
        {

            const BindingFlags bindingFlags = BindingFlags.Instance
                                              | BindingFlags.Public
                                              | BindingFlags.FlattenHierarchy;

            var clrMember = schemaClrType.GetProperty(propertyName, bindingFlags)
                         ?? (MemberInfo?) schemaClrType.GetField(propertyName, bindingFlags);
            var notMappedAttribute = clrMember?.GetCustomAttribute<NotMappedAttribute>(inherit: true);
            if (notMappedAttribute == null)
            {
                continue;
            }

            properties.Remove(propertyName);
        }

        return Task.CompletedTask;
    }
}