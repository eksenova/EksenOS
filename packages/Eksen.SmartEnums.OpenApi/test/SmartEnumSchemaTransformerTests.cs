using System.Text.Json.Nodes;
using Eksen.SmartEnums.OpenApi;
using Eksen.SmartEnums.OpenApi.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.OpenApi;
using Shouldly;

namespace Eksen.SmartEnums.OpenApi.Tests;

public class SmartEnumSchemaTransformerTests : EksenUnitTestBase
{
    [Fact]
    public void ProcessSchema_Should_Set_String_Type_For_Enumeration()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        SmartEnumSchemaTransformer.ProcessSchema(schema, typeof(TestColor));

        // Assert
        schema.Type.ShouldBe(JsonSchemaType.String);
    }

    [Fact]
    public void ProcessSchema_Should_Report_IsNullable_False_For_Non_Nullable()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        SmartEnumSchemaTransformer.ProcessSchema(
            schema, typeof(TestColor),
            out var isNullable, out _, out _);

        // Assert
        isNullable.ShouldBeFalse();
    }

    [Fact]
    public void ProcessSchema_Should_Populate_Enum_Values()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        SmartEnumSchemaTransformer.ProcessSchema(schema, typeof(TestColor));

        // Assert
        schema.Enum.ShouldNotBeNull();
        schema.Enum.Count.ShouldBe(3);
    }

    [Fact]
    public void ProcessSchema_Should_Set_Array_Type_For_Collection()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        SmartEnumSchemaTransformer.ProcessSchema(
            schema, typeof(List<TestColor>),
            out _, out var isCollection, out _);

        // Assert
        isCollection.ShouldBeTrue();
        schema.Type.ShouldBe(JsonSchemaType.Array);
        schema.Items.ShouldNotBeNull();
    }

    [Fact]
    public void ProcessSchema_Should_Not_Modify_Non_Enumeration_Type()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        SmartEnumSchemaTransformer.ProcessSchema(schema, typeof(string));

        // Assert
        schema.Type.ShouldBeNull();
        schema.Enum.ShouldBeNull();
    }

    [Fact]
    public void ProcessSchema_Should_Handle_Nested_Enumeration_Properties()
    {
        // Arrange
        var colorSchema = new OpenApiSchema();
        var nameSchema = new OpenApiSchema();
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Color"] = colorSchema,
                ["Name"] = nameSchema
            }
        };

        // Act
        SmartEnumSchemaTransformer.ProcessSchema(schema, typeof(ModelWithEnum));

        // Assert
        colorSchema.Type.ShouldNotBeNull();
        (colorSchema.Type!.Value & JsonSchemaType.String).ShouldBe(JsonSchemaType.String);
    }
}
