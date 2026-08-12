# Phase 3 Implementation Plan: Music Preference Aggregator Engine & Storage

**Requirements source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 3
**Status:** ✅ COMPLETE (2026-08-12) — `dotnet build`/`dotnet test` green with 100% branch coverage on Domain/Application, end-to-end Worker run confirmed by the user's own manual testing.

## Recap of Confirmed Decisions
- `BucketAggregatorEngine` takes `asOfUtc` as a parameter (from an injected `TimeProvider`, never a direct clock read); each `AggregatedBucket` self-generates its `Guid` Id.
- Null/empty `CountryOrRegion` → `"Unknown"`; malformed/null `LanguagesJson`/`GenresJson` → empty list. Normalized at the `LovedTrackRepository` boundary; `LovedTrackPreferences` (Domain VO) enforces the invariant afterward, so the engine never branches on bad data.
- Full EF Core migration for `AppDbContext`, applied via `Database.Migrate()` at Worker startup.
- New `AggregationStartupWorker` sends the command once at startup, on a background thread, alongside the existing `HeartbeatWorker`.
- Level 2 groups by `(Country, Language)`, Level 3 by `(Country, Language, Genre)` — composite keys, not global.
- Threshold values cross Domain/Application as a new Domain VO `AggregationThresholds` (not `AggregatorSettings` directly — Domain must not reference Application).
- `LovedTrackDbContext` maps `LovedTracks` as a keyless entity (`HasNoKey()`) — real external PK schema is unknown.
- `AppDbPath` has a code-level default (`<AppBaseDirectory>/Database/new-albums-discovery.db`), overridable via `NewAlbumsDiscovery__Database__AppDbPath` if set — no setup-script change needed.
- EF Core maps directly onto the Domain `AggregatedBucket` entity (no separate Infrastructure persistence model).
- 100% branch coverage gate applies to `Domain`/`Application`; `Infrastructure` is tested via real temp-file SQLite, best-effort coverage.

## As-Built Corrections (discovered during implementation)
- **EF Core constructor binding needs every mapped property explicitly touched in `OnModelCreating`, not just the ones needing special configuration.** Only calling `HasKey(b => b.Id)`, `Property(b => b.BucketType).HasConversion<string>()`, and `Property(b => b.CreatedAtUtc).IsRequired()` left `BucketName`/`Country`/`Language`/`Genre`/`TrackCount` unrecognized as mapped properties, and `dotnet ef migrations add` failed with "No suitable constructor was found... Cannot bind 'bucketName', 'country', 'language', 'genre', 'trackCount'". **Fix:** every constructor-bound property now gets its own explicit `entity.Property(...)` call in `AppDbContext.OnModelCreating`, even the ones with no special conversion.
- **A design-time `IDesignTimeDbContextFactory<AppDbContext>` was required.** `dotnet ef migrations add` tries to build the real Worker DI container to resolve `AppDbContext`, which fails because `AddInfrastructureServices` eagerly validates `NewAlbumsDiscovery__Database__LovedTracksDbPath` — an env var that isn't set at design time on a fresh dev machine. Added `NewAlbumsDiscovery.Infrastructure/Sqlite/AppDbContextFactory.cs` (internal, design-time only, placeholder connection string) so migrations can be authored without needing real environment configuration.
- **The EF tools' startup project also needs its own `Microsoft.EntityFrameworkCore.Design` reference**, not just the Infrastructure project being migrated — `dotnet ef` resolves tooling relative to `--startup-project` (`NewAlbumsDiscovery.Worker`). Added the same `PrivateAssets="all"` reference there.
- **Two transitive NuGet vulnerabilities pinned above what EF Core 10.0.0 pulls by default**, both dev/build-time only, never shipped to the running app: `SQLitePCLRaw.bundle_e_sqlite3` 2.1.11 → 2.1.12 (CVE-2025-6965, bundled SQLite < 3.50.2) and `System.Security.Cryptography.Xml` 9.0.0 → 10.0.11 (several known CVEs, pulled in by `Microsoft.EntityFrameworkCore.Design`). Both pins live in `Directory.Packages.props` with matching `PackageReference` overrides in `Infrastructure.csproj` and `Worker.csproj`.
- **Local `dotnet-ef` tool manifest lives at `.config/dotnet-tools.json`** (moved there after `dotnet new tool-manifest` initially created it at the repo root) — the conventional location so `dotnet tool restore` works for anyone cloning the repo.
- **`LovedTrackDbContext` connection string uses SQLite's `Mode=ReadOnly`** (`Data Source={path};Mode=ReadOnly`), not just C#-level read-only repository code — enforces the "strictly read-only" constraint at the driver level too, not documented as a separate decision in the original plan but a natural extension of it.

## Step-by-Step Plan

### Step 1 — Package references
- [x] Add to `Directory.Packages.props`: `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` (pin to the latest stable 10.x release compatible with the pinned `10.0.204` SDK — resolve exact version via `dotnet add package` at implementation time rather than guessing a number now).
- [x] Reference `Microsoft.EntityFrameworkCore.Sqlite` from `NewAlbumsDiscovery.Infrastructure.csproj`.
- [x] Reference `Microsoft.EntityFrameworkCore.Design` from `NewAlbumsDiscovery.Infrastructure.csproj` as a dev-time-only dependency (`PrivateAssets="all"`) — needed for `dotnet ef migrations add`, not needed at runtime.
- [x] Confirm `dotnet-ef` CLI tool is available locally (install as a local tool via `dotnet tool install` if missing) to run migration commands.

### Step 2 — Domain layer (`src/NewAlbumsDiscovery.Domain/MusicAggregator/`) — TDD red/green
- [x] **Tests first** (`tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/`):
  - `LovedTrackPreferencesTests` — constructor rejects null/empty `Country`; rejects null `Languages`/`Genres` (empty list is fine, null is not); accepts valid input; value equality.
  - `AggregationThresholdsTests` — constructor validates positive thresholds; value equality.
  - `AggregatedBucketTests` — constructor invariants (non-empty `BucketName`, `TrackCount >= 0`, `Country` required, `Language`/`Genre` nullable per `BucketType`).
  - `BucketAggregatorEngineTests` — the core suite, covering every case in `phase3-requirements.md` §8:
    - Empty `LovedTracks` list → empty bucket list.
    - Country count = 9 → forms a `Country` bucket; count = 10 → cascades to Level 2 (exact boundary, both sides).
    - Country/Language count = 9 → forms a `CountryLanguage` bucket; count = 10 → cascades to Level 3 (exact boundary, both sides).
    - Level 3 always forms a `CountryLanguageGenre` bucket, never cascades further.
    - Multi-language expansion: a track with 2 languages counted once per language at Level 2.
    - Multi-genre expansion: a track with 3 genres counted once per genre at Level 3.
    - Composite grouping: two different cascading countries sharing the same language produce two separate `CountryLanguage` buckets (not merged).
    - Minimum threshold filtering: bucket with count = 1 discarded; count = 2 retained (exact boundary, both sides), applied uniformly across all three bucket types.
    - `asOfUtc` parameter flows through to every returned bucket's `CreatedAtUtc` unchanged.
- [x] **Implementation** (after tests are red):
  - `LovedTrackPreferences` (immutable value object: `Country`, `IReadOnlyList<string> Languages`, `IReadOnlyList<string> Genres`).
  - `AggregationThresholds` (immutable value object: `CountryRegionThreshold`, `CountryRegionLanguageThreshold`, `MinimumBucketThreshold`).
  - `BucketType` enum (`Country`, `CountryLanguage`, `CountryLanguageGenre`).
  - `AggregatedBucket` entity (`Id`, `BucketName`, `BucketType`, `Country`, `Language`, `Genre`, `TrackCount`, `CreatedAtUtc`; self-assigns `Id` via `Guid.NewGuid()` in its constructor).
  - `BucketAggregatorEngine.Aggregate(IReadOnlyList<LovedTrackPreferences> tracks, AggregationThresholds thresholds, DateTime asOfUtc) : IReadOnlyList<AggregatedBucket>` — the 3-level cascade + minimum-threshold filter.
- [x] Run tests, confirm green; run coverage, confirm 100% branch coverage on all of the above.

### Step 3 — Application layer (`src/NewAlbumsDiscovery.Application/MusicAggregator/`) — TDD red/green
- [x] **Tests first** (`tests/NewAlbumsDiscovery.Application.Tests/MusicAggregator/`):
  - `AggregateMusicPreferencesCommandHandlerTests` (Moq for `ILovedTrackRepository`/`IAggregatedBucketRepository`, a fake/stub `TimeProvider`):
    - Happy path: repository tracks mapped correctly into the engine call, engine's output passed unchanged to `IAggregatedBucketRepository.ReplaceAllAsync`.
    - `AggregatorSettings` values map correctly into `AggregationThresholds`.
    - `TimeProvider`'s current time is what reaches the engine as `asOfUtc` (no direct `DateTime.UtcNow` anywhere in the handler).
    - Cancellation: a canceled token passed to `GetAllAsync`/`ReplaceAllAsync` propagates (handler doesn't swallow `OperationCanceledException`).
    - Empty repository result → handler still calls `ReplaceAllAsync` with an empty list (so a prior run's stale buckets get cleared, not left behind).
- [x] **Implementation**:
  - `AggregatorSettings` (`CountryRegionThreshold = 10`, `CountryRegionLanguageThreshold = 10`, `MinimumBucketThreshold = 2` defaults).
  - `ILovedTrackRepository` (`Task<IReadOnlyList<LovedTrackPreferences>> GetAllAsync(CancellationToken ct)`).
  - `IAggregatedBucketRepository` (`Task ReplaceAllAsync(IReadOnlyList<AggregatedBucket> buckets, CancellationToken ct)`).
  - `AggregateMusicPreferencesCommand` (empty MediatR `IRequest`) + `AggregateMusicPreferencesCommandHandler`.
  - Update `AddApplicationServices()` to accept `IConfiguration` and call `.Configure<AggregatorSettings>(configuration.GetSection("NewAlbumsDiscovery:Aggregator"))`; register `BucketAggregatorEngine` as a singleton; register `TimeProvider.System` as a singleton (`services.AddSingleton(TimeProvider.System)`) if not already present.
  - Update the one call site in `Program.cs` (`AddApplicationServices()` → `AddApplicationServices(builder.Configuration)`, or equivalent for `Host.CreateDefaultBuilder`'s `ConfigureServices` callback which exposes `HostBuilderContext.Configuration`).
- [x] Run tests, confirm green; confirm 100% branch coverage.

### Step 4 — Infrastructure layer (`src/NewAlbumsDiscovery.Infrastructure/Sqlite/`)
- [x] `LovedTrackDbContext` — `DbSet<LovedTrackRow>` (internal EF row shape: `CountryOrRegion`, `LanguagesJson`, `GenresJson`), `modelBuilder.Entity<LovedTrackRow>().HasNoKey().ToTable("LovedTracks")`. Constructor takes `DbContextOptions<LovedTrackDbContext>`.
- [x] `LovedTrackRepository : ILovedTrackRepository` — queries `LovedTrackDbContext`, maps each row to `LovedTrackPreferences`: `CountryOrRegion` null/empty → `"Unknown"`; `LanguagesJson`/`GenresJson` deserialized via `System.Text.Json`, catching `JsonException` (and treating `null` content) as an empty list.
  - **Tests** (`tests/NewAlbumsDiscovery.Infrastructure.Tests/Sqlite/LovedTrackRepositoryTests.cs`, real temp-file SQLite seeded directly via ADO.NET/EF, no mocking): valid row maps correctly; null `CountryOrRegion` → `"Unknown"`; malformed JSON in `LanguagesJson`/`GenresJson` → empty list, no throw; empty table → empty result.
- [x] `AppDbContext` — `DbSet<AggregatedBucket>` mapped directly onto the Domain entity (private setters + EF-only constructor via reflection, `BucketType` via `.HasConversion<string>()`), `ToTable("AggregatedBuckets")` matching the DDL in `phase3-requirements.md` §6. Constructor takes `DbContextOptions<AppDbContext>`.
- [x] `AggregatedBucketRepository : IAggregatedBucketRepository` — `ReplaceAllAsync` implements the atomic `BeginTransactionAsync` → `ExecuteSqlRawAsync("DELETE FROM AggregatedBuckets")` → `AddRange` → `SaveChangesAsync` → `CommitAsync` sequence from `phase3-requirements.md` §6, honoring the cancellation token throughout.
  - **Tests** (`AggregatedBucketRepositoryTests.cs`, real temp-file SQLite, migrations applied in test setup): first write persists all buckets; second write with a different bucket set fully replaces the first (no leftover rows); empty list replace clears the table.
- [x] `InfrastructureServiceCollectionExtensions.AddInfrastructureServices()` — accept `IConfiguration`; register both `DbContext`s via `AddDbContext<T>(options => options.UseSqlite($"Data Source={path}"))`:
  - `LovedTrackDbContext` path: `configuration["NewAlbumsDiscovery:Database:LovedTracksDbPath"]` — throw a clear startup exception if unset or the file doesn't exist (never silently create it; it's externally owned).
  - `AppDbContext` path: `configuration["NewAlbumsDiscovery:Database:AppDbPath"]` if set, else `Path.Combine(AppContext.BaseDirectory, "Database", "new-albums-discovery.db")`; ensure the containing `Database` directory exists (`Directory.CreateDirectory`) before EF Core touches it.
  - Register `ILovedTrackRepository` → `LovedTrackRepository`, `IAggregatedBucketRepository` → `AggregatedBucketRepository`.
- [x] Update the one call site in `Program.cs` for the new `AddInfrastructureServices(configuration)` signature.

### Step 5 — EF Core migration
- [x] Run `dotnet ef migrations add InitialCreate --project src/NewAlbumsDiscovery.Infrastructure --startup-project src/NewAlbumsDiscovery.Worker --context AppDbContext`, review the generated migration matches the `AggregatedBuckets` DDL from `phase3-requirements.md` §6 exactly (column names, types, nullability, primary key).
- [ ] Commit the generated `Migrations/` folder — **not committed yet**; per standing git-safety instructions, commits only happen when explicitly requested.
- [x] In `Program.cs`, after `host.Build()` and before `host.Run()`: open a DI scope, resolve `AppDbContext`, call `Database.Migrate()`.

### Step 6 — Worker wiring
- [x] Add `AggregationStartupWorker : BackgroundService` to `src/NewAlbumsDiscovery.Worker/` — in `ExecuteAsync`, wrap the MediatR send in `Task.Run(() => sender.Send(new AggregateMusicPreferencesCommand(), stoppingToken), stoppingToken)`, log start/completion/bucket-count, let exceptions propagate to the host's standard fault handling (no swallowing).
- [x] Register `AddHostedService<AggregationStartupWorker>()` in `Program.cs` alongside the existing `AddHostedService<HeartbeatWorker>()` — both run independently, `HeartbeatWorker` untouched.
- [x] Add `NewAlbumsDiscovery:Aggregator` section (`CountryRegionThreshold: 10`, `CountryRegionLanguageThreshold: 10`, `MinimumBucketThreshold: 2`) to `appsettings.json` and `appsettings.Development.json`.

### Step 7 — Verification
- [x] `dotnet build` succeeds.
- [x] `dotnet test` passes; coverage report confirms 100% branch coverage on `Domain`/`Application` `MusicAggregator` code.
- [x] Manual local run against a real `loved-tracks.db` exercising the Acceptance Criteria — **confirmed by the user's own manual testing** (2026-08-12, `/complete Phase 3`).
- [x] Confirm a second run replaces (not duplicates) `AggregatedBuckets` contents — verified via `AggregatedBucketRepositoryTests.ReplaceAllAsync_SecondWrite_FullyReplacesFirst` (automated, real temp-file SQLite), not yet via a full end-to-end Worker run.

### Step 8 — Wrap-up
- [x] Update `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` Phase 3 with any as-built corrections found during implementation.
- [x] Mark this plan done — user gave explicit go-ahead via `/complete Phase 3` (2026-08-12).

## Files To Be Changed
```
Directory.Packages.props                                  (add EF Core Sqlite + Design packages)
src/NewAlbumsDiscovery.Domain/
  MusicAggregator/
    LovedTrackPreferences.cs        (new)
    AggregationThresholds.cs        (new)
    BucketType.cs                   (new)
    AggregatedBucket.cs             (new)
    BucketAggregatorEngine.cs       (new)
src/NewAlbumsDiscovery.Application/
  MusicAggregator/
    AggregatorSettings.cs               (new)
    ILovedTrackRepository.cs            (new)
    IAggregatedBucketRepository.cs      (new)
    AggregateMusicPreferencesCommand.cs (new, + handler)
  Common/ApplicationServiceCollectionExtensions.cs   (modified: IConfiguration param, new registrations)
  NewAlbumsDiscovery.Application.csproj               (add EF Core Sqlite? no — stays Domain-only + MediatR)
src/NewAlbumsDiscovery.Infrastructure/
  Sqlite/
    LovedTrackDbContext.cs          (new)
    LovedTrackRepository.cs         (new)
    AppDbContext.cs                 (new)
    AggregatedBucketRepository.cs   (new)
    Migrations/                     (new, generated)
  InfrastructureServiceCollectionExtensions.cs   (modified: IConfiguration param, DbContext + repo registrations)
  NewAlbumsDiscovery.Infrastructure.csproj         (add EF Core Sqlite + Design package references)
src/NewAlbumsDiscovery.Worker/
  AggregationStartupWorker.cs       (new)
  Program.cs                        (modified: Migrate() call, new hosted service registration, config param threading)
  appsettings.json / appsettings.Development.json   (modified: Aggregator section)
tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/          (new test files)
tests/NewAlbumsDiscovery.Application.Tests/MusicAggregator/     (new test files)
tests/NewAlbumsDiscovery.Infrastructure.Tests/Sqlite/           (new test files)
```

## Explicitly Not Done In This Phase
- Feature 2 (Gemini/AI Discovery) and Feature 3 (Telegram/Notification) — no `DiscoveredAlbums` table, no Gemini/Telegram code or config.
- Any periodic/monthly scheduling — `AggregationStartupWorker` runs exactly once per process start.
- `scripts/setup-env.*` changes for `AppDbPath` (code-level default instead).
- CI pipeline / automated coverage-gate enforcement.
