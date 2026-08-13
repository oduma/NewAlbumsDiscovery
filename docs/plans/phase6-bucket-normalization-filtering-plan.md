# Phase 6 Implementation Plan — Bucket Aggregator Engine Refinements

**Status:** ✅ COMPLETE (2026-08-13) — all steps below implemented and automated-tested; Step 7's live Worker run was not performed (see that step and FUNCTIONAL_REQUIREMENTS.md → Phase 6 → Acceptance Criteria for the explicit caveat).

**Source:** `docs/requirements/phase6-requirements.md`, finalized in `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 6.

**Constitution:** Strict TDD (Red-Green-Refactor, 100% branch coverage on Domain + Application — see `docs/constitution/tdd.md`); Domain stays zero-dependency/pure (`docs/constitution/DDD-architecture.md` §1); no complex mapping profiles, explicit manual factories only (`docs/constitution/coding-principles.md` §3).

---

## Step 0 — Asset relocation ✅

- Copy `docs/specs/assets/country-synonyms.json` → `src/NewAlbumsDiscovery.Infrastructure/MusicAggregator/Assets/country-synonyms.json` (new folder). Content unchanged — flat `string → string` variant-to-canonical map.
- Add to `NewAlbumsDiscovery.Infrastructure.csproj`:
  ```xml
  <EmbeddedResource Include="MusicAggregator\Assets\*.json" />
  ```
- No test for this step alone — covered indirectly by Step 3's provider tests, which fail loudly (`InvalidOperationException`) if the resource isn't found.

## Step 1 — Domain: master-data value types ✅

- Test-first, `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/CountryMasterDataTests.cs`:
  - `IsKnownCountry` true for an exact-case key, true for a different-case variant, false for an unknown name.
  - `IsContinentName` true for `"Europe"`, false for `"France"`, false for `"Australia"` (the load-bearing edge case — `"Australia"` is a country whose `Continent` value is `"Oceania"`, so it must never satisfy `IsContinentName`).
  - `TryGetContinent` returns `true` + correct continent for a known country (any case), `false` for an unknown name.
  - Constructor throws on `null` dictionary (`ArgumentNullException`).
- Implement `src/NewAlbumsDiscovery.Domain/MusicAggregator/CountryMasterDataEntry.cs`:
  ```csharp
  public sealed record CountryMasterDataEntry(string Continent);
  ```
- Implement `src/NewAlbumsDiscovery.Domain/MusicAggregator/CountryMasterData.cs`:
  ```csharp
  public sealed class CountryMasterData
  {
      private readonly IReadOnlyDictionary<string, CountryMasterDataEntry> _countries;
      private readonly IReadOnlySet<string> _continents;

      public CountryMasterData(IReadOnlyDictionary<string, CountryMasterDataEntry> countries)
      {
          ArgumentNullException.ThrowIfNull(countries);
          _countries = new Dictionary<string, CountryMasterDataEntry>(countries, StringComparer.OrdinalIgnoreCase);
          _continents = _countries.Values.Select(e => e.Continent).ToHashSet(StringComparer.OrdinalIgnoreCase);
      }

      public bool IsKnownCountry(string name) => _countries.ContainsKey(name);
      public bool IsContinentName(string name) => _continents.Contains(name);

      public bool TryGetContinent(string country, out string? continent)
      {
          if (_countries.TryGetValue(country, out var entry))
          {
              continent = entry.Continent;
              return true;
          }
          continent = null;
          return false;
      }
  }
  ```

## Step 2 — Domain: `CountryNormalizer` ✅

- Test-first, `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/CountryNormalizerTests.cs`:
  - A track whose `Country` exactly matches a synonym key is rebuilt with the canonical value (`"United States"` → `"USA"`), `Languages`/`Genres` preserved unchanged.
  - A track whose `Country` matches a synonym key case-insensitively is still normalized.
  - A track whose `Country` has no synonym entry passes through unchanged (same instance or value-equal — assert on the resulting `Country`/`Languages`/`Genres`, per `LovedTrackPreferences`'s existing value-equality).
  - Empty synonym dictionary → all tracks pass through unchanged.
  - Empty track list → empty result, no exception.
  - `null` tracks or `null` synonyms → `ArgumentNullException`.
- Implement `src/NewAlbumsDiscovery.Domain/MusicAggregator/CountryNormalizer.cs`:
  ```csharp
  public sealed class CountryNormalizer
  {
      public IReadOnlyList<LovedTrackPreferences> Normalize(
          IReadOnlyList<LovedTrackPreferences> tracks,
          IReadOnlyDictionary<string, string> synonyms)
      {
          ArgumentNullException.ThrowIfNull(tracks);
          ArgumentNullException.ThrowIfNull(synonyms);

          var caseInsensitiveSynonyms = new Dictionary<string, string>(synonyms, StringComparer.OrdinalIgnoreCase);

          return tracks
              .Select(track => caseInsensitiveSynonyms.TryGetValue(track.Country, out var canonical)
                  ? new LovedTrackPreferences(canonical, track.Languages, track.Genres)
                  : track)
              .ToList();
      }
  }
  ```

## Step 3 — Domain: `IBucketFilterRule` pipeline ✅

- Test-first, `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/Filtering/InvalidCountryDeletionRuleTests.cs`:
  - A bucket with a known country `Country` is retained.
  - A bucket with a known continent `Country` is retained.
  - A bucket with an unrecognized `Country` (e.g. `"Unknown"`, `"Global/Various"`, an arbitrary string) is dropped.
  - Case-insensitive match retains a differently-cased known country.
  - Empty bucket list → empty result.
- Test-first, `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/Filtering/ContinentFallbackEliminationRuleTests.cs`:
  - A continent-named bucket (`"Europe"`) is dropped when a `Country`-type bucket for a specific country in that continent (`"France"`) also exists.
  - A continent-named bucket is dropped when the only other evidence is a `CountryLanguage` or `CountryLanguageGenre` bucket for a country in that continent (not just a `Country`-type one) — locks in Decision 3.
  - A continent-named bucket is **retained** when no other bucket resolves to a country in that continent.
  - A bucket named `"Australia"` is retained regardless of what other buckets exist — explicit regression test for the structural edge case (Design Notes).
  - Two different continent buckets in the same list are evaluated independently (one dropped, one retained).
  - Empty bucket list → empty result.
- Implement `src/NewAlbumsDiscovery.Domain/MusicAggregator/Filtering/IBucketFilterRule.cs`:
  ```csharp
  public interface IBucketFilterRule
  {
      IReadOnlyList<AggregatedBucket> Apply(IReadOnlyList<AggregatedBucket> buckets, CountryMasterData masterData);
  }
  ```
- Implement `src/NewAlbumsDiscovery.Domain/MusicAggregator/Filtering/InvalidCountryDeletionRule.cs`:
  ```csharp
  public sealed class InvalidCountryDeletionRule : IBucketFilterRule
  {
      public IReadOnlyList<AggregatedBucket> Apply(IReadOnlyList<AggregatedBucket> buckets, CountryMasterData masterData)
          => buckets.Where(b => masterData.IsKnownCountry(b.Country) || masterData.IsContinentName(b.Country)).ToList();
  }
  ```
- Implement `src/NewAlbumsDiscovery.Domain/MusicAggregator/Filtering/ContinentFallbackEliminationRule.cs`:
  ```csharp
  public sealed class ContinentFallbackEliminationRule : IBucketFilterRule
  {
      public IReadOnlyList<AggregatedBucket> Apply(IReadOnlyList<AggregatedBucket> buckets, CountryMasterData masterData)
      {
          bool HasSpecificCountryEvidence(string continent) => buckets.Any(b =>
              !masterData.IsContinentName(b.Country)
              && masterData.TryGetContinent(b.Country, out var bucketContinent)
              && string.Equals(bucketContinent, continent, StringComparison.OrdinalIgnoreCase));

          return buckets
              .Where(b => !masterData.IsContinentName(b.Country) || !HasSpecificCountryEvidence(b.Country))
              .ToList();
      }
  }
  ```

## Step 4 — Application: `ICountryMasterDataProvider` port ✅

- Implement `src/NewAlbumsDiscovery.Application/MusicAggregator/ICountryMasterDataProvider.cs`:
  ```csharp
  public interface ICountryMasterDataProvider
  {
      Task<IReadOnlyDictionary<string, string>> GetCountrySynonymsAsync(CancellationToken cancellationToken);
      Task<CountryMasterData> GetCountryMasterDataAsync(CancellationToken cancellationToken);
  }
  ```
  (Pure port definition — no test needed for an interface itself; exercised via the handler tests in Step 5 and the Infrastructure tests in Step 6.)

## Step 5 — Application: wire `AggregateMusicPreferencesCommandHandler` ✅

- Test-first, extend `tests/NewAlbumsDiscovery.Application.Tests/MusicAggregator/AggregateMusicPreferencesCommandHandlerTests.cs` (new tests alongside the existing 6):
  - Tracks are normalized (via a mocked `CountryNormalizer`... **note:** `CountryNormalizer` is a concrete Domain class like `BucketAggregatorEngine`, so tests should use a real instance with a controlled synonym dictionary returned by a mocked `ICountryMasterDataProvider`, not a mock of the normalizer itself — matches how `BucketAggregatorEngineTests` already tests the real engine, and how the handler already uses a real `BucketAggregatorEngine` today) before being passed to `BucketAggregatorEngine.Aggregate`.
  - Buckets are folded through the filter rules in the exact order the fake `IEnumerable<IBucketFilterRule>` provides them (assert order-dependent behavior with two rules that each drop different buckets, verifying both applied and in sequence).
  - `ReplaceAllAsync` receives the *filtered* bucket list, not the engine's raw output.
  - Cancellation propagates to `GetCountrySynonymsAsync`/`GetCountryMasterDataAsync` calls the same way it already does for the repository calls.
  - Empty tracks / empty synonyms / empty filter-rule list still completes without error (buckets pass through the engine and an empty `foreach` unchanged).
- Modify `src/NewAlbumsDiscovery.Application/MusicAggregator/AggregateMusicPreferencesCommand.cs`:
  ```csharp
  public sealed class AggregateMusicPreferencesCommandHandler : IRequestHandler<AggregateMusicPreferencesCommand>
  {
      private readonly ILovedTrackRepository _lovedTrackRepository;
      private readonly IAggregatedBucketRepository _aggregatedBucketRepository;
      private readonly BucketAggregatorEngine _engine;
      private readonly IOptions<AggregatorSettings> _settings;
      private readonly TimeProvider _timeProvider;
      private readonly ICountryMasterDataProvider _countryMasterDataProvider;
      private readonly CountryNormalizer _normalizer;
      private readonly IEnumerable<IBucketFilterRule> _filterRules;

      public async Task Handle(AggregateMusicPreferencesCommand request, CancellationToken cancellationToken)
      {
          var tracks = await _lovedTrackRepository.GetAllAsync(cancellationToken);

          var synonyms = await _countryMasterDataProvider.GetCountrySynonymsAsync(cancellationToken);
          var normalizedTracks = _normalizer.Normalize(tracks, synonyms);

          var thresholds = new AggregationThresholds(
              _settings.Value.CountryRegionThreshold,
              _settings.Value.CountryRegionLanguageThreshold,
              _settings.Value.MinimumBucketThreshold);

          var asOfUtc = _timeProvider.GetUtcNow().UtcDateTime;

          var buckets = _engine.Aggregate(normalizedTracks, thresholds, asOfUtc);

          var masterData = await _countryMasterDataProvider.GetCountryMasterDataAsync(cancellationToken);
          foreach (var rule in _filterRules)
          {
              buckets = rule.Apply(buckets, masterData);
          }

          await _aggregatedBucketRepository.ReplaceAllAsync(buckets, cancellationToken);
      }
  }
  ```
- Update `src/NewAlbumsDiscovery.Application/Common/ApplicationServiceCollectionExtensions.cs` — add, near the existing `BucketAggregatorEngine` registration:
  ```csharp
  services.AddSingleton<CountryNormalizer>();

  // Registration order is load-bearing: IEnumerable<IBucketFilterRule> resolves in this exact
  // order. InvalidCountryDeletionRule must run before ContinentFallbackEliminationRule so
  // continent-fallback evidence is only evaluated against already-validated country buckets.
  services.AddScoped<IBucketFilterRule, InvalidCountryDeletionRule>();
  services.AddScoped<IBucketFilterRule, ContinentFallbackEliminationRule>();
  ```
  (`ICountryMasterDataProvider` is registered in Infrastructure, Step 6 — Application only declares the port.)

## Step 6 — Infrastructure: `EmbeddedCountryMasterDataProvider` ✅

- Test-first, `tests/NewAlbumsDiscovery.Infrastructure.Tests/MusicAggregator/EmbeddedCountryMasterDataProviderTests.cs` (exercises the **real** embedded JSON, no mocking — mirrors `PromptAssetsTests`' style):
  - `GetCountryMasterDataAsync` returns a `CountryMasterData` where `IsKnownCountry("USA")` is `true` and `TryGetContinent("USA", out var continent)` yields `"North America"`.
  - `IsContinentName("Europe")` is `true`; `IsContinentName("Australia")` is `false`.
  - `GetCountrySynonymsAsync` returns a dictionary containing `"United States" → "USA"` and `"Great Britain" → "UK"`.
  - Both methods are callable multiple times without throwing (sanity check on internal caching, if any is added — caching itself is an implementation detail, not separately unit-tested).
- Implement `src/NewAlbumsDiscovery.Infrastructure/MusicAggregator/EmbeddedCountryMasterDataProvider.cs`:
  ```csharp
  public sealed class EmbeddedCountryMasterDataProvider : ICountryMasterDataProvider
  {
      private const string MasterDataResourceName = "NewAlbumsDiscovery.Infrastructure.Gemini.Assets.countries-languages.json";
      private const string SynonymsResourceName = "NewAlbumsDiscovery.Infrastructure.MusicAggregator.Assets.country-synonyms.json";

      private CountryMasterData? _masterData;
      private IReadOnlyDictionary<string, string>? _synonyms;

      public Task<CountryMasterData> GetCountryMasterDataAsync(CancellationToken cancellationToken)
      {
          _masterData ??= BuildMasterData();
          return Task.FromResult(_masterData);
      }

      public Task<IReadOnlyDictionary<string, string>> GetCountrySynonymsAsync(CancellationToken cancellationToken)
      {
          _synonyms ??= ParseJson<Dictionary<string, string>>(SynonymsResourceName);
          return Task.FromResult(_synonyms);
      }

      private static CountryMasterData BuildMasterData()
      {
          var raw = ParseJson<Dictionary<string, RawCountryEntry>>(MasterDataResourceName);
          var entries = raw.ToDictionary(kvp => kvp.Key, kvp => new CountryMasterDataEntry(kvp.Value.Continent));
          return new CountryMasterData(entries);
      }

      private static T ParseJson<T>(string resourceName)
      {
          var assembly = typeof(EmbeddedCountryMasterDataProvider).Assembly;
          using var stream = assembly.GetManifestResourceStream(resourceName)
              ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
          return JsonSerializer.Deserialize<T>(stream)
              ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' deserialized to null.");
      }

      private sealed record RawCountryEntry(string Language, string Continent);
  }
  ```
  (Instance-field caching, not `static`, is deliberate: the type is registered `AddSingleton`, so one instance already lives for the process lifetime — no need for `static`/`Lazy<T>` ceremony on top of that.)
- Update `src/NewAlbumsDiscovery.Infrastructure/InfrastructureServiceCollectionExtensions.cs` — add:
  ```csharp
  services.AddSingleton<ICountryMasterDataProvider, EmbeddedCountryMasterDataProvider>();
  ```

## Step 7 — Full-suite verification (partially ✅ — see item 5)

1. `dotnet build` from the repo root — must succeed with no warnings-as-errors. **Done.** 0 warnings, 0 errors.
2. `dotnet test` from the repo root — all tests pass, including the new Domain/Application/Infrastructure tests from Steps 1–6. **Done.** 128/128 passing (80 Domain, 26 Application, 22 Infrastructure) — up from the Phase 5 baseline of 88/88 (48 Domain, 23 Application, 17 Infrastructure); this phase added 32 Domain, 3 Application, 5 Infrastructure tests.
3. Confirm 100% branch coverage on every new Domain and Application class listed under FUNCTIONAL_REQUIREMENTS.md → Phase 6 → In Scope (`CountryMasterData`, `CountryNormalizer`, `InvalidCountryDeletionRule`, `ContinentFallbackEliminationRule`, the updated `AggregateMusicPreferencesCommandHandler`). **Done.** `branch-rate="1"` confirmed via `coverlet`/cobertura on every listed class.
4. Update `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 6 → Acceptance Criteria with explicit **Verified** annotations citing the exact test classes (same style as Phases 4/5), and mark this plan's status line ✅ COMPLETE. **Done**, via `/complete`.
5. A live end-to-end Worker run against the real `loved-tracks.db`, confirming real-world country data normalizes and filters as expected. **Not done.** Same category of gap left open by Phase 5 — no confirmation has been reported back of an actual run since Phase 5's equivalent gap was first flagged.

---

### Explicit non-goals
- No change to `BucketAggregatorEngine`, `AggregationThresholds`, `AggregatedBucket`, or `BucketType` — this phase only wraps new pre/post steps around the existing pure engine call.
- No new `AggregatorSettings` fields or `appsettings.json` sections — both new assets are hardcoded embedded-resource names.
- No live end-to-end Worker run against real `loved-tracks.db` data as part of this plan's own verification — same category of gap Phase 5 left open, not resolved here.
- No UI/notification of dropped buckets — deletions are silent, per the source requirement.
- No duplicate copy of `countries-languages.json` into `MusicAggregator/Assets/` — the existing `Gemini/Assets/` copy is reused directly.
