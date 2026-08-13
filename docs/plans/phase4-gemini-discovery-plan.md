# Phase 4 Implementation Plan: Generative AI Engine Integration (Gemini)

**Status:** ⏪ ROLLED BACK (2026-08-13) — originally shipped complete (all steps below implemented and automated-tested), then deleted at the user's explicit request after Phase 5 established the pipeline architecture this should have been built inside. See FUNCTIONAL_REQUIREMENTS.md → Phase 4 → Rollback for exactly what was deleted vs. kept (`GeminiOptions.Model` + the API key config path). Everything below describes what was originally built and no longer reflects the codebase.
**Requirements:** [`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`](../requirements/FUNCTIONAL_REQUIREMENTS.md) → Phase 4

Strict TDD (`docs/constitution/tdd.md`): every step below writes a failing test before the implementation that makes it pass. Domain and Application code must reach 100% branch coverage; the thin `IGeminiApiClient` SDK wrapper is the one exempted Infrastructure edge (same treatment `AppDbContext`/`LovedTrackDbContext` got in Phase 3).

---

## Step 0 — Config/script groundwork (no C# yet) ✅

1. `git mv`-equivalent rename: `z-com-ai/gemini-api-key.txt` → `z-com-ai/gemini_api_key.txt` (content unchanged — it's `.gitignore`'d, so a plain filesystem rename, not a tracked git operation).
2. Extend `scripts/setup-env.ps1`:
   - Add the `_path.txt`-suffix branch to the per-file loop: if `$file.Name` ends with `_path.txt`, keep today's `Join-Path $env:USERPROFILE $relativePath` behavior; otherwise persist the trimmed content verbatim as the variable value.
   - Update the header comment to document both branches.
3. Extend `scripts/setup-env.sh` the same way (`case "$filename" in *_path.txt) ... ;; *) ... ;; esac`), update its header comment too.
4. Manual verification (no automated test — consistent with Phase 2's precedent that these scripts aren't CI-tested): re-run `setup-env.ps1` elevated and confirm `NewAlbumsDiscovery__GeminiApiKey` is set to the raw key string, and that `NewAlbumsDiscovery__Database__LovedTracksDbPath` is unaffected (still resolves via the home directory). **Not re-run live as part of this completion** — code-reviewed only; see FUNCTIONAL_REQUIREMENTS.md → Phase 4 → Acceptance Criteria.

## Step 1 — Domain layer (`src/NewAlbumsDiscovery.Domain/AIDiscovery/`) ✅

1. **Test-first:** `AlbumKeyTests` — construction normalizes case and trims whitespace; two keys built from differently-cased/whitespace-padded input are equal; different artist or album → not equal.
   **Implement:** `AlbumKey` — immutable value object (`readonly record struct` per `DDD-architecture.md` §3), `AlbumKey.From(string artist, string album)`.
2. **Test-first:** `DiscoveredAlbumTests` — constructor throws on null/empty `Artist`, `Album`, or `Country`; valid construction round-trips all properties; `Create(...)` generates a fresh `Guid`; equality is by `Id` alone (mirrors `AggregatedBucket`).
   **Implement:** `DiscoveredAlbum` entity — `Id`, `Artist`, `Album`, `Country`, `Language` (nullable), `Genre` (nullable), `DiscoveredAtUtc`; constructor + `Create(...)` factory, same shape as `AggregatedBucket`.
3. **Test-first:** `AlbumDeduplicatorTests` — empty candidate list → empty result; no overlap with existing keys → all candidates returned; full overlap → empty result; partial overlap → only non-matching candidates returned; case/whitespace-insensitive match confirmed; duplicate candidates *within* the same candidate list collapse to one.
   **Implement:** `AlbumDeduplicator.SelectNew(IReadOnlyList<(string Artist, string Album)> candidates, IReadOnlySet<AlbumKey> existingKeys)` — pure static method.

## Step 2 — Application layer (`src/NewAlbumsDiscovery.Application/AIDiscovery/`) ✅

1. `GeminiOptions` — `Model` (default `"gemini-1.5-flash"`), `MaxAlbumsPerPrompt` (default `20`), `Timeframe` (default `"last 30 days"`), `MaxRetryAttempts` (default `3`), `InitialBackoffSeconds` (default `2`). No test needed (plain data holder, same as `AggregatorSettings`).
2. `CandidateAlbum(string Artist, string Album)` and `DiscoveryPromptRequest(string Country, string? Language, string? Genre, int MaxAlbums, string Timeframe)` records.
3. `IGeminiDiscoveryClient` — `Task<IReadOnlyList<CandidateAlbum>> DiscoverAsync(DiscoveryPromptRequest request, CancellationToken cancellationToken)`.
4. `IDiscoveredAlbumRepository` — `Task<IReadOnlyList<DiscoveredAlbum>> GetAllAsync(CancellationToken ct)`, `Task AddRangeAsync(IReadOnlyList<DiscoveredAlbum> albums, CancellationToken ct)`.
5. Extend `IAggregatedBucketRepository` (existing file, `MusicAggregator` namespace) with `Task<IReadOnlyList<AggregatedBucket>> GetAllAsync(CancellationToken ct)`.
6. **Test-first:** `DiscoverAlbumsCommandHandlerTests` (mock all three ports + `ILovedTrackRepository`-style fake `TimeProvider`, per Phase 3's `AggregateMusicPreferencesCommandHandlerTests` pattern):
   - No buckets → `IGeminiDiscoveryClient` never called, `AddRangeAsync` never called.
   - One bucket, all-new candidates → all persisted with the bucket's `Country`/`Language`/`Genre` and `TimeProvider`'s current instant.
   - One bucket, candidates already in `GetAllAsync()`'s existing set → none persisted.
   - Two buckets returning the same `(Artist, Album)` (case/whitespace variant) → only the first occurrence persisted.
   - `IGeminiDiscoveryClient` returns an empty list for a bucket → no error, other buckets still processed.
   **Implement:** `DiscoverAlbumsCommand : IRequest` (no params) + `DiscoverAlbumsCommandHandler` — reads buckets + existing albums, loops buckets building a `DiscoveryPromptRequest` per bucket, calls the client, runs candidates through `AlbumDeduplicator.SelectNew`, accumulates newly-created `DiscoveredAlbum`s (adding each to the in-memory existing-keys set immediately, enforcing the cross-bucket same-run dedup rule), and calls `AddRangeAsync` once at the end (skip the call entirely if nothing new was found).
7. Register `services.Configure<GeminiOptions>(configuration.GetSection("NewAlbumsDiscovery:Gemini"));` in `ApplicationServiceCollectionExtensions.AddApplicationServices`. (MediatR already scans the whole Application assembly, so the new handler needs no explicit registration.)

## Step 3 — NuGet + prompt template assets ✅

1. `dotnet add package Mscc.GenerativeAI` (from `NewAlbumsDiscovery.Infrastructure`), then move the resolved version into `Directory.Packages.props` as a `<PackageVersion>` entry (central package management, matching every other dependency) and drop the version attribute from the project's `<PackageReference>`.
2. Copy (not move) the three in-scope files from `docs/specs/assets/prompts/` into `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/`:
   - `country-prompt.md`, `country-language-prompt.md`, `country-language-genres-prompt.md` (renamed mid-implementation — see FUNCTIONAL_REQUIREMENTS.md → Phase 4 "Mid-implementation drift" note; its `{{genres}}` placeholder is populated with the bucket's single `Genre` value, a one-item list).
   - `country-genres-prompt.md`, `genre-expansion-prompt.md`, and `docs/specs/assets/countries-languages.json` are explicitly **not** used — confirmed out of scope.
3. Mark them as embedded resources in `NewAlbumsDiscovery.Infrastructure.csproj`:
   ```xml
   <ItemGroup>
     <EmbeddedResource Include="Gemini\Prompts\*.md" />
   </ItemGroup>
   ```

## Step 4 — Infrastructure: Gemini client (`src/NewAlbumsDiscovery.Infrastructure/Gemini/`) ✅

1. `IGeminiApiClient` (public, not `internal` as originally sketched — no `InternalsVisibleTo` exists in this codebase; see As-Built Corrections in FUNCTIONAL_REQUIREMENTS.md) — `Task<string> GenerateContentAsync(string prompt, CancellationToken ct)`.
2. `GeminiApiClient : IGeminiApiClient` — thin wrapper constructing `Mscc.GenerativeAI`'s client with the raw API key and configured model, calling it, returning the raw text response. **No dedicated unit test** (exempted edge, per Design Notes) — exercised implicitly whenever the app actually runs against the real API.
3. **Test-first:** `PromptTemplateLoaderTests` (or inline in `GeminiDiscoveryClientTests` if simpler once written) — given a `DiscoveryPromptRequest` with `Genre` set, the genre template is selected and all three of `{{country}}`/`{{language}}`/`{{genre}}` are substituted; `Genre` null but `Language` set → language template, no `{{genre}}` placeholder left unsubstituted; both null → country template only.
4. **Test-first:** `GeminiDiscoveryClientTests` (mock `IGeminiApiClient` with Moq):
   - Valid JSON response → maps to the expected `CandidateAlbum` list.
   - Malformed JSON on first attempt, valid JSON on retry → succeeds, `IGeminiApiClient` called exactly twice, one `Task.Delay` observed via fake `TimeProvider`.
   - Malformed JSON on every attempt → after `MaxRetryAttempts`, returns an empty list, logs an error (verify via a captured `ILogger`), does not throw.
   - `IGeminiApiClient` throws a rate-limit-shaped exception → same backoff/retry path, eventually succeeds.
   - Backoff delays follow `InitialBackoffSeconds * 2^attempt` and are verified against the fake `TimeProvider`, not real elapsed time.
   **Implement:** `GeminiDiscoveryClient : IGeminiDiscoveryClient` — loads/caches the three embedded templates once (static or singleton-scoped), selects + renders the template per Design Notes' branching rule, calls `IGeminiApiClient` inside the retry loop, deserializes with `System.Text.Json`, maps to `CandidateAlbum`.

## Step 5 — Infrastructure: persistence (`src/NewAlbumsDiscovery.Infrastructure/Sqlite/`) ✅

1. Add `DiscoveredAlbum` mapping to `AppDbContext.OnModelCreating` (new `DbSet<DiscoveredAlbum> DiscoveredAlbums`), following the same explicit-`Property()`-per-field pattern as `AggregatedBucket` (Phase 3's As-Built lesson: implicit mapping fails EF's constructor-binding).
2. `dotnet ef migrations add AddDiscoveredAlbums` (via the `AppDbContextFactory` design-time factory already in place) — creates the `DiscoveredAlbums` table alongside the existing `AggregatedBuckets` one, same database, same migration history.
3. Implement `AggregatedBucketRepository.GetAllAsync` (`_dbContext.AggregatedBuckets.AsNoTracking().ToListAsync(ct)`).
4. **Test-first:** `DiscoveredAlbumRepositoryTests` (real temp-file SQLite via `Database.Migrate()`, mirroring `AggregatedBucketRepositoryTests`):
   - `AddRangeAsync` on an empty table persists all rows.
   - A second `AddRangeAsync` call **adds to**, does not replace, existing rows (contrast with `AggregatedBucketRepository.ReplaceAllAsync`).
   - `GetAllAsync` round-trips all fields including nullable `Language`/`Genre`.
   **Implement:** `DiscoveredAlbumRepository : IDiscoveredAlbumRepository`.
5. **Test-first (regression):** extend `AggregatedBucketRepositoryTests` with a case confirming `GetAllAsync` returns exactly what `ReplaceAllAsync` last wrote.

## Step 6 — DI wiring ✅

In `InfrastructureServiceCollectionExtensions.AddInfrastructureServices`:
1. Read `configuration["NewAlbumsDiscovery:GeminiApiKey"]`; throw `InvalidOperationException` with a descriptive message (same style as the `LovedTracksDbPath` check) if missing/whitespace.
2. Register `IGeminiApiClient` via a factory lambda capturing the validated API key and `IOptions<GeminiOptions>.Value.Model`.
3. Register `IGeminiDiscoveryClient -> GeminiDiscoveryClient`, `IDiscoveredAlbumRepository -> DiscoveredAlbumRepository` (both `AddScoped`, consistent with the existing repositories).

## Step 7 — Config files ✅

Add to both `appsettings.json` and `appsettings.Development.json`, under the existing `NewAlbumsDiscovery` section, alongside `Aggregator`:
```json
"Gemini": {
  "Model": "gemini-1.5-flash",
  "MaxAlbumsPerPrompt": 20,
  "Timeframe": "last 30 days",
  "MaxRetryAttempts": 3,
  "InitialBackoffSeconds": 2
}
```
(`GeminiApiKey` itself is never written to either file — machine-env-var only, same treatment as the database paths.)

## Step 8 — Full-suite verification (partially ✅ — see item 4)

1. `dotnet build` — solution builds clean. **Done.**
2. `dotnet test` — all existing Phase 1–3 tests still pass (no regressions), all new Phase 4 tests pass. **Done** — 115/115 passing (77 Domain, 12 Application, 26 Infrastructure).
3. Coverage report confirms 100% branch coverage on the new `Domain/AIDiscovery` and `Application/AIDiscovery` code (`AlbumKey`, `DiscoveredAlbum`, `AlbumDeduplicator`, `DiscoverAlbumsCommandHandler`). **Done** — `branch-rate="1"` confirmed on all listed classes plus `GeminiOptions`/`CandidateAlbum`/`DiscoveryPromptRequest`.
4. **Not done.** Manual: with a real `NewAlbumsDiscovery__GeminiApiKey` set (via the updated setup script) and at least one row in `AggregatedBuckets`, manually resolve `IMediator` in a throwaway harness (or a temporary call from `Program.cs`, removed before commit) and confirm `DiscoverAlbumsCommand` populates `DiscoveredAlbums` end-to-end against the real Gemini API — the one thing no automated test covers per Step 4's scope decision. **Do not wire this call permanently into `Program.cs`** — that's explicitly Out of Scope (deferred trigger wiring). This remains the one recommended follow-up before relying on Phase 4 against the real Gemini API in a live environment.

## Explicit non-goals (see FUNCTIONAL_REQUIREMENTS.md → Phase 4 → Out of Scope)

- No hosted service, no `Program.cs` change, no automatic run ordering relative to `AggregateMusicPreferencesCommand`.
- No Notification/Telegram consumption of `DiscoveredAlbums`.
- No live-API automated tests.
- No `Retry-After` header handling.
