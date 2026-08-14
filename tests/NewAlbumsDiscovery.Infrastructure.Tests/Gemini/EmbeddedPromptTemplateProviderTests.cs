using NewAlbumsDiscovery.Infrastructure.Gemini;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Gemini;

/// <summary>
/// Exercises the real embedded prompt template assets, mirroring
/// EmbeddedCountryMasterDataProviderTests' style - no mocking, this is the actual data shipping
/// with the assembly.
/// </summary>
public class EmbeddedPromptTemplateProviderTests
{
    private readonly EmbeddedPromptTemplateProvider _provider = new();

    [Theory]
    [InlineData("country-prompt.md", "{{country}}")]
    [InlineData("country-language-prompt.md", "{{language}}")]
    [InlineData("country-language-genres-prompt.md", "{{genres}}")]
    [InlineData("genre-expansion-prompt.md", "{{genre}}")]
    [InlineData("country-instrumental-prompt.md", "{{country}}")]
    [InlineData("country-instrumental-genres-prompt.md", "{{genres}}")]
    public async Task GetTemplateAsync_KnownTemplate_LoadsNonEmptyContentContainingExpectedToken(string templateFileName, string expectedToken)
    {
        var content = await _provider.GetTemplateAsync(templateFileName, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains(expectedToken, content);
    }

    [Fact]
    public async Task GetTemplateAsync_UnknownTemplate_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.GetTemplateAsync("does-not-exist.md", CancellationToken.None));
    }

    [Fact]
    public async Task GetTemplateAsync_DeletedCountryGenresTemplate_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.GetTemplateAsync("country-genres-prompt.md", CancellationToken.None));
    }

    [Fact]
    public async Task GetTemplateAsync_CalledTwice_ReturnsConsistentContent()
    {
        var first = await _provider.GetTemplateAsync("country-prompt.md", CancellationToken.None);
        var second = await _provider.GetTemplateAsync("country-prompt.md", CancellationToken.None);

        Assert.Equal(first, second);
    }
}
