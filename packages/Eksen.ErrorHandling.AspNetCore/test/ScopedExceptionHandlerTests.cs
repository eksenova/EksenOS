using Eksen.TestBase;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Eksen.ErrorHandling.AspNetCore.Tests;

public sealed class ScopedExceptionHandlerTests : EksenUnitTestBase
{
    [Fact]
    public async Task SingletonHandlerUsesEachRequestsScopedFormatter()
    {
        var formatterCount = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEksen(eksen => eksen.AddLocalization().AddErrorHandling(errors => errors.AddAspNetCoreSupport()));
        services.AddScoped<IErrorFormatter>(_ =>
        {
            var instance = ++formatterCount;
            var formatter = new Mock<IErrorFormatter>();
            formatter.Setup(x => x.FormatError(It.IsAny<IErrorData>())).Returns($"request-{instance}");
            return formatter.Object;
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var handler = provider.GetServices<IExceptionHandler>().Single();
        for (var request = 1; request <= 2; request++)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Response.Body = new MemoryStream();
            var exception = new EksenException(new ErrorDescriptor(ErrorType.Validation, "Proof", "Rejected"));
            (await handler.TryHandleAsync(context, exception, CancellationToken.None)).ShouldBeTrue();
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            (await reader.ReadToEndAsync()).ShouldContain($"request-{request}");
        }
        formatterCount.ShouldBe(2);
    }
}
