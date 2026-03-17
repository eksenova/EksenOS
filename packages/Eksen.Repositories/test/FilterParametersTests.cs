using System.Linq.Expressions;
using Eksen.Repositories;
using Eksen.TestBase;
using Eksen.ValueObjects.Entities;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class FakeEntity : IEntity
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public class BaseFilterParametersTests : EksenUnitTestBase
{
    [Fact]
    public void Predicate_Should_Default_To_Null()
    {
        // Arrange & Act
        var filter = new BaseFilterParameters<FakeEntity>();

        // Assert
        filter.Predicate.ShouldBeNull();
    }

    [Fact]
    public void Predicate_Should_Be_Settable()
    {
        // Arrange
        Expression<Func<FakeEntity, bool>> predicate = x => x.Value > 5;

        // Act
        var filter = new BaseFilterParameters<FakeEntity> { Predicate = predicate };

        // Assert
        filter.Predicate.ShouldBe(predicate);
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_From_Expression()
    {
        // Arrange
        Expression<Func<FakeEntity, bool>> predicate = x => x.Name == "test";

        // Act
        BaseFilterParameters<FakeEntity> filter = predicate;

        // Assert
        filter.Predicate.ShouldBe(predicate);
    }

    [Fact]
    public void ToFilterExpression_Should_Return_Predicate()
    {
        // Arrange
        Expression<Func<FakeEntity, bool>> predicate = x => x.Value == 1;
        var filter = new BaseFilterParameters<FakeEntity> { Predicate = predicate };

        // Act
        var result = filter.ToFilterExpression();

        // Assert
        result.ShouldBe(predicate);
    }

    [Fact]
    public void ToFilterExpression_Should_Return_Null_When_No_Predicate()
    {
        // Arrange
        var filter = new BaseFilterParameters<FakeEntity>();

        // Act
        var result = filter.ToFilterExpression();

        // Assert
        result.ShouldBeNull();
    }
}

public class DefaultFilterParametersTests : EksenUnitTestBase
{
    [Fact]
    public void Should_Inherit_BaseFilterParameters()
    {
        // Arrange & Act
        var filter = new DefaultFilterParameters<FakeEntity>();

        // Assert
        filter.ShouldBeAssignableTo<BaseFilterParameters<FakeEntity>>();
    }
}
