using System.Linq.Expressions;
using Eksen.Repositories;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class BaseIncludeOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Includes_Should_Default_To_Null()
    {
        // Arrange & Act
        var options = new BaseIncludeOptions<FakeEntity>();

        // Assert
        options.Includes.ShouldBeNull();
    }

    [Fact]
    public void IgnoreAutoIncludes_Should_Default_To_False()
    {
        // Arrange & Act
        var options = new BaseIncludeOptions<FakeEntity>();

        // Assert
        options.IgnoreAutoIncludes.ShouldBeFalse();
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_From_List()
    {
        // Arrange
        var includes = new List<Expression<Func<FakeEntity, object>>>
        {
            x => x.Name
        };

        // Act
        BaseIncludeOptions<FakeEntity> options = includes;

        // Assert
        options.Includes.ShouldNotBeNull();
        options.Includes.Count.ShouldBe(1);
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_From_Array()
    {
        // Arrange
        Expression<Func<FakeEntity, object>>[] includes = [x => x.Name, x => x.Value];

        // Act
        BaseIncludeOptions<FakeEntity> options = includes;

        // Assert
        options.Includes.ShouldNotBeNull();
        options.Includes.Count.ShouldBe(2);
    }
}

public class DefaultIncludeOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Should_Inherit_BaseIncludeOptions()
    {
        // Arrange & Act
        var options = new DefaultIncludeOptions<FakeEntity>();

        // Assert
        options.ShouldBeAssignableTo<BaseIncludeOptions<FakeEntity>>();
    }
}
