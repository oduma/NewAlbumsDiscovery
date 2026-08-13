# Phase 7 Implementation Plan — AI Discovery Prompt Preparation

**Status:** ✅ COMPLETE (2026-08-13) — all steps below implemented and automated-tested; Step 10's live Worker run was not performed (see that step and FUNCTIONAL_REQUIREMENTS.md → Phase 7 → Acceptance Criteria for the explicit caveat).

**Requirements:** [`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`](../requirements/FUNCTIONAL_REQUIREMENTS.md) → Phase 7. Source spec: [`docs/requirements/phase7-requirements.md`](../requirements/phase7-requirements.md) (Antigravity-owned).

Every step follows strict Red-Green-Refactor per `docs/constitution/tdd.md`: write the failing test first, implement the minimum to pass, then refactor. Domain and Application code must reach 100% branch coverage.

---

## Step 0 ✅ — Asset relocation

Copy three prompt templates (byte-identical, no edits) from the Antigravity-owned canonical source into the Infrastructure project, where the existing `<EmbeddedResource Include="Gemini\Prompts\*.md" />` wildcard in `NewAlbumsDiscovery.Infrastructure.csproj` will embed them automatically:

- `docs/specs/assets/prompts/country-prompt.md` → `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/country-prompt.md`
- `docs/specs/assets/prompts/country-language-prompt.md` → `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/country-language-prompt.md`
- `docs/specs/assets/prompts/country-language-genres-prompt.md` → `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/country-language-genres-prompt.md`

No `.csproj` change needed. `genre-expansion-prompt.md` and `country-genres-prompt.md` are already present from Phase 5 (`country-genres-prompt.md` stays unconsumed this phase, per Decision 3).

---

## Step 1 ✅ — Domain: `AggregatedBucket.IsInstrumental`

**Test first** — `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/AggregatedBucketTests.cs` (extend existing file if present, else create):
- `IsInstrumental` is `true` when `Language == "Instrumental"` (exact case).
- `IsInstrumental` is `false` when `Language` is `"instrumental"` (different case — ordinal comparison, no case-folding).
- `IsInstrumental` is `false` when `Language` is `null`.
- `IsInstrumental` is `false` when `Language` is any other non-null value (e.g. `"English"`).

**Implementation** — `src/NewAlbumsDiscovery.Domain/MusicAggregator/AggregatedBucket.cs`:
```csharp
public bool IsInstrumental => string.Equals(Language, "Instrumental", StringComparison.Ordinal);
```

---

## Step 2 ✅ — Domain: `PromptRenderer`

**Test first** — `tests/NewAlbumsDiscovery.Domain.Tests/AIDiscovery/PromptRendererTests.cs`:
- Null `template` throws `ArgumentNullException`.
- Null `values` throws `ArgumentNullException`.
- Single `{{token}}` is replaced with its dictionary value.
- Multiple distinct tokens are all replaced.
- A token repeated more than once in the template is replaced at every occurrence.
- A dictionary key with no matching `{{token}}` in the template is silently unused (no error, no effect).
- A `{{token}}` in the template with no matching dictionary key is left untouched, literally, in the output.
- An empty `values` dictionary returns the template unchanged.

**Implementation** — `src/NewAlbumsDiscovery.Domain/AIDiscovery/PromptRenderer.cs`:
```csharp
namespace NewAlbumsDiscovery.Domain.AIDiscovery;

public sealed class PromptRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        var rendered = template;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace($"{{{{{key}}}}}", value);
        }

        return rendered;
    }
}
```

---

## Step 3 ✅ — Domain: `TimeframeFormatter`

**Test first** — `tests/NewAlbumsDiscovery.Domain.Tests/AIDiscovery/TimeframeFormatterTests.cs`:
- Given a fixed `DateTimeOffset` (e.g. `2026-08-13T00:00:00Z`), `Format` returns `"between 14-JUL-2026 and 13-AUG-2026"` (30 days back, upper-cased month abbreviation).
- Given a `DateTimeOffset` with a non-zero UTC offset, the date range is computed from the UTC instant, not the local wall-clock date.

**Implementation** — `src/NewAlbumsDiscovery.Domain/AIDiscovery/TimeframeFormatter.cs`:
```csharp
namespace NewAlbumsDiscovery.Domain.AIDiscovery;

public sealed class TimeframeFormatter
{
    private const int LookbackDays = 30;

    public string Format(DateTimeOffset asOf)
    {
        var end = asOf.UtcDateTime.Date;
        var start = end.AddDays(-LookbackDays);
        return $"between {FormatDate(start)} and {FormatDate(end)}";
    }

    private static string FormatDate(DateTime date)
        => date.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
}
```

---

## Step 4 ✅ — Application: new port, options, and notifier method

**4a. `IPromptTemplateProvider` port** — `src/NewAlbumsDiscovery.Application/AIDiscovery/Prompts/IPromptTemplateProvider.cs`:
```csharp
namespace NewAlbumsDiscovery.Application.AIDiscovery.Prompts;

public interface IPromptTemplateProvider
{
    Task<string> GetTemplateAsync(string templateFileName, CancellationToken cancellationToken);
}
```
No test needed for an interface.

**4b. `AIDiscoveryOptions.MaxAlbumsPerQuery`** — `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/AIDiscoveryOptions.cs`, add:
```csharp
public int MaxAlbumsPerQuery { get; set; } = 20;
```

**4c. `IDiscoveryNotifier.NotifyPromptRenderedAsync`** — `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/IDiscoveryNotifier.cs`, add:
```csharp
Task NotifyPromptRenderedAsync(string header, string promptContent, CancellationToken cancellationToken);
```
Existing `Mock<IDiscoveryNotifier>` usages in `PrintBucketStepTests`, `BucketProcessingStageTests`, `StartNotificationStageTests`, `ReportPublicationStageTests` are unaffected (Moq auto-stubs unimplemented members).

**4d. `RecordingTimeProvider` fixed-clock support** — `tests/NewAlbumsDiscovery.Application.Tests/TestSupport/RecordingTimeProvider.cs`:
```csharp
public sealed class RecordingTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public List<TimeSpan> Delays { get; } = [];

    public RecordingTimeProvider(DateTimeOffset? utcNow = null)
    {
        _utcNow = utcNow ?? DateTimeOffset.UtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Delays.Add(dueTime);
        callback(state);
        return new NoopTimer();
    }

    private sealed class NoopTimer : ITimer { /* unchanged */ }
}
```
Additive and backward-compatible — no existing call site breaks.

---

## Step 5 ✅ — Application: `GenreExpansionPromptStep`

**Test first** — `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/GenreExpansionPromptStepTests.cs`:
- Bucket with `Genre == null` (Level 1/2) → `IPromptTemplateProvider` and `IDiscoveryNotifier` are never called.
- Bucket with `Genre` set but `IsInstrumental == true` → never called.
- Bucket with `Genre` set, not instrumental → requests `"genre-expansion-prompt.md"`, renders with `{{genre}}`, `{{country}}`, `{{language}}` substituted from the bucket, and calls `NotifyPromptRenderedAsync("--- PROMPT 1: GENRE EXPANSION ---", <rendered>, ct)` exactly once.
- Cancellation token is passed through to both the template provider and the notifier call.

**Implementation** — `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/GenreExpansionPromptStep.cs`:
```csharp
namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

public sealed class GenreExpansionPromptStep : IBucketProcessingStep
{
    private const string TemplateFileName = "genre-expansion-prompt.md";
    private const string Header = "--- PROMPT 1: GENRE EXPANSION ---";

    private readonly IPromptTemplateProvider _templates;
    private readonly PromptRenderer _renderer;
    private readonly IDiscoveryNotifier _notifier;

    public GenreExpansionPromptStep(IPromptTemplateProvider templates, PromptRenderer renderer, IDiscoveryNotifier notifier)
    {
        _templates = templates;
        _renderer = renderer;
        _notifier = notifier;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, CancellationToken cancellationToken)
    {
        if (bucket.Genre is null || bucket.IsInstrumental)
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

        var rendered = _renderer.Render(template, values);
        await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);
    }
}
```

---

## Step 6 ✅ — Application: `DiscoveryQueryPromptStep`

**Test first** — `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/DiscoveryQueryPromptStepTests.cs`:
- `IsInstrumental == true` → never calls the template provider or notifier.
- `Country`-type bucket → requests `"country-prompt.md"`; substitution dictionary has `country`, `timeframe`, `maxAlbums` only (no `language`, no `genres`).
- `CountryLanguage`-type bucket → requests `"country-language-prompt.md"`; dictionary adds `language`, still no `genres`.
- `CountryLanguageGenre`-type bucket → requests `"country-language-genres-prompt.md"`; dictionary has `country`, `language`, `timeframe`, `maxAlbums`, and `genres` = literal `"TBD"`.
- `{{timeframe}}` comes from `TimeframeFormatter.Format(timeProvider.GetUtcNow())` (verified via a `RecordingTimeProvider` fixed to a known instant).
- `{{maxAlbums}}` comes from `IOptions<AIDiscoveryOptions>.Value.MaxAlbumsPerQuery`.
- Notifies with header `"--- PROMPT 2: DISCOVERY QUERY ---"` exactly once, for each non-instrumental case.

**Implementation** — `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/DiscoveryQueryPromptStep.cs`:
```csharp
namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

public sealed class DiscoveryQueryPromptStep : IBucketProcessingStep
{
    private const string Header = "--- PROMPT 2: DISCOVERY QUERY ---";

    private static readonly IReadOnlyDictionary<BucketType, string> TemplatesByBucketType = new Dictionary<BucketType, string>
    {
        [BucketType.Country] = "country-prompt.md",
        [BucketType.CountryLanguage] = "country-language-prompt.md",
        [BucketType.CountryLanguageGenre] = "country-language-genres-prompt.md",
    };

    private readonly IPromptTemplateProvider _templates;
    private readonly PromptRenderer _renderer;
    private readonly TimeframeFormatter _timeframeFormatter;
    private readonly IDiscoveryNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<AIDiscoveryOptions> _options;

    public DiscoveryQueryPromptStep(
        IPromptTemplateProvider templates,
        PromptRenderer renderer,
        TimeframeFormatter timeframeFormatter,
        IDiscoveryNotifier notifier,
        TimeProvider timeProvider,
        IOptions<AIDiscoveryOptions> options)
    {
        _templates = templates;
        _renderer = renderer;
        _timeframeFormatter = timeframeFormatter;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _options = options;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, CancellationToken cancellationToken)
    {
        if (bucket.IsInstrumental)
        {
            return;
        }

        var templateFileName = TemplatesByBucketType[bucket.BucketType];
        var template = await _templates.GetTemplateAsync(templateFileName, cancellationToken);

        var values = new Dictionary<string, string>
        {
            ["country"] = bucket.Country,
            ["timeframe"] = _timeframeFormatter.Format(_timeProvider.GetUtcNow()),
            ["maxAlbums"] = _options.Value.MaxAlbumsPerQuery.ToString(CultureInfo.InvariantCulture),
        };

        if (bucket.Language is not null)
        {
            values["language"] = bucket.Language;
        }

        if (bucket.Genre is not null)
        {
            values["genres"] = "TBD";
        }

        var rendered = _renderer.Render(template, values);
        await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);
    }
}
```

---

## Step 7 ✅ — Application DI wiring

`src/NewAlbumsDiscovery.Application/Common/ApplicationServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<PromptRenderer>();
services.AddSingleton<TimeframeFormatter>();
...
services.AddScoped<IBucketProcessingStep, PrintBucketStep>();
services.AddScoped<IBucketProcessingStep, GenreExpansionPromptStep>();
services.AddScoped<IBucketProcessingStep, DiscoveryQueryPromptStep>();
```
Registration order is load-bearing (same convention as the existing stage/rule registrations): the standard print always runs first, then genre-expansion, then discovery-query — matching the source spec's example output ordering.

---

## Step 8 ✅ — Infrastructure: `EmbeddedPromptTemplateProvider`

**Test first** — see Step 9 (Infrastructure tests exercise the real embedded resources directly, no mocking, per the `EmbeddedCountryMasterDataProviderTests` precedent).

**Implementation** — `src/NewAlbumsDiscovery.Infrastructure/Gemini/EmbeddedPromptTemplateProvider.cs`:
```csharp
namespace NewAlbumsDiscovery.Infrastructure.Gemini;

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
```

**DI registration** — `src/NewAlbumsDiscovery.Infrastructure/InfrastructureServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<IPromptTemplateProvider, EmbeddedPromptTemplateProvider>();
```
(registered singleton, same rationale as `ICountryMasterDataProvider` — shipped assets never change at runtime.)

**Config** — `src/NewAlbumsDiscovery.Worker/appsettings.json`, `NewAlbumsDiscovery:AIDiscovery` section gains:
```json
"MaxAlbumsPerQuery": 20
```

**`ConsoleDiscoveryNotifier.NotifyPromptRenderedAsync`** — `src/NewAlbumsDiscovery.Infrastructure/Notifications/ConsoleDiscoveryNotifier.cs`:
```csharp
public Task NotifyPromptRenderedAsync(string header, string promptContent, CancellationToken cancellationToken)
{
    Console.WriteLine(header);
    Console.WriteLine(promptContent);
    return Task.CompletedTask;
}
```

---

## Step 9 ✅ — Infrastructure tests

**`tests/NewAlbumsDiscovery.Infrastructure.Tests/Gemini/EmbeddedPromptTemplateProviderTests.cs`** (new, mirrors `EmbeddedCountryMasterDataProviderTests`' no-mocking style against the real embedded assets):
- Each of `country-prompt.md`, `country-language-prompt.md`, `country-language-genres-prompt.md`, `genre-expansion-prompt.md` loads non-empty content containing at least one expected `{{token}}`.
- An unknown template file name throws `InvalidOperationException`.
- Calling `GetTemplateAsync` twice for the same file returns identical content (cache path exercised).

**`tests/NewAlbumsDiscovery.Infrastructure.Tests/Notifications/ConsoleDiscoveryNotifierTests.cs`** (extend): add `NotifyPromptRenderedAsync_WritesHeaderThenContent`, asserting both the header and content strings appear in captured console output, in that order.

---

## Step 10 (partially ✅ — see item 5) — Full-suite verification

1. **Done.** `dotnet build` succeeds solution-wide with 0 warnings/errors.
2. **Done.** `dotnet test` — 159/159 tests passing (94 Domain, 36 Application, 29 Infrastructure), up from the Phase 6 baseline of 128/128.
3. **Done.** `dotnet test --collect:"XPlat Code Coverage"` confirms `branch-rate="1"` in each new class's own test-project cobertura report: `AggregatedBucket`, `PromptRenderer`, `TimeframeFormatter` (Domain.Tests); `GenreExpansionPromptStep`, `DiscoveryQueryPromptStep` — both the class and its compiler-generated async state machine (Application.Tests); `EmbeddedPromptTemplateProvider` (Infrastructure.Tests). One gap was found and fixed along the way: `GenreExpansionPromptStep`'s async state machine initially reported `branch-rate="0.8333"` because no test exercised a bucket with `Genre` set and `Language == null` (the `?? string.Empty` branch) — added `ProcessAsync_WithGenreAndNullLanguage_SubstitutesEmptyStringForLanguage` to close it.
4. **Done.** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 7 updated: marked ✅ COMPLETE, every Acceptance Criteria bullet annotated with **Verified** references, As-Built Corrections section added describing the one coverage-gap fix above.
5. **Not done.** An actual live `dotnet run --project src/NewAlbumsDiscovery.Worker` against a real `loved-tracks.db`, visually confirming the printed prompts look correct for real bucket data. Flagged explicitly, same as Phases 5 and 6, rather than silently skipped.

---

## Explicit non-goals

- No real Gemini API calls.
- No chaining of Prompt 1's response into Prompt 2's `{{genres}}` — always the literal string `"TBD"` this phase.
- No `country-genres-prompt.md` consumption or country-default-language fallback (unreachable bucket shape — see Decision 3).
- No persistence of rendered prompts.
- No CI pipeline / automated coverage-gate enforcement.
