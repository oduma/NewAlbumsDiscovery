namespace NewAlbumsDiscovery.Application.AIDiscovery;

public sealed record GeminiCallResult(bool IsSuccess, bool IsTransientFailure, string? ResponseText, string? ErrorMessage)
{
    public static GeminiCallResult Success(string responseText) => new(true, false, responseText, null);

    public static GeminiCallResult Transient(string errorMessage) => new(false, true, null, errorMessage);

    public static GeminiCallResult Permanent(string errorMessage) => new(false, false, null, errorMessage);
}
