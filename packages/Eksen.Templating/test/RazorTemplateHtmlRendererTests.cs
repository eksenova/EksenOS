using System.Dynamic;
using Eksen.TestBase;
using Eksen.Templating.Html;
using Moq;
using RazorLight;
using Shouldly;

namespace Eksen.Templating.Tests;

public class RazorTemplateHtmlRendererTests : EksenUnitTestBase
{
    [Fact]
    public async Task RenderTemplateAsync_Should_Return_Rendered_Html()
    {
        // Arrange
        var expectedHtml = "<h1>Hello, World!</h1>";
        var templateKey = "test-template";
        var model = new { Name = "World" };

        var razorLightEngine = new Mock<IRazorLightEngine>();
        razorLightEngine
            .Setup(e => e.CompileRenderAsync(
                templateKey,
                model,
                It.IsAny<ExpandoObject?>()))
            .ReturnsAsync(expectedHtml);

        var renderer = new RazorTemplateHtmlRenderer(razorLightEngine.Object);

        // Act
        var result = await renderer.RenderTemplateAsync(templateKey, model);

        // Assert
        result.ShouldBe(expectedHtml);

        razorLightEngine.Verify(
            e => e.CompileRenderAsync(templateKey, model, It.IsAny<ExpandoObject?>()),
            Times.Once);
    }

    [Fact]
    public async Task RenderTemplateAsync_Should_Pass_ViewBag_To_Engine()
    {
        // Arrange
        var templateKey = "template-with-viewbag";
        var model = new { Title = "Test" };
        var viewBag = new ExpandoObject();
        ((IDictionary<string, object?>)viewBag)["PageTitle"] = "My Page";

        var razorLightEngine = new Mock<IRazorLightEngine>();
        razorLightEngine
            .Setup(e => e.CompileRenderAsync(
                templateKey,
                model,
                viewBag))
            .ReturnsAsync("<title>My Page</title>");

        var renderer = new RazorTemplateHtmlRenderer(razorLightEngine.Object);

        // Act
        var result = await renderer.RenderTemplateAsync(templateKey, model, viewBag);

        // Assert
        result.ShouldBe("<title>My Page</title>");

        razorLightEngine.Verify(
            e => e.CompileRenderAsync(templateKey, model, viewBag),
            Times.Once);
    }

    [Fact]
    public async Task RenderTemplateAsync_Should_Pass_Null_ViewBag_By_Default()
    {
        // Arrange
        var templateKey = "template";
        var model = new { };

        var razorLightEngine = new Mock<IRazorLightEngine>();
        razorLightEngine
            .Setup(e => e.CompileRenderAsync(
                templateKey,
                model,
                null))
            .ReturnsAsync("<div></div>");

        var renderer = new RazorTemplateHtmlRenderer(razorLightEngine.Object);

        // Act
        var result = await renderer.RenderTemplateAsync(templateKey, model);

        // Assert
        result.ShouldBe("<div></div>");

        razorLightEngine.Verify(
            e => e.CompileRenderAsync(templateKey, model, null),
            Times.Once);
    }
}
