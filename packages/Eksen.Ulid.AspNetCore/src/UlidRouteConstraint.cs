using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Eksen.Ulid.AspNetCore;

public sealed class UlidRouteConstraint : IRouteConstraint
{
    public const string UlidContraint = "ulid";

    public bool Match(
        HttpContext? httpContext, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var routeValue))
        {
            return false;
        }

        var routeValueString = Convert.ToString(routeValue, CultureInfo.InvariantCulture);
        if (routeValueString is null)
        {
            return false;
        }

        return System.Ulid.TryParse(routeValueString, out _);
    }
}