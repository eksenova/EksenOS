using System.ComponentModel;
using System.Text.Json.Nodes;
using Eksen.OpenApi.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Eksen.OpenApi.Tests;

[ApiController, Route("orders"), Description("Place and read orders."), DisplayName("Orders")]
public sealed class ScopeOrdersController : ControllerBase
{
    [HttpGet]
    public OrderPage List([FromQuery] int skip = 0, [FromQuery] int take = 20) => new([], 0);

    [HttpGet("{id}/audit"), InternalApi]
    public AuditEntry Audit(string id) => new(id, "created");
}

[ApiController, Route("loader"), InternalApi, Description("Loader handshake used by game servers.")]
public sealed class ScopeLoaderController : ControllerBase
{
    [HttpPost]
    public LoaderReply Handshake(LoaderRequest request) => new(request.Version);
}

public sealed record OrderPage(IReadOnlyList<OrderRow> Items, long TotalCount);

public sealed record OrderRow(string Id);

public sealed record AuditEntry(string Id, string Action);

public sealed record LoaderRequest(string Version);

public sealed record LoaderReply(string Version);

public class DocumentScopeTests
{
    private static async Task<(JsonNode Public, JsonNode Internal)> GenerateAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(ScopeOrdersController).Assembly);
        builder.Services.Configure<OpenApiStandardDescriptions>(descriptions => descriptions
            .Add("skip", "Number of items to skip.")
            .Add("totalCount", "Number of matching items across all pages."));
        builder.Services.AddOpenApi("public", options => options.UseEksenDocumentScope(OpenApiDocumentScope.Public));
        builder.Services.AddOpenApi("internal", options => options.UseEksenDocumentScope(OpenApiDocumentScope.Internal));

        await using var app = builder.Build();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        var publicDocument = JsonNode.Parse(await client.GetStringAsync("/openapi/public.json"))!;
        var internalDocument = JsonNode.Parse(await client.GetStringAsync("/openapi/internal.json"))!;
        await app.StopAsync();

        return (publicDocument, internalDocument);
    }

    private static IEnumerable<string> PathsOf(JsonNode document) => document["paths"]!.AsObject().Select(entry => entry.Key);

    private static IEnumerable<string> SchemasOf(JsonNode document) =>
        document["components"]?["schemas"]?.AsObject().Select(entry => entry.Key) ?? [];

    [Fact]
    public async Task Public_Document_Should_Exclude_Internal_Controllers_And_Actions()
    {
        var (publicDocument, _) = await GenerateAsync();

        PathsOf(publicDocument).ShouldBe(["/orders"], ignoreOrder: true);
    }

    [Fact]
    public async Task Internal_Document_Should_Include_Every_Endpoint()
    {
        var (_, internalDocument) = await GenerateAsync();

        PathsOf(internalDocument).ShouldBe(["/orders", "/orders/{id}/audit", "/loader"], ignoreOrder: true);
    }

    [Fact]
    public async Task Public_Document_Should_Not_List_Models_Used_Only_By_Internal_Endpoints()
    {
        var (publicDocument, internalDocument) = await GenerateAsync();

        SchemasOf(publicDocument).ShouldNotContain("AuditEntry");
        SchemasOf(publicDocument).ShouldNotContain("LoaderRequest");
        SchemasOf(publicDocument).ShouldNotContain("LoaderReply");
        SchemasOf(internalDocument).ShouldContain("LoaderRequest");
    }

    [Fact]
    public async Task Markers_Should_Not_Reach_Clients()
    {
        var (publicDocument, internalDocument) = await GenerateAsync();

        publicDocument.ToJsonString().ShouldNotContain(InternalApi.MarkerExtension);
        internalDocument.ToJsonString().ShouldNotContain(InternalApi.MarkerExtension);
    }

    [Fact]
    public async Task Tags_Should_Carry_Controller_Description_And_Drop_Unused_Ones()
    {
        var (publicDocument, internalDocument) = await GenerateAsync();

        var publicTags = publicDocument["tags"]!.AsArray();
        publicTags.Count.ShouldBe(1);
        publicTags[0]!["name"]!.GetValue<string>().ShouldBe("ScopeOrders");
        publicTags[0]!["description"]!.GetValue<string>().ShouldBe("Place and read orders.");
        publicTags[0]!["x-displayName"]!.GetValue<string>().ShouldBe("Orders");

        internalDocument["tags"]!.AsArray().Select(tag => tag!["name"]!.GetValue<string>())
            .ShouldBe(["ScopeLoader", "ScopeOrders"], ignoreOrder: true);
    }

    [Fact]
    public async Task Standard_Descriptions_Should_Fill_Blank_Parameters_And_Properties()
    {
        var (publicDocument, _) = await GenerateAsync();

        var parameters = publicDocument["paths"]!["/orders"]!["get"]!["parameters"]!.AsArray();
        parameters.Single(p => p!["name"]!.GetValue<string>() == "skip")!["description"]!.GetValue<string>()
            .ShouldBe("Number of items to skip.");
        parameters.Single(p => p!["name"]!.GetValue<string>() == "take")!["description"].ShouldBeNull();

        publicDocument["components"]!["schemas"]!["OrderPage"]!["properties"]!["totalCount"]!["description"]!
            .GetValue<string>().ShouldBe("Number of matching items across all pages.");
    }
}
