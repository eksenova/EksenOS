using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Eksen.UnitOfWork.AspNetCore;

public sealed class UnitOfWorkMiddleware(RequestDelegate next)
{
    private static readonly string[] IgnoredMethods = ["GET", "HEAD", "OPTIONS"];

    public async Task Invoke(HttpContext httpContext, IUnitOfWorkManager unitOfWorkManager)
    {
        var endpoint = httpContext.Features.Get<IEndpointFeature>()?.Endpoint;

        if (endpoint == null || IgnoredMethods.Any(method => httpContext.Request.Method.Equals(method, StringComparison.OrdinalIgnoreCase)))
        {
            await next(httpContext);
            return;
        }

        var attribute = endpoint.Metadata.GetMetadata<UnitOfWorkAttribute>() ?? new UnitOfWorkAttribute();
        if (!attribute.IsEnabled)
        {
            await next(httpContext);
            return;
        }

        IUnitOfWorkScope? scope = null;

        try
        {
            scope = unitOfWorkManager.BeginScope(isolationLevel: attribute.IsolationLevel);

            try
            {
                await next(httpContext);
            }
            catch (Exception)
            {
                await scope.RollbackAsync();
                throw;
            }

            await scope.CommitAsync();
        }
        finally
        {
            if (scope != null)
            {
                await scope.DisposeAsync();
            }
        }
    }
}