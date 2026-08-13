using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;

namespace NewAlbumsDiscovery.Infrastructure.Gemini;

/// <summary>
/// Reads prompt templates from Gemini/Prompts as embedded resources, parsing each once and
/// caching for the process lifetime. Registered as a singleton, so instance-field caching is
/// sufficient (mirrors EmbeddedCountryMasterDataProvider's pattern).
/// </summary>
public sealed class EmbeddedPromptTemplateProvider : IPromptTemplateProvider
{
    private const string ResourceNamePrefix = "NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.";

    private readonly Dictionary<string, string> _cache = new();

    public Task<string> GetTemplateAsync(string templateFileName, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(templateFileName, out var content))
        {
            content = LoadTemplate(templateFileName);
            _cache[templateFileName] = content;
        }

        return Task.FromResult(content);
    }

    private static string LoadTemplate(string templateFileName)
    {
        var resourceName = ResourceNamePrefix + templateFileName;
        var assembly = typeof(EmbeddedPromptTemplateProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
