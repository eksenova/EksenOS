using Eksen.TestBase;
using Shouldly;

namespace Eksen.Ulid.Tests;

public sealed record SampleUlidEntityId(System.Ulid Value) : UlidEntityId<SampleUlidEntityId>(Value);

public class UlidEntityIdTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Return_Id_With_Given_Value()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id = SampleUlidEntityId.Create(ulid);

        // Assert
        id.Value.ShouldBe(ulid);
    }

    [Fact]
    public void NewId_Should_Generate_Unique_Id()
    {
        // Arrange & Act
        var id1 = SampleUlidEntityId.NewId();
        var id2 = SampleUlidEntityId.NewId();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void Empty_Should_Return_Empty_Ulid()
    {
        // Arrange & Act
        var empty = SampleUlidEntityId.Empty;

        // Assert
        empty.Value.ShouldBe(System.Ulid.Empty);
    }

    [Fact]
    public void Equal_Ids_Should_Be_Equal()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id1 = SampleUlidEntityId.Create(ulid);
        var id2 = SampleUlidEntityId.Create(ulid);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var id1 = SampleUlidEntityId.NewId();
        var id2 = SampleUlidEntityId.NewId();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void ToString_Should_Return_Ulid_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = SampleUlidEntityId.Create(ulid);

        // Act
        var result = id.ToString();

        // Assert
        result.ShouldContain(ulid.ToString());
    }

    [Fact]
    public void Parse_Should_Return_Id_From_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var str = ulid.ToString();

        // Act
        var id = SampleUlidEntityId.Parse(str);

        // Assert
        id.Value.ShouldBe(ulid);
    }

    [Fact]
    public void TryParse_Should_Return_True_For_Valid_Ulid()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var str = ulid.ToString();

        // Act
        var success = SampleUlidEntityId.TryParse(str, provider: null, out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Value.ShouldBe(ulid);
    }

    [Fact]
    public void TryParse_Should_Return_False_For_Invalid_String()
    {
        // Arrange & Act
        var success = SampleUlidEntityId.TryParse("invalid", provider: null, out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void CompareTo_Should_Compare_Values()
    {
        // Arrange
        var id1 = SampleUlidEntityId.NewId();
        var id2 = SampleUlidEntityId.NewId();

        // Act
        var comparison = id1.CompareTo(id2);

        // Assert
        comparison.ShouldNotBe(0);
    }

    [Fact]
    public void Equals_Ulid_Should_Return_True_For_Same_Value()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = SampleUlidEntityId.Create(ulid);

        // Act & Assert
        id.Equals(ulid).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Ulid_Should_Return_False_For_Different_Value()
    {
        // Arrange
        var id = SampleUlidEntityId.NewId();

        // Act & Assert
        id.Equals(System.Ulid.NewUlid()).ShouldBeFalse();
    }

    [Fact]
    public void Length_Should_Be_26()
    {
        // Assert
        SampleUlidEntityId.Length.ShouldBe(26);
    }

    [Fact]
    public void Explicit_Conversion_To_Ulid_Should_Work()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = SampleUlidEntityId.Create(ulid);

        // Act
        var result = (System.Ulid)id;

        // Assert
        result.ShouldBe(ulid);
    }

    [Fact]
    public void Explicit_Conversion_To_String_Should_Work()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = SampleUlidEntityId.Create(ulid);

        // Act
        var result = (string)id;

        // Assert
        result.ShouldBe(ulid.ToString());
    }

    [Fact]
    public void ToParseableString_Should_Return_Ulid_Value()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = SampleUlidEntityId.Create(ulid);

        // Act
        var result = id.ToParseableString();

        // Assert
        result.ShouldBe(ulid.ToString());
    }
}
