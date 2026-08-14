# Phase 10 Implementation Plan: Gemini API Integration (Discovery Query & Persistence)

**Status:** ✅ COMPLETE (2026-08-14) — all 13 steps implemented and tested. See FUNCTIONAL_REQUIREMENTS.md → Phase 10 → As-Built Corrections for the two coverage gaps found and closed during Step 13 (no design deviations from this plan).

Requirements: [`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`](../requirements/FUNCTIONAL_REQUIREMENTS.md) → Phase 10 (Decisions, Design Notes, In/Out of Scope, Acceptance Criteria).
Raw input: [`docs/requirements/phase10-requirements.md`](../requirements/phase10-requirements.md).

Follows the project's strict TDD mandate (`docs/constitution/tdd.md`): every step below is test-first (Red → Green → Refactor), 100% branch coverage required on every touched Domain/Application class, verified via `dotnet test --collect:"XPlat Code Coverage"` + cobertura `branch-rate="1"` inspection, same as Phase 9.

---

## Step 1 ✅ — Domain: `DiscoveredAlbumStatus`, `AlbumKey`, `AlbumDeduplicator`

Zero-dependency Domain additions in `src/NewAlbumsDiscovery.Domain/AIDiscovery/`.

- [x] `DiscoveredAlbumStatus.cs` — `public enum DiscoveredAlbumStatus { Pending }`.
- [x] **Test-first** `AlbumKeyTests.cs` (`tests/NewAlbumsDiscovery.Domain.Tests/AIDiscovery/`): `From` trims and lowercases both `Artist` and `Album`; two keys built from differently-cased/whitespace-padded input are equal (`readonly record struct` gives structural equality for free — assert it); different artist or album produces an unequal key.
- [x] `AlbumKey.cs` — `readonly record struct` with `NormalizedArtist`/`NormalizedAlbum`, `static From(string artist, string album)` factory, `private static string Normalize(string value) => value.Trim().ToLowerInvariant();` (mirrors the deleted Phase 4 precedent).
- [x] **Test-first** `AlbumDeduplicatorTests.cs`: given a candidate list with an in-batch duplicate (same normalized key twice) → only the first occurrence returned; given an `existingKeys` set pre-seeded with a key → any candidate matching it is rejected; accepted candidates are added to `existingKeys` (mutation-in-place, assert the set grew); empty candidate list → empty result, `existingKeys` untouched.
- [x] `AlbumDeduplicator.cs` — static class, `SelectNew(IReadOnlyList<(string Artist, string Album)> candidates, ISet<AlbumKey> existingKeys)`, mirrors the deleted Phase 4 precedent exactly (loop, `AlbumKey.From`, `existingKeys.Add(key)` used as the "was this new" check since `ISet<T>.Add` returns `false` for an existing member).

## Step 2 ✅ — Domain: `DiscoveredAlbum` entity

- [x] **Test-first** `DiscoveredAlbumTests.cs`: constructor throws `ArgumentException` on null/whitespace `Artist` or `AlbumName` (mirror `AggregatedBucketTests`' pattern for `AggregatedBucket`); `Create(...)` generates a fresh `Guid` and sets `Status = DiscoveredAlbumStatus.Pending`; equality is by `Id` alone (two distinct `Create()` calls with identical field values are unequal; same `Id` via the explicit constructor are equal).
- [x] `DiscoveredAlbum.cs` — sealed class, `IEquatable<DiscoveredAlbum>`, same shape as `AggregatedBucket.cs`:
  ```csharp
  public sealed class DiscoveredAlbum : IEquatable<DiscoveredAlbum>
  {
      public Guid Id { get; }
      public Guid ReferenceBucketId { get; }
      public string Artist { get; }
      public string AlbumName { get; }
      public DiscoveredAlbumStatus Status { get; }
      public DateTime DiscoveredAtUtc { get; }

      public DiscoveredAlbum(Guid id, Guid referenceBucketId, string artist, string albumName, DiscoveredAlbumStatus status, DateTime discoveredAtUtc) { /* validate artist/albumName non-null/whitespace */ }

      public static DiscoveredAlbum Create(Guid referenceBucketId, string artist, string albumName, DateTime discoveredAtUtc)
          => new(Guid.NewGuid(), referenceBucketId, artist, albumName, DiscoveredAlbumStatus.Pending, discoveredAtUtc);

      public bool Equals(DiscoveredAlbum? other) => other is not null && Id == other.Id;
      // Equals(object?), GetHashCode() mirroring AggregatedBucket
  }
  ```

## Step 3 ✅ — Application: `IDiscoveredAlbumRepository` port + `GeminiRetryExecutor`

- [x] `IDiscoveredAlbumRepository.cs` (`src/NewAlbumsDiscovery.Application/AIDiscovery/`):
  ```csharp
  public interface IDiscoveredAlbumRepository
  {
      Task<ISet<AlbumKey>> GetExistingAlbumKeysAsync(CancellationToken cancellationToken);
      Task AddRangeAsync(IReadOnlyList<DiscoveredAlbum> albums, CancellationToken cancellationToken);
  }
  ```
  No test file (pure interface, nothing to unit test — same convention as `IAggregatedBucketRepository`).
- [x] **Test-first** `GeminiRetryExecutorTests.cs`: success on first attempt → no delay, returns the success result; transient failure then success → delays `backoffs[0]` seconds (via `RecordingTimeProvider`) then returns success; all retries exhausted (transient every time) → delays for every configured backoff in order, then returns the final transient `GeminiCallResult` unchanged (caller decides what "exhausted" means); permanent failure on first attempt → no delay, returns the permanent result immediately; respects `cancellationToken` (delegates to `Task.Delay(..., cancellationToken)`).
- [x] `GeminiRetryExecutor.cs` (`src/NewAlbumsDiscovery.Application/AIDiscovery/`) — extracted from `GenreExpansionPromptStep`'s current loop, made reusable:
  ```csharp
  public sealed class GeminiRetryExecutor
  {
      private readonly IGeminiClient _geminiClient;
      private readonly TimeProvider _timeProvider;

      public GeminiRetryExecutor(IGeminiClient geminiClient, TimeProvider timeProvider) { ... }

      public async Task<GeminiCallResult> ExecuteAsync(string prompt, IReadOnlyList<int> backoffSeconds, CancellationToken cancellationToken)
      {
          for (var attempt = 0; ; attempt++)
          {
              var result = await _geminiClient.GenerateContentAsync(prompt, cancellationToken);
              if (result.IsSuccess) return result;
              if (result.IsTransientFailure && attempt < backoffSeconds.Count)
              {
                  await Task.Delay(TimeSpan.FromSeconds(backoffSeconds[attempt]), _timeProvider, cancellationToken);
                  continue;
              }
              return result;
          }
      }
  }
  ```
  Returns the terminal `GeminiCallResult` either way (success or the final failure) — callers own what "failure" means for their step (abandonment message, fallback value, etc.), keeping this class free of any bucket/notifier knowledge.

## Step 4 ✅ — Application: refactor `GenreExpansionPromptStep` onto `GeminiRetryExecutor`

- [x] Update `GenreExpansionPromptStepTests.cs`: replace the `IGeminiClient` + `RecordingTimeProvider` constructor wiring with a `GeminiRetryExecutor` built from the same mocks/fakes (or mock `GeminiRetryExecutor` isn't needed — construct a real one against a mocked `IGeminiClient`, same as today, just routed through the executor). Every existing assertion (retry timing, abandonment message, fallback-on-malformed-JSON, instrumental/no-genre skip) must still pass — this is a refactor, not a behavior change.
- [x] `GenreExpansionPromptStep.cs` — constructor takes `GeminiRetryExecutor` instead of `IGeminiClient geminiClient` + raw `TimeProvider` for the retry path (still needs nothing else new); body becomes:
  ```csharp
  var result = await _retryExecutor.ExecuteAsync(prompt, backoffs, cancellationToken);
  if (result.IsSuccess) { state.ResolvedGenres = ResolveGenres(result.ResponseText!, bucket.Genre); return; }
  await _notifier.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, result.ErrorMessage ?? "Gemini API call failed.", cancellationToken);
  state.Abandon();
  ```
- [x] Re-run full Application test suite — confirm 0 regressions, `branch-rate="1"` still holds for `GenreExpansionPromptStep`.

## Step 5 ✅ — Application: `IBucketProcessingStep` signature change + `BucketProcessingState` extension

- [x] Update `IBucketProcessingStep.cs`: `Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, ISet<AlbumKey> existingAlbumKeys, CancellationToken cancellationToken);`.
- [x] Update `BucketProcessingState.cs`: add `public IReadOnlyList<(string Artist, string Album)> DiscoveredCandidates { get; set; } = Array.Empty<(string, string)>();` and `public int PersistedAlbumCount { get; set; }`.
- [x] Update `PrintBucketStepTests.cs` / `PrintBucketStep.cs` for the new unused parameter (mirrors how it already ignores `state`).
- [x] Update all `GenreExpansionPromptStepTests.cs` / `DiscoveryQueryPromptStepTests.cs` call sites for the extra parameter (an empty `HashSet<AlbumKey>()` suffices where the step ignores it).

## Step 6 ✅ — Application: `DiscoveryQueryPromptStep` rewrite (real Gemini call)

- [x] Rewrite `DiscoveryQueryPromptStepTests.cs`. New cases needed (in addition to updating every existing test for the new constructor/signature):
  - Success with a well-formed `[{"artist":"A","album":"B"}, ...]` response → `state.DiscoveredCandidates` populated with the exact (Artist, Album) pairs, in order; `NotifyPromptRenderedAsync` never called (method removed — compile-time enforced).
  - Malformed JSON / non-array response despite success → `state.DiscoveredCandidates` is empty, `state.IsAbandoned` stays `false`, no retry attempted (assert `IGeminiClient` invoked exactly once).
  - Transient failure then success → retries per `GeminiOptions.RetryBackoffSeconds` (reuse the `RecordingTimeProvider` pattern from Phase 9).
  - Retries exhausted / permanent failure → `NotifyBucketAbandonedAsync` called with the bucket's name/track count and the exact same format as Genre Expansion; `state.Abandon()` called; `state.DiscoveredCandidates` stays empty.
  - Existing template-selection tests (Country / CountryLanguage / CountryLanguageGenre / instrumental variants, `{{genres}}` substitution from `state.ResolvedGenres` or `bucket.Genre`) still pass, just against Gemini call assertions instead of `NotifyPromptRenderedAsync` assertions.
- [x] Rewrite `DiscoveryQueryPromptStep.cs`:
  - Keep the existing template-selection/rendering logic unchanged (lines building `values`/`rendered`).
  - Replace `await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);` with a call through `GeminiRetryExecutor.ExecuteAsync(rendered, _geminiOptions.Value.RetryBackoffSeconds, cancellationToken)`.
  - On success: parse `result.ResponseText!` into candidates via a private helper (`ParseCandidates`, using a private nested `GeminiAlbumDto(string Artist, string Album)` record + `JsonSerializer.Deserialize<List<GeminiAlbumDto>>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })`, `catch (JsonException) { return []; }` — same shape as the deleted Phase 4 `GeminiDiscoveryClient.ParseCandidates`), set `state.DiscoveredCandidates`.
  - On failure (retries exhausted or permanent): `NotifyBucketAbandonedAsync` (same call shape as `GenreExpansionPromptStep`) + `state.Abandon()`.
  - Constructor gains `GeminiRetryExecutor` and `IOptions<GeminiOptions>` (for `RetryBackoffSeconds`); ignores the new `existingAlbumKeys` parameter.

## Step 7 ✅ — Application: `AlbumPersistenceStep` (new)

- [x] **Test-first** `AlbumPersistenceStepTests.cs`:
  - No candidates on `state` → `state.PersistedAlbumCount == 0`, `IDiscoveredAlbumRepository.AddRangeAsync` never called, `NotifyBucketDiscoverySucceededAsync` called with `0`.
  - All candidates new (not in `existingAlbumKeys`) → all persisted, `existingAlbumKeys` grows by that many entries, `state.PersistedAlbumCount` matches, notifier called with that count.
  - Some candidates already in `existingAlbumKeys` (pre-seeded) → only the new ones persisted/counted; the seeded ones are silently skipped.
  - Two candidates in the same batch that are duplicates of each other (case/whitespace variants) → only one persisted.
  - Persisted `DiscoveredAlbum.ReferenceBucketId` equals the bucket's `Id`; `DiscoveredAtUtc` comes from an injected `TimeProvider`.
- [x] `AlbumPersistenceStep.cs`:
  ```csharp
  public sealed class AlbumPersistenceStep : IBucketProcessingStep
  {
      // ctor: IDiscoveredAlbumRepository, IDiscoveryNotifier, TimeProvider

      public async Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, ISet<AlbumKey> existingAlbumKeys, CancellationToken cancellationToken)
      {
          var newAlbums = AlbumDeduplicator.SelectNew(state.DiscoveredCandidates, existingAlbumKeys);
          if (newAlbums.Count > 0)
          {
              var entities = newAlbums
                  .Select(a => DiscoveredAlbum.Create(bucket.Id, a.Artist, a.Album, _timeProvider.GetUtcNow().UtcDateTime))
                  .ToList();
              await _repository.AddRangeAsync(entities, cancellationToken);
          }
          state.PersistedAlbumCount = newAlbums.Count;
          await _notifier.NotifyBucketDiscoverySucceededAsync(bucket.BucketName, newAlbums.Count, cancellationToken);
      }
  }
  ```

## Step 8 ✅ — Application: `BucketOutcome`, `DiscoveryRunReport`, `DiscoveryRunReportCalculator`

- [x] `BucketOutcome.cs` (`src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/`): `public sealed record BucketOutcome(string BucketName, BucketType BucketType, bool WasAbandoned, int AlbumsDiscovered);`.
- [x] `DiscoveryRunReport.cs`: record with `TotalBuckets`, `BucketsSkipped`, `EmptyBuckets`, `TotalAlbumsDiscovered` (all `int`), `AverageAlbumsPerBucket`/`AverageAlbumsPerLevel1`/`AverageAlbumsPerLevel2`/`AverageAlbumsPerLevel3` (all `double?`), `HighestYieldBucketNames`/`LowestYieldBucketNames` (`IReadOnlyList<string>`, empty if none eligible), `HighestYieldCount`/`LowestYieldCount` (`int`).
- [x] **Test-first** `DiscoveryRunReportCalculatorTests.cs` — table of scenarios: empty outcome list; all buckets abandoned; all buckets empty (0 albums, not abandoned); a normal mixed run with all three `BucketType`s represented; a 3-way tie for highest yield; a 2-way tie for lowest yield; a run where only one `BucketType` is present (other two levels' averages are `null`). Assert every field precisely, including tie name ordering (source order from the input list) and the `null`/`"N/A"`-eligible cases.
- [x] `DiscoveryRunReportCalculator.cs` — static, pure: `public static DiscoveryRunReport Calculate(IReadOnlyList<BucketOutcome> outcomes)`. Eligibility filter `!o.WasAbandoned && o.AlbumsDiscovered > 0` applied uniformly to the overall average, all three per-level averages, and both highest/lowest yield, per Design Notes.

## Step 9 ✅ — Application: wire it all into `BucketProcessingStage` / `AIDiscoveryPipelineContext` / `ReportPublicationStage`

- [x] Update `AIDiscoveryPipelineContext.cs`: add `IReadOnlyList<BucketOutcome> BucketOutcomes` (find and update the one call site that constructs the initial context to pass `Array.Empty<BucketOutcome>()` or equivalent).
- [x] Update `BucketProcessingStageTests.cs`: inject a mocked `IDiscoveredAlbumRepository`; assert `GetExistingAlbumKeysAsync` is called exactly once per `ExecuteAsync` call (not per bucket); assert the same `ISet<AlbumKey>` instance is passed to every step for every bucket (mutation visible across buckets); assert `context.BucketOutcomes` after execution matches each bucket's `(BucketName, BucketType, IsAbandoned, PersistedAlbumCount)`.
- [x] Update `BucketProcessingStage.cs`:
  ```csharp
  var existingKeys = await _albumRepository.GetExistingAlbumKeysAsync(cancellationToken);
  var outcomes = new List<BucketOutcome>();
  // ...loop as today, but:
  await step.ProcessAsync(bucket, state, existingKeys, cancellationToken);
  // ...after the inner foreach, before the inter-bucket delay:
  outcomes.Add(new BucketOutcome(bucket.BucketName, bucket.BucketType, state.IsAbandoned, state.PersistedAlbumCount));
  // ...
  return context with { ProcessedBucketCount = processedCount, AbandonedBucketCount = abandonedCount, BucketOutcomes = outcomes };
  ```
  Constructor gains `IDiscoveredAlbumRepository`.
- [x] Update `ReportPublicationStageTests.cs`: assert `DiscoveryRunReportCalculator`'s output (or an equivalent computed report) is passed to a new `NotifyDiscoveryReportAsync` call, before the existing `NotifyPipelineCompletedAsync` call (order matters — assert via `Mock` call-order/`Times` or a `MockSequence`).
- [x] Update `ReportPublicationStage.cs`:
  ```csharp
  var report = DiscoveryRunReportCalculator.Calculate(context.BucketOutcomes);
  await _notifier.NotifyDiscoveryReportAsync(report, cancellationToken);
  await _notifier.NotifyPipelineCompletedAsync(context.ProcessedBucketCount, context.AbandonedBucketCount, cancellationToken);
  return context;
  ```

## Step 10 ✅ — Application: `IDiscoveryNotifier` changes

- [x] `IDiscoveryNotifier.cs`: remove `NotifyPromptRenderedAsync`; add:
  ```csharp
  Task NotifyBucketDiscoverySucceededAsync(string bucketName, int albumCount, CancellationToken cancellationToken);
  Task NotifyDiscoveryReportAsync(DiscoveryRunReport report, CancellationToken cancellationToken);
  ```

## Step 11 ✅ — Infrastructure: `DiscoveredAlbumRepository`, `AppDbContext`, migration

- [x] **Test-first** `DiscoveredAlbumRepositoryTests.cs` (`tests/NewAlbumsDiscovery.Infrastructure.Tests/Sqlite/`), mirroring `AggregatedBucketRepositoryTests`' real-temp-file-SQLite-via-`Database.Migrate()` pattern:
  - `GetExistingAlbumKeysAsync` on an empty table → empty set.
  - `AddRangeAsync` then `GetExistingAlbumKeysAsync` → keys reflect the persisted rows, normalized.
  - `AddRangeAsync` called twice (simulating two buckets in one run) → both persist, nothing is overwritten (additive, unlike `AggregatedBucketRepository`).
  - Deleting the referenced `AggregatedBucket` row (raw SQL, simulating `ReplaceAllAsync`'s `DELETE FROM AggregatedBuckets`) cascades to delete the dependent `DiscoveredAlbums` row — proves the FK/cascade mapping is wired correctly (Decision 4).
- [x] `AppDbContext.cs`: add `using NewAlbumsDiscovery.Domain.AIDiscovery;`, `public DbSet<DiscoveredAlbum> DiscoveredAlbums => Set<DiscoveredAlbum>();`, and in `OnModelCreating`:
  ```csharp
  modelBuilder.Entity<DiscoveredAlbum>(entity =>
  {
      entity.ToTable("DiscoveredAlbums");
      entity.HasKey(a => a.Id);
      entity.Property(a => a.Artist).IsRequired();
      entity.Property(a => a.AlbumName).IsRequired();
      entity.Property(a => a.Status).HasConversion<string>().IsRequired();
      entity.Property(a => a.DiscoveredAtUtc).IsRequired();
      entity.HasOne<AggregatedBucket>()
          .WithMany()
          .HasForeignKey(a => a.ReferenceBucketId)
          .OnDelete(DeleteBehavior.Cascade);
  });
  ```
- [x] `DiscoveredAlbumRepository.cs` (`src/NewAlbumsDiscovery.Infrastructure/Sqlite/`): straightforward `GetExistingAlbumKeysAsync` (project `Artist`/`AlbumName` via `AsNoTracking`, map to `AlbumKey.From`, `.ToHashSet()`) and `AddRangeAsync` (`_dbContext.DiscoveredAlbums.AddRange(albums); await _dbContext.SaveChangesAsync(cancellationToken);` — no transaction/delete, additive per Design Notes).
- [x] Run `dotnet ef migrations add AddDiscoveredAlbums --project src/NewAlbumsDiscovery.Infrastructure --startup-project src/NewAlbumsDiscovery.Worker --context AppDbContext`; review the generated migration matches the mapping above (table name, FK, cascade delete, string-converted `Status` column).
- [x] Do not commit the generated `Migrations/` folder as part of this step — per standing git-safety instructions, commits only happen when explicitly requested.

## Step 12 ✅ — Infrastructure: `ConsoleDiscoveryNotifier` + DI wiring

- [x] Update `ConsoleDiscoveryNotifierTests.cs`: remove the `NotifyPromptRenderedAsync` test; add tests for `NotifyBucketDiscoverySucceededAsync` (`"Processed Successfully - {albumCount} albums found"`) and `NotifyDiscoveryReportAsync` (exact multi-line format from Design Notes, including the `"N/A"` branches for null averages and empty highest/lowest lists, and the comma-joined tie-name case).
- [x] `ConsoleDiscoveryNotifier.cs`: remove `NotifyPromptRenderedAsync`; implement the two new methods per the format above (`F2` formatting for averages, `"N/A"` when `null`/empty).
- [x] `ApplicationServiceCollectionExtensions.cs`: register `services.AddScoped<GeminiRetryExecutor>();`; add `services.AddScoped<IBucketProcessingStep, AlbumPersistenceStep>();` **after** `DiscoveryQueryPromptStep` in the existing registration block (order is load-bearing, per the existing comment above that block — persistence must run after parsing).
- [x] `InfrastructureServiceCollectionExtensions.cs`: add `services.AddScoped<IDiscoveredAlbumRepository, DiscoveredAlbumRepository>();` alongside the existing `IAggregatedBucketRepository` registration.

## Step 13 ✅ — Full verification + documentation

- [x] `dotnet build` on the full solution — zero errors/warnings.
- [x] `dotnet test` against the `.slnx` (not an individual `.csproj` — see Phase 9's As-Built note about partial test runs) — all Domain/Application/Infrastructure tests passing.
- [x] `dotnet test --collect:"XPlat Code Coverage"`; inspect the cobertura report for every touched Domain/Application class (`AlbumKey`, `DiscoveredAlbum`, `AlbumDeduplicator`, `GeminiRetryExecutor`, `GenreExpansionPromptStep`, `DiscoveryQueryPromptStep`, `AlbumPersistenceStep`, `BucketProcessingStage`, `DiscoveryRunReportCalculator`, `ReportPublicationStage`) — confirm `branch-rate="1"` on each, closing any gap with a targeted test the same way Phase 9 did (documented in As-Built Corrections if any arise).
- [x] Update `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 10: change the header to `✅ COMPLETE (<date>)`, annotate every Acceptance Criteria bullet with `**Verified** via <exact test name(s)>`, add an As-Built Corrections section (defects found/fixed, coverage gaps closed, any further deviations from this plan discovered mid-implementation).
- [x] Mark this plan file's Status line `✅ COMPLETE` and check off every step above.

---

## Open risk carried forward (documented, not resolved this phase)

Per Decision 4: because `AggregatedBucketRepository.ReplaceAllAsync` unconditionally wipes and reinserts `AggregatedBuckets` on every run, and `DiscoveredAlbums.ReferenceBucketId` cascades on delete, **`DiscoveredAlbums` is effectively cleared at the start of every scheduled run**, before that run's own AIDiscovery stage runs. Cross-run dedup and cross-run persistence for later review (Feature 3) only work within a single run's lifetime as currently wired. This was explicitly confirmed as an accepted tradeoff, not an oversight — flagging here so it's visible to whoever picks up Feature 3 later.
