namespace NewAlbumsDiscovery.Application.AIDiscovery.Prompts;

public interface IPromptTemplateProvider
{
    Task<string> GetTemplateAsync(string templateFileName, CancellationToken cancellationToken);
}
