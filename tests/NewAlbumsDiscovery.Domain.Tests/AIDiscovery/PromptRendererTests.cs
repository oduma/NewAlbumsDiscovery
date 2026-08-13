using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class PromptRendererTests
{
    private readonly PromptRenderer _renderer = new();

    [Fact]
    public void Render_WithNullTemplate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.Render(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public void Render_WithNullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.Render("template", null!));
    }

    [Fact]
    public void Render_WithSingleToken_ReplacesIt()
    {
        var result = _renderer.Render("Hello {{name}}!", new Dictionary<string, string> { ["name"] = "World" });

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void Render_WithMultipleDistinctTokens_ReplacesAll()
    {
        var result = _renderer.Render(
            "{{country}} - {{language}}",
            new Dictionary<string, string> { ["country"] = "Romania", ["language"] = "Romanian" });

        Assert.Equal("Romania - Romanian", result);
    }

    [Fact]
    public void Render_WithRepeatedToken_ReplacesEveryOccurrence()
    {
        var result = _renderer.Render(
            "{{country}} is in {{country}}'s region.",
            new Dictionary<string, string> { ["country"] = "Romania" });

        Assert.Equal("Romania is in Romania's region.", result);
    }

    [Fact]
    public void Render_WithUnusedDictionaryKey_HasNoEffect()
    {
        var result = _renderer.Render(
            "Hello {{name}}!",
            new Dictionary<string, string> { ["name"] = "World", ["unused"] = "ignored" });

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void Render_WithTokenMissingFromValues_LeavesTokenUntouched()
    {
        var result = _renderer.Render("Hello {{name}}!", new Dictionary<string, string>());

        Assert.Equal("Hello {{name}}!", result);
    }

    [Fact]
    public void Render_WithEmptyValues_ReturnsTemplateUnchanged()
    {
        const string template = "No tokens here.";

        var result = _renderer.Render(template, new Dictionary<string, string>());

        Assert.Equal(template, result);
    }
}
