using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Eksen.TestBase;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;

namespace Eksen.OpenApi.Tests;

internal static class TestContextFactory
{
    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    public static OpenApiSchemaTransformerContext CreateContext(Type type)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        var typeInfo = options.GetTypeInfo(type);

        return new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            JsonTypeInfo = typeInfo,
            JsonPropertyInfo = null,
            ParameterDescription = null,
            ApplicationServices = Services
        };
    }
}

public class ExampleValueAttributeTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Set_Value()
    {
        // Arrange & Act
        var attr = new ExampleValueAttribute("test-value");

        // Assert
        attr.Value.ShouldBe("test-value");
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Value()
    {
        // Arrange & Act
        var attr = new ExampleValueAttribute(null);

        // Assert
        attr.Value.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Should_Accept_Integer_Value()
    {
        // Arrange & Act
        var attr = new ExampleValueAttribute(42);

        // Assert
        attr.Value.ShouldBe(42);
    }

    [Fact]
    public void Constructor_Should_Accept_Boolean_Value()
    {
        // Arrange & Act
        var attr = new ExampleValueAttribute(true);

        // Assert
        attr.Value.ShouldBe(true);
    }

    [Fact]
    public void Attribute_Should_Be_Applicable_To_Property_And_Field()
    {
        // Arrange & Act
        var usageAttr = typeof(ExampleValueAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .FirstOrDefault() as AttributeUsageAttribute;

        // Assert
        usageAttr.ShouldNotBeNull();
        usageAttr.ValidOn.HasFlag(AttributeTargets.Property).ShouldBeTrue();
        usageAttr.ValidOn.HasFlag(AttributeTargets.Field).ShouldBeTrue();
    }
}

public class EnumStringSchemaTransformerTests : EksenUnitTestBase
{
    private readonly EnumStringSchemaTransformer _transformer = new();

    [Fact]
    public async Task TransformAsync_Should_Set_String_Type_For_Enum()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        var context = TestContextFactory.CreateContext(typeof(TestEnum));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        (schema.Type!.Value & JsonSchemaType.String).ShouldBe(JsonSchemaType.String);
        (schema.Type!.Value & JsonSchemaType.Integer).ShouldBe((JsonSchemaType)0);
    }

    [Fact]
    public async Task TransformAsync_Should_Populate_Enum_Values()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        var context = TestContextFactory.CreateContext(typeof(TestEnum));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Enum.ShouldNotBeNull();
        schema.Enum.Count.ShouldBe(3);
    }

    [Fact]
    public async Task TransformAsync_Should_Use_EnumMember_Attribute_Values()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        var context = TestContextFactory.CreateContext(typeof(TestEnumWithMemberAttribute));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Enum.ShouldNotBeNull();
        var enumValues = schema.Enum.Select(e => e.ToString()).ToList();
        enumValues.ShouldContain("active-status");
        enumValues.ShouldContain("inactive-status");
    }

    [Fact]
    public async Task TransformAsync_Should_Not_Modify_NonEnum_Types()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var context = TestContextFactory.CreateContext(typeof(string));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Enum.ShouldBeNull();
    }
}

public class NotMappedSchemaTransformerTests : EksenUnitTestBase
{
    private readonly NotMappedSchemaTransformer _transformer = new();

    [Fact]
    public async Task TransformAsync_Should_Remove_NotMapped_Properties()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["InternalField"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        var context = TestContextFactory.CreateContext(typeof(TestModelWithNotMapped));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Properties.ShouldContainKey("Name");
        schema.Properties.ShouldNotContainKey("InternalField");
    }

    [Fact]
    public async Task TransformAsync_Should_Not_Remove_Regular_Properties()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["Description"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        var context = TestContextFactory.CreateContext(typeof(TestModelWithoutNotMapped));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Properties.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TransformAsync_Should_Skip_NonObject_Schemas()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Name"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        var context = TestContextFactory.CreateContext(typeof(string));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Properties.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_Should_Skip_When_Properties_Are_Null()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = null!
        };
        var context = TestContextFactory.CreateContext(typeof(TestModelWithNotMapped));

        // Act & Assert
        await Should.NotThrowAsync(
            () => _transformer.TransformAsync(schema, context, CancellationToken.None));
    }
}

#pragma warning disable CS0618

public class ObsoleteSchemaTransformerTests : EksenUnitTestBase
{
    private readonly ObsoleteSchemaTransformer _transformer = new();

    [Fact]
    public async Task TransformAsync_Should_Set_Deprecated_For_Obsolete_Type()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = TestContextFactory.CreateContext(typeof(ObsoleteTestModel));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Deprecated.ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_Should_Include_Deprecation_Message_In_Description()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = TestContextFactory.CreateContext(typeof(ObsoleteTestModel));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Description.ShouldNotBeNull();
        schema.Description.ShouldContain("Deprecated");
        schema.Description.ShouldContain("Use NewModel instead");
    }

    [Fact]
    public async Task TransformAsync_Should_Append_To_Existing_Description()
    {
        // Arrange
        var schema = new OpenApiSchema { Description = "Original description" };
        var context = TestContextFactory.CreateContext(typeof(ObsoleteTestModel));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Description.ShouldStartWith("Original description");
        schema.Description.ShouldContain("Deprecated");
    }

    [Fact]
    public async Task TransformAsync_Should_Not_Set_Deprecated_For_NonObsolete_Type()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = TestContextFactory.CreateContext(typeof(TestModelWithoutNotMapped));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Deprecated.ShouldBeFalse();
        schema.Description.ShouldBeNull();
    }
}

#region Test Models

public enum TestEnum
{
    Value1,
    Value2,
    Value3
}

public enum TestEnumWithMemberAttribute
{
    [EnumMember(Value = "active-status")]
    Active,

    [EnumMember(Value = "inactive-status")]
    Inactive
}

public class TestModelWithNotMapped
{
    public string Name { get; set; } = null!;

    [NotMapped]
    public string InternalField { get; set; } = null!;
}

public class TestModelWithoutNotMapped
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

#pragma warning disable CS0618
[Obsolete("Use NewModel instead")]
#pragma warning restore CS0618
public class ObsoleteTestModel
{
    public string OldProperty { get; set; } = null!;
}

#endregion
