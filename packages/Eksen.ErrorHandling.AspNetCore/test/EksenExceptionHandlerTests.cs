using System.Net;
using System.Text.Json;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Eksen.ErrorHandling.AspNetCore.Tests;

public class EksenExceptionHandlerTests : EksenUnitTestBase
{
    private readonly Mock<ILogger<EksenExceptionHandler>> _logger = new();
    private readonly Mock<IErrorFormatter> _errorFormatter = new();

    private EksenExceptionHandler CreateHandler()
    {
        return new EksenExceptionHandler(_logger.Object, _errorFormatter.Object);
    }

    #region EksenException Handling

    [Fact]
    public async Task TryHandleAsync_Should_Return_True_For_EksenException()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Validation error occurred");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        var result = await handler.TryHandleAsync(httpContext, exception);

        // Assert
        result.ShouldBeTrue();

        _errorFormatter.Verify(
            f => f.FormatError(It.IsAny<IErrorData>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Set_BadRequest_For_Validation_Error()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Validation error");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Set_NotFound_For_NotFound_Error()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.NotFound, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Not found");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Set_Unauthorized_For_Authorization_Error()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Authorization, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Unauthorized");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Set_TooManyRequests_For_RateLimit_Error()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.RateLimit, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Rate limited");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Set_Conflict_For_Conflict_Error()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Conflict, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Conflict");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Set_InternalServerError_For_Unknown_ErrorType()
    {
        // Arrange
        var descriptor = new Mock<IErrorDescriptor>();
        descriptor.Setup(d => d.Code).Returns("TestModule.UnknownError");
        descriptor.Setup(d => d.ErrorType).Returns("CustomUnknownType");

        var errorData = new Mock<IErrorData>();
        errorData.Setup(e => e.Descriptor).Returns(descriptor.Object);
        errorData.Setup(e => e.Data).Returns(new Dictionary<string, object?>());

        var exception = new EksenException(descriptor.Object);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Unknown error");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Response Body

    [Fact]
    public async Task TryHandleAsync_Should_Write_ErrorMessage_To_Response()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("A validation error occurred");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        var body = await ReadResponseBody(httpContext);
        var response = JsonSerializer.Deserialize<ErrorResponseBody>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        response.ShouldNotBeNull();
        response.ErrorMessage.ShouldBe("A validation error occurred");
    }

    [Fact]
    public async Task TryHandleAsync_Should_Include_ErrorData_When_Present()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "TestModule");
        var instance = new ErrorInstance(descriptor)
            .WithData("field", "email")
            .WithData("reason", "invalid");
        var exception = new EksenException(instance);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Validation error");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        var body = await ReadResponseBody(httpContext);
        var response = JsonSerializer.Deserialize<ErrorResponseBody>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        response.ShouldNotBeNull();
        response.ErrorData.ShouldNotBeNull();
        response.ErrorData.ShouldContainKey("field");
        response.ErrorData.ShouldContainKey("reason");
    }

    [Fact]
    public async Task TryHandleAsync_Should_Not_Include_ErrorData_When_Empty()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "TestModule");
        var exception = new EksenException(descriptor);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Validation error");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        await handler.TryHandleAsync(httpContext, exception);

        // Assert
        var body = await ReadResponseBody(httpContext);
        var response = JsonSerializer.Deserialize<ErrorResponseBody>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        response.ShouldNotBeNull();
        response.ErrorData.ShouldBeNull();
    }

    #endregion

    #region Non-EksenException

    [Fact]
    public async Task TryHandleAsync_Should_Return_False_For_Non_EksenException()
    {
        // Arrange
        var exception = new InvalidOperationException("some error");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        var result = await handler.TryHandleAsync(httpContext, exception);

        // Assert
        result.ShouldBeFalse();

        _errorFormatter.Verify(
            f => f.FormatError(It.IsAny<IErrorData>()),
            Times.Never);
    }

    #endregion

    #region InnerException Unwrapping

    [Fact]
    public async Task TryHandleAsync_Should_Unwrap_InnerException_When_EksenException()
    {
        // Arrange
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "TestModule");
        var inner = new EksenException(descriptor);
        var wrapper = new Exception("wrapper", inner);

        _errorFormatter
            .Setup(f => f.FormatError(It.IsAny<IErrorData>()))
            .Returns("Inner validation error");

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        var result = await handler.TryHandleAsync(httpContext, wrapper);

        // Assert
        result.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);

        _errorFormatter.Verify(
            f => f.FormatError(It.IsAny<IErrorData>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return_False_When_InnerException_Is_Not_EksenException()
    {
        // Arrange
        var inner = new ArgumentException("arg error");
        var wrapper = new Exception("wrapper", inner);

        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        // Act
        var result = await handler.TryHandleAsync(httpContext, wrapper);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region Helpers

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.TraceIdentifier = "test-trace-id";
        return httpContext;
    }

    private static async Task<string> ReadResponseBody(HttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        return await reader.ReadToEndAsync();
    }

    #endregion
}
