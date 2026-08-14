# Phase 9 Implementation Plan: Gemini API Integration (Genre Expansion Only)

**Status:** ✅ COMPLETE (2026-08-14) — all 9 steps implemented and tested. See `FUNCTIONAL_REQUIREMENTS.md` → Phase 9 → As-Built Corrections for the one deviation from this plan (`GeminiOptions.ApiKey` dropped in favor of direct `IConfiguration` reads in `GeminiHttpClient`) and the one coverage gap found and closed during Step 9.

**Requirements:** [`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`](../requirements/FUNCTIONAL_REQUIREMENTS.md) → Phase 9.

Strict TDD (Red-Green-Refactor, 100% branch coverage on Domain + Application) per `docs/constitution/tdd.md`; Domain zero-dependency purity per `docs/constitution/DDD-architecture.md` (no Domain changes this phase — everything lands in Application/Infrastructure).

---

## Step 1 ✅ — Application: `IGeminiClient` port + `GeminiCallResult`

**New file:** `src/NewAlbumsDiscovery.Application/AIDiscovery/IGeminiClient.cs`

```csharp
namespace NewAlbumsDiscovery.Application.AIDiscovery;

public interface IGeminiClient
{
    Task<GeminiCallResult> GenerateContentAsync(string prompt, CancellationToken cancellationToken);
}
```

**New file:** `src/NewAlbumsDiscovery.Application/AIDiscovery/GeminiCallResult.cs`

```csharp
namespace NewAlbumsDiscovery.Application.AIDiscovery;

public sealed record GeminiCallResult(bool IsSuccess, bool IsTransientFailure, string? ResponseText, string? ErrorMessage)
{
    public static GeminiCallResult Success(string responseText) => new(true, false, responseText, null);

    public static GeminiCallResult Transient(string errorMessage) => new(false, true, null, errorMessage);

    public static GeminiCallResult Permanent(string errorMessage) => new(false, false, null, errorMessage);
}
```

No dedicated test file — a pure port + record with no branching logic, mirroring `IPromptTemplateProvider`'s precedent (exercised indirectly through consumers' tests).

---

## Step 2 ✅ — Application: `BucketProcessingState` + `IBucketProcessingStep` signature change

**New file:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/BucketProcessingState.cs`

```csharp
namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Mutable, per-bucket coordination state threaded through one bucket's step loop inside
/// BucketProcessingStage. Not a Domain concept — an Application-only orchestration artifact,
/// recreated fresh for every bucket (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 9).
/// </summary>
public sealed class BucketProcessingState
{
    public string? ResolvedGenres { get; set; }

    public bool IsAbandoned { get; private set; }

    public void Abandon() => IsAbandoned = true;
}
```

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/IBucketProcessingStep.cs`
- Change signature to:
  ```csharp
  Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, CancellationToken cancellationToken);
  ```

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/PrintBucketStep.cs`
- Add the `BucketProcessingState state` parameter (unused — this step never reads or writes it):
  ```csharp
  public Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, CancellationToken cancellationToken)
      => _notifier.NotifyBucketProcessedAsync(bucket.BucketName, bucket.TrackCount, cancellationToken);
  ```

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/PrintBucketStepTests.cs`
- Update call sites to pass `new BucketProcessingState()`. No new test cases — behavior unchanged.

Red (signature mismatch fails compilation across all three step classes + their tests) → apply changes → green.

---

## Step 3 ✅ — Application: pipeline reporting (`AbandonedBucketCount` + `IDiscoveryNotifier`)

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/AIDiscoveryPipelineContext.cs`
```csharp
public sealed record AIDiscoveryPipelineContext(
    IReadOnlyList<AggregatedBucket> SortedBuckets,
    int ProcessedBucketCount = 0,
    int AbandonedBucketCount = 0);
```

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/IDiscoveryNotifier.cs`
- Add:
  ```csharp
  Task NotifyBucketAbandonedAsync(string bucketName, int trackCount, string reason, CancellationToken cancellationToken);
  ```
- Change:
  ```csharp
  Task NotifyPipelineCompletedAsync(int processedBucketCount, int abandonedBucketCount, CancellationToken cancellationToken);
  ```

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/ReportPublicationStage.cs`
- Update the call site:
  ```csharp
  await _notifier.NotifyPipelineCompletedAsync(context.ProcessedBucketCount, context.AbandonedBucketCount, cancellationToken);
  ```

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/ReportPublicationStageTests.cs`
- Update the mock verification to the new two-int signature; add a case asserting `AbandonedBucketCount` flows through correctly when non-zero.

Red → green.

---

## Step 4 ✅ — Application: `GeminiOptions` extension + binding

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/GeminiOptions.cs`
```csharp
public sealed class GeminiOptions
{
    public string Model { get; set; } = "gemini-3.5-flash";
    public int[] RetryBackoffSeconds { get; set; } = [10, 30, 180];
    public string ApiKey { get; set; } = string.Empty;
}
```

**File:** `src/NewAlbumsDiscovery.Application/Common/ApplicationServiceCollectionExtensions.cs`
- After the existing `services.Configure<GeminiOptions>(configuration.GetSection("NewAlbumsDiscovery:Gemini"));`, add:
  ```csharp
  services.PostConfigure<GeminiOptions>(o => o.ApiKey = configuration["NewAlbumsDiscovery:GeminiApiKey"] ?? string.Empty);
  ```
  (`ApiKey` lives outside the nested `Gemini:` section, per the flat env-var convention established in Phase 2/4 — bound separately via `PostConfigure` rather than `Bind`.)

No dedicated test file for this options POCO, mirroring `AIDiscoveryOptions`'s precedent — exercised indirectly through Step 5/7's tests.

---

## Step 5 ✅ — Application: `GenreExpansionPromptStep` rewrite (the core of this phase)

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/GenreExpansionPromptStep.cs`

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

public sealed class GenreExpansionPromptStep : IBucketProcessingStep
{
    private const string TemplateFileName = "genre-expansion-prompt.md";

    private readonly IPromptTemplateProvider _templates;
    private readonly PromptRenderer _renderer;
    private readonly IDiscoveryNotifier _notifier;
    private readonly IGeminiClient _geminiClient;
    private readonly IOptions<AIDiscoveryOptions> _aiDiscoveryOptions;
    private readonly IOptions<GeminiOptions> _geminiOptions;
    private readonly TimeProvider _timeProvider;

    public GenreExpansionPromptStep(
        IPromptTemplateProvider templates,
        PromptRenderer renderer,
        IDiscoveryNotifier notifier,
        IGeminiClient geminiClient,
        IOptions<AIDiscoveryOptions> aiDiscoveryOptions,
        IOptions<GeminiOptions> geminiOptions,
        TimeProvider timeProvider)
    {
        _templates = templates;
        _renderer = renderer;
        _notifier = notifier;
        _geminiClient = geminiClient;
        _aiDiscoveryOptions = aiDiscoveryOptions;
        _geminiOptions = geminiOptions;
        _timeProvider = timeProvider;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, CancellationToken cancellationToken)
    {
        if (bucket.Genre is null || bucket.IsInstrumental(_aiDiscoveryOptions.Value.InstrumentalLanguage))
        {
            return;
        }

        var template = await _templates.GetTemplateAsync(TemplateFileName, cancellationToken);
        var values = new Dictionary<string, string>
        {
            ["genre"] = bucket.Genre,
            ["country"] = bucket.Country,
            ["language"] = bucket.Language ?? string.Empty,
        };
        var prompt = _renderer.Render(template, values);

        var backoffs = _geminiOptions.Value.RetryBackoffSeconds;

        for (var attempt = 0; ; attempt++)
        {
            var result = await _geminiClient.GenerateContentAsync(prompt, cancellationToken);

            if (result.IsSuccess)
            {
                state.ResolvedGenres = ResolveGenres(result.ResponseText!, bucket.Genre);
                return;
            }

            if (result.IsTransientFailure && attempt < backoffs.Length)
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffs[attempt]), _timeProvider, cancellationToken);
                continue;
            }

            await _notifier.NotifyBucketAbandonedAsync(
                bucket.BucketName, bucket.TrackCount, result.ErrorMessage ?? "Gemini API call failed.", cancellationToken);
            state.Abandon();
            return;
        }
    }

    private static string ResolveGenres(string responseText, string fallbackGenre)
    {
        try
        {
            var genres = JsonSerializer.Deserialize<string[]>(responseText);
            return genres is { Length: > 0 } ? string.Join(", ", genres) : fallbackGenre;
        }
        catch (JsonException)
        {
            return fallbackGenre;
        }
    }
}
```

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/GenreExpansionPromptStepTests.cs` — rewritten around a `Mock<IGeminiClient>` and a fake/manual `TimeProvider` (mirroring this codebase's existing hand-rolled `TimeProvider` test precedent, not `Microsoft.Extensions.TimeProvider.Testing`). Cases:
- `ProcessAsync_WithGenreButInstrumental_NeverCallsGeminiClient` / `ProcessAsync_WithNullGenre_NeverCallsGeminiClient` — guard clause updated from the old `NeverRequestsTemplateOrNotifies` assertion to `_geminiClient.Verify(..., Times.Never)`.
- `ProcessAsync_WithSuccessfulCall_NeverPrintsPromptToConsole` — asserts `NotifyPromptRenderedAsync` is never called (Decision 8).
- `ProcessAsync_WithSuccessfulCall_JoinsJsonArrayIntoResolvedGenres` — `IGeminiClient` returns `GeminiCallResult.Success("[\"Genre A\",\"Genre B\"]")`; asserts `state.ResolvedGenres == "Genre A, Genre B"`, `state.IsAbandoned == false`.
- `ProcessAsync_WithEmptyJsonArray_FallsBackToOriginalGenre` — response `"[]"` → `state.ResolvedGenres == bucket.Genre`.
- `ProcessAsync_WithUnparsableJson_FallsBackToOriginalGenre` — response `"not json"` → `state.ResolvedGenres == bucket.Genre`, no retry (single call verified).
- `ProcessAsync_WithTransientFailureThenSuccess_RetriesOnceAndSucceeds` — first call `Transient(...)`, second `Success(...)`; asserts exactly one `Task.Delay`-equivalent wait of 10s occurred (via the fake `TimeProvider`'s recorded delay) and `_geminiClient` was called exactly twice.
- `ProcessAsync_WithAllRetriesExhausted_AbandonsAfterConfiguredBackoffs` — three `Transient(...)` results in a row (matching `[10, 30, 180]`'s length) then a fourth attempt still transient → asserts 4 total calls, delays of `10s, 30s, 180s` in that order, `state.IsAbandoned == true`, `NotifyBucketAbandonedAsync` called once with the bucket's name/track count.
- `ProcessAsync_WithNonTransientFailure_AbandonsImmediatelyWithoutRetrying` — `Permanent(...)` on the first call → asserts exactly one call, zero delays, `state.IsAbandoned == true`.
- `ProcessAsync_WithNonDefaultRetryBackoffSeconds_UsesConfiguredValues` — `CreateStep` helper extended to accept a custom `RetryBackoffSeconds` array, proving the ladder is genuinely configuration-driven and not hardcoded.

Red → green, then confirm cobertura shows `branch-rate="1"` for the new `attempt`/`IsTransientFailure`/`IsSuccess` branches (including the async state machine).

---

## Step 6 ✅ — Application: `DiscoveryQueryPromptStep` consumes resolved genres + `BucketProcessingStage` short-circuit

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/DiscoveryQueryPromptStep.cs`
- Add `BucketProcessingState state` to `ProcessAsync`'s signature.
- Replace:
  ```csharp
  values["genres"] = isInstrumental ? bucket.Genre : "TBD";
  ```
  with:
  ```csharp
  values["genres"] = isInstrumental ? bucket.Genre : state.ResolvedGenres!;
  ```
  (Safe: `BucketProcessingStage`'s short-circuit guarantees this step never runs for a bucket whose `GenreExpansionPromptStep` call abandoned, so `ResolvedGenres` is always set by the time a non-instrumental `Genre`-bearing bucket reaches here.)

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/BucketProcessingStage.cs`
```csharp
public async Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken)
{
    var buckets = context.SortedBuckets;
    var processedCount = 0;
    var abandonedCount = 0;

    for (var i = 0; i < buckets.Count; i++)
    {
        var bucket = buckets[i];
        var state = new BucketProcessingState();

        foreach (var step in _steps)
        {
            await step.ProcessAsync(bucket, state, cancellationToken);

            if (state.IsAbandoned)
            {
                break;
            }
        }

        processedCount++;
        if (state.IsAbandoned)
        {
            abandonedCount++;
        }

        if (i < buckets.Count - 1)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.Value.InterBucketDelaySeconds), _timeProvider, cancellationToken);
        }
    }

    return context with { ProcessedBucketCount = processedCount, AbandonedBucketCount = abandonedCount };
}
```

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/DiscoveryQueryPromptStepTests.cs`
- Replace every test's hardcoded `"TBD"` assertion for non-instrumental `CountryLanguageGenre` buckets with a `BucketProcessingState { ResolvedGenres = "..." }` passed in, asserting `{{genres}}` renders that value.
- Instrumental-bucket tests (Phase 8) unchanged in intent, updated only for the new `state` parameter (unused for instrumental paths).

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/BucketProcessingStageTests.cs`
- Add `ExecuteAsync_WhenAStepAbandonsTheBucket_SkipsRemainingStepsForThatBucketOnly` — a 3-step mock sequence where step 2 calls `state.Abandon()`; asserts step 3 is never invoked for that bucket, but the next bucket's steps 1–3 all run normally.
- Add `ExecuteAsync_WithAbandonedBuckets_ReturnsCorrectAbandonedBucketCount` — asserts `AbandonedBucketCount` in the returned context matches the number of buckets whose state was abandoned, and `ProcessedBucketCount` still counts every bucket including abandoned ones.
- Existing tests updated for the new `BucketProcessingState` parameter on mocked steps.

Red → green, then confirm cobertura shows `branch-rate="1"` for the new `state.IsAbandoned` branches in both files.

---

## Step 7 ✅ — Infrastructure: `GeminiHttpClient` adapter + DI wiring + fail-fast key check

**New file:** `src/NewAlbumsDiscovery.Infrastructure/Gemini/GeminiHttpClient.cs`

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery;

namespace NewAlbumsDiscovery.Infrastructure.Gemini;

public sealed class GeminiHttpClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<GeminiOptions> _options;

    public GeminiHttpClient(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<GeminiCallResult> GenerateContentAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Value.Model}:generateContent";
        var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody),
        };
        request.Headers.Add("x-goog-api-key", _options.Value.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return GeminiCallResult.Transient($"Network error calling Gemini API: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return GeminiCallResult.Transient($"Timeout calling Gemini API: {ex.Message}");
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;

            if (statusCode is 400 or 401 or 403)
            {
                return GeminiCallResult.Permanent($"Gemini API returned {statusCode} {response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return GeminiCallResult.Transient($"Gemini API returned {statusCode} {response.StatusCode}.");
            }

            var text = await ExtractResponseTextAsync(response, cancellationToken);
            return text is null
                ? GeminiCallResult.Permanent("Gemini API returned 200 OK with an unexpected response shape.")
                : GeminiCallResult.Success(text);
        }
    }

    private static async Task<string?> ExtractResponseTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IndexOutOfRangeException or KeyNotFoundException)
        {
            return null;
        }
    }
}
```

**File:** `Directory.Packages.props` — add `<PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.0" />` (aligned with the other `Microsoft.Extensions.*` 10.0.0/10.0.8 pins already present).

**File:** `src/NewAlbumsDiscovery.Infrastructure/NewAlbumsDiscovery.Infrastructure.csproj` — add `<PackageReference Include="Microsoft.Extensions.Http" />`.

**File:** `src/NewAlbumsDiscovery.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- After the existing `lovedTracksDbPath`/`appDbPath` checks, add:
  ```csharp
  var geminiApiKey = configuration["NewAlbumsDiscovery:GeminiApiKey"];
  if (string.IsNullOrWhiteSpace(geminiApiKey))
  {
      throw new InvalidOperationException(
          "NewAlbumsDiscovery__GeminiApiKey is not set. It is required to call the Gemini API.");
  }
  ```
- Add: `services.AddHttpClient<IGeminiClient, GeminiHttpClient>();`

**File:** `src/NewAlbumsDiscovery.Infrastructure/Notifications/ConsoleDiscoveryNotifier.cs`
- Add:
  ```csharp
  public Task NotifyBucketAbandonedAsync(string bucketName, int trackCount, string reason, CancellationToken cancellationToken)
  {
      Console.WriteLine($"AIDiscovery: bucket '{bucketName}' ({trackCount} track(s)) ABANDONED — {reason}");
      return Task.CompletedTask;
  }
  ```
- Change:
  ```csharp
  public Task NotifyPipelineCompletedAsync(int processedBucketCount, int abandonedBucketCount, CancellationToken cancellationToken)
  {
      Console.WriteLine($"Pipeline Complete. Total Buckets Processed: {processedBucketCount} | Total Buckets Abandoned: {abandonedBucketCount}");
      return Task.CompletedTask;
  }
  ```
  (Message format matches `phase9-requirements.md` §5's example exactly, replacing the old `"AIDiscovery: completed. N bucket(s) processed."` text.)

**Test file:** `tests/NewAlbumsDiscovery.Infrastructure.Tests/Gemini/GeminiHttpClientTests.cs` (new) — a fake `HttpMessageHandler` subclass returning canned `HttpResponseMessage`s (no real network), covering:
- 200 OK with a well-formed `candidates[0].content.parts[0].text` body → `IsSuccess == true`, `ResponseText` extracted correctly.
- 400 / 401 / 403 → `IsSuccess == false`, `IsTransientFailure == false`.
- 429 / 500 → `IsSuccess == false`, `IsTransientFailure == true`.
- Handler throws `HttpRequestException` → `IsTransientFailure == true`.
- 200 OK with a malformed/missing-`candidates` body → `IsSuccess == false`, `IsTransientFailure == false`.
- Request assertions: `x-goog-api-key` header present with the configured `ApiKey`; URL contains the configured `Model`.

Red → green.

---

## Step 8 ✅ — Worker: `appsettings.json`

**File:** `src/NewAlbumsDiscovery.Worker/appsettings.json`
```json
"Gemini": { "Model": "gemini-3.5-flash", "RetryBackoffSeconds": [10, 30, 180] }
```

No test changes — no dedicated appsettings-binding test exists for this section today (same precedent as Phase 8's `InstrumentalLanguage`).

---

## Step 9 ✅ — Full-suite verification

1. `dotnet build` — expect clean, zero warnings/errors.
2. `dotnet test` — full solution green; report before/after test counts per project (Domain unchanged, Application and Infrastructure both grow).
3. Cobertura coverage: confirm `branch-rate="1"` for every touched Application class (`GenreExpansionPromptStep`, `BucketProcessingStage`, `DiscoveryQueryPromptStep`, `ReportPublicationStage`, `PrintBucketStep`) including compiler-generated async state machines, and for `GeminiHttpClient` (Infrastructure.Tests).
4. `FUNCTIONAL_REQUIREMENTS.md` → Phase 9: update Acceptance Criteria with **Verified** annotations citing exact test names; add an As-Built Corrections section (even if empty, per Phase 8's precedent of explicitly stating "no deviations" when true).
5. Per Decision 4, **no live Gemini API call** is made as part of this phase's verification — confirm this is explicitly stated in the Acceptance Criteria writeup so it isn't mistaken for an oversight.

---

## Explicit non-goals
- No real Gemini API call for Prompt 2 (Discovery Query) — stays console-only.
- No live/manual Gemini API verification as part of "done" for this phase (Decision 4) — mocked tests only.
- No changes to `BucketAggregatorEngine`, `LovedTrackRepository`, or Phase 8's instrumental-bucket routing.
- No Domain layer changes.
- No Polly or other resilience-library dependency — retry ladder is hand-rolled against `TimeProvider`, consistent with prior art in this repo.
