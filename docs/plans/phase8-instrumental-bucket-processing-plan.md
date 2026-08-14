# Phase 8 Implementation Plan: Instrumental Bucket Processing

**Status:** ✅ COMPLETE (2026-08-13) — all steps below implemented and automated-tested; the live Worker run (Step 6 item 5) was performed against the real `loved-tracks.db` and found zero instrumental buckets in the current data snapshot (see FUNCTIONAL_REQUIREMENTS.md → Phase 8 → Acceptance Criteria for the full result).

**Requirements:** [`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`](../requirements/FUNCTIONAL_REQUIREMENTS.md) → Phase 8.

Strict TDD (Red-Green-Refactor, 100% branch coverage on Domain + Application) per `docs/constitution/tdd.md`; Domain zero-dependency purity per `docs/constitution/DDD-architecture.md`.

---

## Step 1 ✅ — Domain: `AggregatedBucket.IsInstrumental` becomes a method

**File:** `src/NewAlbumsDiscovery.Domain/MusicAggregator/AggregatedBucket.cs`

- Replace:
  ```csharp
  public bool IsInstrumental => string.Equals(Language, "Instrumental", StringComparison.Ordinal);
  ```
  with:
  ```csharp
  public bool IsInstrumental(string instrumentalLanguage)
  {
      ArgumentNullException.ThrowIfNull(instrumentalLanguage);
      return string.Equals(Language, instrumentalLanguage, StringComparison.Ordinal);
  }
  ```

**Test file:** `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/AggregatedBucketTests.cs`
- Update the 4 existing `IsInstrumental_*` tests to call `bucket.IsInstrumental("Instrumental")` instead of the property.
- Add `IsInstrumental_NullArgument_Throws` (`Assert.Throws<ArgumentNullException>`).
- Add `IsInstrumental_WithNonDefaultConfiguredValue_MatchesConfiguredStringInstead` — a bucket with `Language: "NoVocals"` returns `true` for `IsInstrumental("NoVocals")` and `false` for `IsInstrumental("Instrumental")`, proving the check is genuinely parameterized and not still secretly hardcoded.

Red → write tests first against the still-property-based code (compile failure is the expected "red"), then apply the signature change, then green.

---

## Step 2 ✅ — Application: `AIDiscoveryOptions.InstrumentalLanguage`

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/AIDiscoveryOptions.cs`
- Add:
  ```csharp
  public string InstrumentalLanguage { get; set; } = "Instrumental";
  ```

No dedicated test file exists for this options POCO today (mirrors `MaxAlbumsPerQuery`, which also has none) — its default and binding are exercised indirectly through Step 3/4's step tests and the `appsettings.json` value.

---

## Step 3 ✅ — Application: `GenreExpansionPromptStep` picks up the new dependency

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/GenreExpansionPromptStep.cs`
- Add `IOptions<AIDiscoveryOptions> options` constructor parameter, store as `_options`.
- Change the guard:
  ```csharp
  if (bucket.Genre is null || bucket.IsInstrumental(_options.Value.InstrumentalLanguage))
  {
      return;
  }
  ```

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/GenreExpansionPromptStepTests.cs`
- Update every `new GenreExpansionPromptStep(...)` call site to pass an `IOptions<AIDiscoveryOptions>` (default `InstrumentalLanguage = "Instrumental"`), mirroring `DiscoveryQueryPromptStepTests`' existing `CreateStep` helper pattern — introduce an equivalent local helper here for consistency.
- No new behavioral test needed beyond the signature update: Decision 3 states this step's behavior doesn't change, and the existing `ProcessAsync_WithGenreButInstrumental_NeverRequestsTemplateOrNotifies` test (updated to the new constructor) still proves the guard works.

Red (constructor signature mismatch fails compilation) → green.

---

## Step 4 ✅ — Application: `DiscoveryQueryPromptStep` instrumental routing

**File:** `src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/DiscoveryQueryPromptStep.cs`

Add a second template dictionary:
```csharp
private static readonly IReadOnlyDictionary<BucketType, string> InstrumentalTemplatesByBucketType = new Dictionary<BucketType, string>
{
    [BucketType.CountryLanguage] = "country-instrumental-prompt.md",
    [BucketType.CountryLanguageGenre] = "country-instrumental-genres-prompt.md",
};
```

Replace `ProcessAsync` body's early-return + lookup with:
```csharp
public async Task ProcessAsync(AggregatedBucket bucket, CancellationToken cancellationToken)
{
    var isInstrumental = bucket.IsInstrumental(_options.Value.InstrumentalLanguage);

    var templateFileName = isInstrumental
        ? InstrumentalTemplatesByBucketType[bucket.BucketType]
        : TemplatesByBucketType[bucket.BucketType];
    var template = await _templates.GetTemplateAsync(templateFileName, cancellationToken);

    var values = new Dictionary<string, string>
    {
        ["country"] = bucket.Country,
        ["timeframe"] = _timeframeFormatter.Format(_timeProvider.GetUtcNow()),
        ["maxAlbums"] = _options.Value.MaxAlbumsPerQuery.ToString(CultureInfo.InvariantCulture),
    };

    if (!isInstrumental && bucket.Language is not null)
    {
        values["language"] = bucket.Language;
    }

    if (bucket.Genre is not null)
    {
        values["genres"] = isInstrumental ? bucket.Genre : "TBD";
    }

    var rendered = _renderer.Render(template, values);
    await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);
}
```

**Test file:** `tests/NewAlbumsDiscovery.Application.Tests/AIDiscovery/Pipeline/DiscoveryQueryPromptStepTests.cs`
- **Remove** `ProcessAsync_WithInstrumentalBucket_NeverRequestsTemplateOrNotifies` — its premise (instrumental buckets never notify) is no longer true.
- **Add** `ProcessAsync_WithInstrumentalCountryLanguageBucket_UsesCountryInstrumentalPromptAndOmitsGenres`: `BucketType.CountryLanguage`, `Language: "Instrumental"`, `Genre: null` → template `country-instrumental-prompt.md`; assert `{{country}}`/`{{timeframe}}`/`{{maxAlbums}}` substituted and a `{{genres}}` token left untouched in the mock template text (proves no stray substitution).
- **Add** `ProcessAsync_WithInstrumentalCountryLanguageGenreBucket_UsesCountryInstrumentalGenresPromptAndSubstitutesGenreDirectly`: `BucketType.CountryLanguageGenre`, `Language: "Instrumental"`, `Genre: "Ambient"` → template `country-instrumental-genres-prompt.md`; assert `{{genres}}` renders as `"Ambient"` (not `"TBD"`).
- **Add** `ProcessAsync_WithNonDefaultConfiguredInstrumentalLanguage_RoutesByConfiguredValue`: bucket `Language: "NoVocals"`, `CreateStep` helper extended to accept an `instrumentalLanguage` override set to `"NoVocals"` → instrumental routing fires even though the bucket's language isn't the literal `"Instrumental"`, proving Decision 2's configurability end-to-end through the step (not just the Domain unit).
- Existing `ProcessAsync_WithCountryBucket_...`, `...WithCountryLanguageBucket_...`, `...WithCountryLanguageGenreBucket_...`, `...UsesMaxAlbumsPerQueryFromOptions` tests: unchanged in intent, but confirm they still pass unmodified against the new method body (default `InstrumentalLanguage = "Instrumental"` in `CreateStep`, matching current behavior).

Red → green, then confirm cobertura shows `branch-rate="1"` for the new `isInstrumental` / `!isInstrumental && bucket.Language is not null` branches.

---

## Step 5 ✅ — Infrastructure: asset relocation and cleanup

- Copy `docs/specs/assets/prompts/country-instrumental-prompt.md` and `docs/specs/assets/prompts/country-instrumental-genres-prompt.md` to `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/` (byte-identical; existing `<EmbeddedResource Include="Gemini\Prompts\*.md" />` wildcard picks them up, no `.csproj` change).
- Delete `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/country-genres-prompt.md`.
- `appsettings.json` (`src/NewAlbumsDiscovery.Worker/appsettings.json`): add `"InstrumentalLanguage": "Instrumental"` under `NewAlbumsDiscovery:AIDiscovery`.

**Test file:** `tests/NewAlbumsDiscovery.Infrastructure.Tests/Gemini/EmbeddedPromptTemplateProviderTests.cs`
- Add to the `[Theory]`:
  ```csharp
  [InlineData("country-instrumental-prompt.md", "{{country}}")]
  [InlineData("country-instrumental-genres-prompt.md", "{{genres}}")]
  ```
- Add `GetTemplateAsync_DeletedCountryGenresTemplate_Throws` — requesting `"country-genres-prompt.md"` throws `InvalidOperationException`, proving the deletion actually took effect (not just claimed in docs).

**Test file:** `tests/NewAlbumsDiscovery.Infrastructure.Tests/Gemini/PromptAssetsTests.cs`
- Remove the `[InlineData("NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.country-genres-prompt.md")]` row (would otherwise fail with the file gone). Leave the `genre-expansion-prompt.md` row as-is.

Red (new InlineData rows reference not-yet-copied files; old row now references a file about to be deleted) → copy/delete → green.

---

## Step 6 ✅ — Full-suite verification

1. **Done.** `dotnet build` — clean, zero warnings/errors.
2. **Done.** `dotnet test` — full solution green: 165/165 (96 Domain, up from 94; 38 Application, up from 36; 31 Infrastructure, up from 29), up from the Phase 7 baseline of 159/159.
3. **Done.** Cobertura coverage confirms `branch-rate="1"` for `AggregatedBucket` (Domain.Tests) and `GenreExpansionPromptStep`/`DiscoveryQueryPromptStep` — both classes and their compiler-generated async state machines (Application.Tests).
4. **Done.** `FUNCTIONAL_REQUIREMENTS.md` → Phase 8 Acceptance Criteria updated with **Verified** annotations citing exact test names; As-Built Corrections section added, noting no deviations occurred.
5. **Done.** Live Worker run against the real `loved-tracks.db`. First pass (240s bounded, default `InterBucketDelaySeconds`) only reached 21 of 126 buckets and incorrectly concluded no instrumental buckets existed — a verification-process mistake, corrected after the user flagged it (see `FUNCTIONAL_REQUIREMENTS.md` → Phase 8 → As-Built Corrections). Corrected by (a) querying the persisted `AggregatedBuckets` table directly, confirming 19 real `Instrumental`-language buckets, and (b) re-running with `InterBucketDelaySeconds` temporarily overridden to `0` so all 126 buckets completed: zero exceptions, both instrumental prompt shapes (`country-instrumental-prompt.md` for Level 2, `country-instrumental-genres-prompt.md` with real `{{genres}}` for Level 3) confirmed rendering correctly, no stray `PROMPT 1` line before either.

---

## Explicit non-goals
- No real Gemini API calls.
- No chaining of Genre Expansion output into `{{genres}}` for non-instrumental buckets — still `"TBD"`.
- No changes to `BucketAggregatorEngine` or `LovedTrackRepository` to manufacture instrumental buckets from the current data.
- No backward-compatible overload kept for the old parameterless `AggregatedBucket.IsInstrumental` property — this is a breaking, same-phase signature change with every call site updated.
