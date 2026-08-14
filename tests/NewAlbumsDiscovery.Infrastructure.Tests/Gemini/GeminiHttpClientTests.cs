using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Infrastructure.Gemini;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Gemini;

public class GeminiHttpClientTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _exception;
    }

    private static GeminiHttpClient CreateClient(
        HttpMessageHandler handler,
        string model = "gemini-3.5-flash",
        string apiKey = "test-api-key")
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { Model = model });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["NewAlbumsDiscovery:GeminiApiKey"] = apiKey })
            .Build();
        return new GeminiHttpClient(httpClient, options, configuration);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode) { Content = new StringContent(body) };

    [Fact]
    public async Task GenerateContentAsync_WithWellFormedSuccessResponse_ExtractsResponseText()
    {
        const string body = """
        {
          "candidates": [
            { "content": { "parts": [ { "text": "[\"Indie Pop\",\"Alt Pop\"]" } ] } }
          ]
        }
        """;
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        var result = await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsTransientFailure);
        Assert.Equal("[\"Indie Pop\",\"Alt Pop\"]", result.ResponseText);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GenerateContentAsync_WithNonTransientStatusCode_ReturnsPermanentFailure(HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(statusCode, string.Empty));
        var client = CreateClient(handler);

        var result = await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransientFailure);
        Assert.Null(result.ResponseText);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GenerateContentAsync_WithTransientStatusCode_ReturnsTransientFailure(HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(statusCode, string.Empty));
        var client = CreateClient(handler);

        var result = await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransientFailure);
    }

    [Fact]
    public async Task GenerateContentAsync_WhenHandlerThrowsHttpRequestException_ReturnsTransientFailure()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection reset"));
        var client = CreateClient(handler);

        var result = await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransientFailure);
        Assert.Contains("connection reset", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateContentAsync_WhenHandlerThrowsTaskCanceledExceptionWithoutCancellation_ReturnsTransientFailure()
    {
        var handler = new ThrowingHttpMessageHandler(new TaskCanceledException("request timed out"));
        var client = CreateClient(handler);

        var result = await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransientFailure);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateContentAsync_WithSuccessStatusButMalformedBody_ReturnsPermanentFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "{ \"unexpected\": true }"));
        var client = CreateClient(handler);

        var result = await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransientFailure);
    }

    [Fact]
    public async Task GenerateContentAsync_SendsApiKeyHeaderAndModelInUrl()
    {
        const string body = """
        {
          "candidates": [ { "content": { "parts": [ { "text": "[]" } ] } } ]
        }
        """;
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler, model: "gemini-3.5-flash", apiKey: "my-secret-key");

        await client.GenerateContentAsync("prompt", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("my-secret-key", handler.LastRequest!.Headers.GetValues("x-goog-api-key").Single());
        Assert.Contains("gemini-3.5-flash", handler.LastRequest.RequestUri!.ToString());
    }
}
