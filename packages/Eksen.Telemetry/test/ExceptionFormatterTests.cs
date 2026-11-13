using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests;

public class ExceptionFormatterTests : EksenUnitTestBase
{
    [Fact]
    public void Format_Should_Split_Type_Message_And_StackTrace_Without_Message()
    {
        // Arrange
        var exception = Capture(() => Throw(new InvalidOperationException("outer failure", Capture(() => Throw(new ArgumentException("inner failure"))))));

        // Act
        var formatted = ExceptionFormatter.Format(exception);

        // Assert
        formatted.Type.ShouldBe("System.InvalidOperationException");
        formatted.Message.ShouldBe("outer failure");
        formatted.StackTrace.ShouldNotBeNull();
        formatted.StackTrace.ShouldNotContain("outer failure");
        formatted.StackTrace.ShouldNotContain("inner failure");
        formatted.StackTrace.ShouldContain(" ---> [System.ArgumentException]");
        formatted.StackTrace.ShouldContain(nameof(Throw));
    }

    [Fact]
    public void Format_Of_An_Unthrown_Exception_Should_Have_No_StackTrace()
    {
        var formatted = ExceptionFormatter.Format(new TimeoutException("late"));

        formatted.StackTrace.ShouldBeNull();
        formatted.ToAttributes().Select(a => a.Key)
            .ShouldBe(["exception.type", "exception.message"]);
    }

    [Fact]
    public void Generic_Type_Names_Should_Be_Readable()
    {
        ExceptionFormatter.Format(new GenericException<int>()).Type
            .ShouldBe("Eksen.Telemetry.Tests.ExceptionFormatterTests+GenericException`1[System.Int32]");
    }

    private static void Throw(Exception exception)
    {
        throw exception;
    }

    private static Exception Capture(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected an exception.");
    }

    private sealed class GenericException<T> : Exception;
}
