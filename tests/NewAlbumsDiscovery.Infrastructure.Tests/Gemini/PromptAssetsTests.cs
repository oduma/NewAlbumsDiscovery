using System.Text.Json;
using NewAlbumsDiscovery.Infrastructure.Sqlite;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Gemini;

/// <summary>
/// Pure embedded-resource wiring sanity checks for the Phase 5 asset relocation
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 → §2 Asset Relocation).
/// No business logic consumes these files yet.
/// </summary>
public class PromptAssetsTests
{
    [Theory]
    [InlineData("NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.genre-expansion-prompt.md")]
    public void EmbeddedPromptTemplate_LoadsAndIsNonEmpty(string resourceName)
    {
        var content = ReadResource(resourceName);

        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Fact]
    public void CountriesLanguagesJson_LoadsAndParsesAsValidJson()
    {
        var content = ReadResource("NewAlbumsDiscovery.Infrastructure.Gemini.Assets.countries-languages.json");

        Assert.False(string.IsNullOrWhiteSpace(content));
        using var document = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    private static string ReadResource(string resourceName)
    {
        var assembly = typeof(AppDbContext).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
