# Functional Requirements

> Tracks implemented functional requirements per phase. Claude Code owned — see `.agents/rules/multi-agent-governance.md`.

## Phase 1: Cross-Platform Application Scaffolding — ✅ COMPLETE (2026-08-12)

### Goal
Stand up the empty solution/project skeleton for New Albums Discovery as a .NET 10 cross-platform background worker (Windows Service / Linux daemon), with the layered architecture, DI wiring, configuration, and logging plumbing in place — but **no feature logic** (no MusicAggregator/AIDiscovery/Notification business code, no SQLite/Gemini/Telegram integrations). Those land in later phases.

### Decisions (confirmed with user 2026-08-12)
1. **Project granularity:** One project per architectural layer (Domain, Application, Infrastructure, Worker). The three bounded contexts (`MusicAggregator`, `AIDiscovery`, `Notification`) exist as namespace folders inside each layer, not as separate projects. Context isolation is enforced by convention/code review, per `docs/constitution/DDD-architecture.md` §4 red flags.
2. **Test framework:** xUnit, with Moq for mocking (per `docs/constitution/tdd.md` §3 example tooling).
3. **Phase 1 scope:** Pure skeleton only — solution + empty layered projects (with namespace folders per context), DI container wiring, `IOptions<T>` configuration binding pattern, logging, and cross-platform hosting (`.UseWindowsService()` / `.UseSystemd()`). Test projects are created and wired to the solution but contain no test files yet (no feature code exists to test). No SQLite/Gemini/Telegram code.
4. **CI:** Deferred. Phase 1 only needs to build and run locally via `dotnet build` / `dotnet run`.

### As-Built Corrections (discovered during implementation)
The following deviate from the original plan and supersede it — this section is the authoritative record of what actually shipped:
- **No `AddDomainServices()` extension.** `Domain` does not get a DI registration method. Adding one would require a `Microsoft.Extensions.DependencyInjection` package reference in `Domain`, which violates the Zero-Dependency Rule (`docs/constitution/DDD-architecture.md` §1). Only `Application` (`AddApplicationServices()`) and `Infrastructure` (`AddInfrastructureServices()`) participate in DI wiring; `Domain` stays out of that chain entirely until it has something to register.
- **Hosting API:** `Program.cs` uses `Host.CreateDefaultBuilder(args)` (returns `IHostBuilder`), not `Host.CreateApplicationBuilder(args)` (returns `HostApplicationBuilder`). The latter has no `.UseWindowsService()`/`.UseSystemd()` surface in this SDK — those extensions require `IHostBuilder`.
- **Logging:** settled on the built-in `Microsoft.Extensions.Logging` console provider (no Serilog), keeping Phase 1's footprint minimal.
- **Solution file format:** `dotnet new sln` on the installed .NET 10 SDK (10.0.204) generates `NewAlbumsDiscovery.slnx` (the new XML solution format), not the classic `.sln`. Kept as-is — `dotnet sln add`/`dotnet build`/`dotnet test` all work against it transparently.
- **`global.json` added** at repo root, pinning the SDK to `10.0.204` (`rollForward: latestFeature`) — not in the original plan, but needed because multiple SDKs (9.0.205, 9.0.304, 10.0.204) are installed and the build must deterministically target .NET 10.

### In Scope
- Solution file (`NewAlbumsDiscovery.slnx`) and `global.json` (SDK pin) at repo root.
- `src/NewAlbumsDiscovery.Domain` — pure C# project (Zero-Dependency Rule), **zero** NuGet packages, namespace folders `MusicAggregator/`, `AIDiscovery/`, `Notification/`. No DI extension (see As-Built Corrections).
- `src/NewAlbumsDiscovery.Application` — use-case/orchestration layer, references Domain only, wires MediatR (per `docs/constitution/coding-principles.md` §1 SRP guidance) with no handlers yet, namespace folders per context, `AddApplicationServices()` DI extension in `Common/`.
- `src/NewAlbumsDiscovery.Infrastructure` — references Application (for interfaces) and Domain, empty namespace folders `Sqlite/`, `Gemini/`, `Telegram/` reserved for later phases, `AddInfrastructureServices()` no-op DI extension.
- `src/NewAlbumsDiscovery.Worker` — the executable host: `Host.CreateDefaultBuilder(args)`, `Microsoft.Extensions.Hosting.WindowsServices` (`UseWindowsService()`) and `Microsoft.Extensions.Hosting.Systemd` (`UseSystemd()`), `appsettings.json` / `appsettings.Development.json`, built-in console logging, a `HeartbeatWorker` `BackgroundService` (configurable interval, default 60s) to prove the host runs cross-platform, calling `AddApplicationServices()` / `AddInfrastructureServices()` from `Program.cs`.
- `tests/NewAlbumsDiscovery.Domain.Tests`, `tests/NewAlbumsDiscovery.Application.Tests`, `tests/NewAlbumsDiscovery.Infrastructure.Tests` — xUnit + Moq projects wired into the solution and referencing their corresponding `src` project, no test files yet.
- `Directory.Build.props` at repo root for shared MSBuild settings (`TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`).
- `Directory.Packages.props` for central NuGet package version management (DRY on package versions across projects).
- Config binding demonstrates the `NewAlbumsDiscovery__Section__Key` double-underscore env var convention from `docs/specs/technical-specs.md` §2 structurally (via standard `IConfiguration`/`IOptions<T>` host behavior), without inventing concrete Gemini/Telegram option classes yet.

### Out of Scope (future phases)
- MusicAggregator bucket-aggregation logic and `AggregatedBuckets` persistence.
- Gemini API integration, prompt templates, retry/resilience policies.
- Telegram Bot API integration and notification batching.
- SQLite schema/migrations, repository implementations.
- CI pipeline, coverage-gate enforcement.
- Any unit tests (no behavior exists yet to test).

### Acceptance Criteria — all verified ✅
- `dotnet build` succeeds from repo root against `NewAlbumsDiscovery.slnx`. **Verified.**
- `dotnet run --project src/NewAlbumsDiscovery.Worker` starts the host and logs a startup message and at least one heartbeat. **Verified** (startup + heartbeat log lines observed; process terminated via timeout in the non-interactive verification shell rather than a literal Ctrl+C, but shutdown was clean).
- `Domain` project has zero NuGet package references. **Verified** — `src/NewAlbumsDiscovery.Domain/NewAlbumsDiscovery.Domain.csproj` contains no `<PackageReference>` nodes.
- Solution structure and DI registration boundaries match `docs/constitution/DDD-architecture.md` and `docs/constitution/coding-principles.md`. **Verified**, with the `AddDomainServices()` correction noted above.
- `dotnet test` runs successfully (0 tests found is an acceptable pass state for this phase). **Verified.**

### Implementation Record
- Plan: [`docs/plans/phase1-scaffolding-plan.md`](../plans/phase1-scaffolding-plan.md) — all steps checked off, status marked complete.

## Phase 2: Environment Variable Developer Setup Scripts — ✅ COMPLETE (2026-08-12)

**Source:** `docs/requirements/phase2-requirements.md` (Antigravity-owned input spec — see `.agents/rules/multi-agent-governance.md`), finalized here after clarification.

### Goal
Cross-platform developer setup scripts (`scripts/setup-env.ps1` for Windows, `scripts/setup-env.sh` for Linux) that scan `z-com-ai/*.txt`, resolve each file's content as a path relative to the current user's home directory, and persist the result as a Machine-level `NewAlbumsDiscovery__*` environment variable — so a Windows Service or Linux daemon running under a system account can read it. `z-com-ai/` is already `.gitignore`'d (`*z-com-ai` in `.gitignore`) and currently contains one input file, `sqlite-db-path.txt` (content: `DB`).

### Decisions (confirmed with user 2026-08-12)
1. **Filename → env var mapping: pure mechanical transform.** No manifest file, no lookup table. Algorithm: strip `.txt` → split remaining filename on `-` → PascalCase each segment (uppercase first character, lowercase the rest) → join with `__` → prefix `NewAlbumsDiscovery__`.
   - Example: `sqlite-db-path.txt` → `NewAlbumsDiscovery__Sqlite__Db__Path`.
   - **This deviates from the literal example in `phase2-requirements.md` §2** (`NewAlbumsDiscovery__Database__Path`), which was not a mechanical transform of the filename and could only be reproduced via a manifest/lookup table. The user explicitly chose the mechanical-transform approach (true zero-touch extensibility — §5) over reproducing that exact example, so this section is the authoritative name for `sqlite-db-path.txt` going forward. `docs/specs/technical-specs.md` should be treated as needing a matching update in a later docs pass if it references `Database__Path` directly.
2. **Linux persistence target: `/etc/environment` + a systemd scaffold file.** The script upserts each variable into `/etc/environment` (read by PAM at login) **and** into `/etc/newalbumsdiscovery.env` (plain `KEY=value`, no quotes — forward-compatible with a future systemd unit's `EnvironmentFile=/etc/newalbumsdiscovery.env` directive). **Known gap, explicitly out of scope for this phase:** systemd-managed services do not inherit `/etc/environment` — wiring the actual daemon's unit file to read `/etc/newalbumsdiscovery.env` is deferred to the future deployment phase that creates the systemd unit.
3. **Session refresh: yes, best-effort.** After the Machine-level write, both scripts also apply the same values to their own process environment for immediate feedback/verification. Documented platform asymmetry (see Design Notes) rather than silently promising something that doesn't fully work:
   - **Windows:** running `.\scripts\setup-env.ps1` directly (not dot-sourced) executes in the *same* PowerShell process/session, and `$env:` writes are process-scoped — so the calling terminal sees the variable immediately with no special invocation needed.
   - **Linux:** running `./scripts/setup-env.sh` executes in a **child process**; `export` there is invisible to the parent shell unless the script is `source`d (`source scripts/setup-env.sh`). The script detects whether it was sourced and prints a warning with the correct invocation if not.
4. **No directory creation.** The script only resolves the path and persists the env var. Provisioning the actual folder (e.g. creating `%USERPROFILE%\DB`) is left to the application or the developer — out of scope here.

### As-Built Corrections (discovered during implementation and testing)
- **Real bug found and fixed in `setup-env.sh`: `return` inside a nested function cannot abort a sourced script.** The original design had a `fail()` helper that did `return 1` when `$SOURCED=1`. That `return` only unwinds `fail()`'s own call frame — it does **not** stop the sourced script itself, so a non-root + sourced run would print the error and then keep executing into privileged operations instead of stopping cleanly. Proven with an isolated repro (`source`d probe script continued past the "abort" point and printed an unreachable line) before being fixed. **Fix:** every abort/early-return point is now inlined directly at the script's own top level (not inside any helper function) — the `SOURCED`-conditional `return`/`exit` choice is repeated at each of the four exit points (root check, empty-directory, no-`.txt`-files, and the natural end) rather than centralized, because only a top-level `return` correctly unwinds a sourced script.
- **Second finding, fixed alongside the first: sourcing leaked `set -euo pipefail` into the caller's shell.** Since `source` merges the sourced script's shell-option changes into the caller, a developer following the documented `source scripts/setup-env.sh` advice would have `errexit`/`nounset`/`pipefail` silently turned on in their own interactive shell afterward — a real surprise for anyone not expecting it. **Fix:** the script now snapshots the caller's shell options (`set +o`) before applying its own `set -euo pipefail`, and restores them via a `restore_shell_opts` helper at every exit point (this one *is* safely centralized in a function, since restoring global shell-option state doesn't have the same top-level-only constraint that aborting a sourced script does).
- **Runtime-verified via WSL (Ubuntu), not just reviewed.** The original plan flagged the Linux script as "cannot be runtime-verified" in this Windows environment. WSL turned out to be available, so `setup-env.sh` was actually executed (as both a non-root and root user) rather than only reviewed: non-root+executed (blocked, exit 1, no writes), root+executed-not-sourced (persisted correctly, correct "not sourced" warning printed), root+sourced (persisted **and** immediately visible in that same shell via `$NewAlbumsDiscovery__Sqlite__Db__Path`), idempotent rerun (line count stayed at 1 in both `/etc/environment` and `/etc/newalbumsdiscovery.env`, no duplicates), and dynamic pickup of a second, temporary `.txt` file with zero script changes. All temporary test files/variables (Windows Machine scope and the WSL instance's `/etc/environment`/`/etc/newalbumsdiscovery.env`) were cleaned up afterward, except the real `NewAlbumsDiscovery__Sqlite__Db__Path` Machine value on this Windows machine, which the user chose to keep since it reflects real `z-com-ai/sqlite-db-path.txt` content.
- **`setup-env.ps1`'s elevated path required a live UAC prompt to verify**, which an automated tool cannot click through. The user approved a `Start-Process -Verb RunAs` prompt in the moment to allow real verification (elevated set, non-elevated block, dynamic file pickup, safe rerun) rather than settling for code review only.

### Design Notes (Solution-Architect-level implementation calls, not re-litigated with the user)
- **Elevation is a hard gate, not a soft warning.** Both scripts check privilege *before* touching any file/registry state:
  - Windows: `([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)`. If false, print instructions to re-run from an elevated PowerShell window and `exit 1` — no partial writes.
  - Linux: `[ "$(id -u)" -eq 0 ]`. If false, print instructions to re-run with `sudo` and `exit 1` — no partial writes.
- **Idempotent upsert, not blind append.** Rerunning either script (e.g. after editing a `.txt` file's content) must update the existing line for a given key rather than duplicating it — applies to `[Environment]::SetEnvironmentVariable` (naturally idempotent) and to both Linux env files (find-existing-key-and-replace, else append).
- **Path resolution uses proper path-combine APIs** (`Join-Path` / equivalent), not naive string concatenation, so trailing slashes or separator style in the `.txt` content don't break the result.
- **`z-com-ai/` location is resolved relative to the script's own location** (`$PSScriptRoot\..\z-com-ai` / `$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")/../z-com-ai`), not the caller's current working directory, so the scripts work regardless of where they're invoked from.
- **Assumption:** resolved paths do not contain the `|` character (used as the sed delimiter for the Linux upsert) or literal newlines. Spaces are fine. Not defended against exotic inputs — acceptable for this narrow scope.

### In Scope
- `scripts/setup-env.ps1` — Windows setup script per the above.
- `scripts/setup-env.sh` — Linux setup script per the above.
- Elevation/privilege checks in both, gating all writes.
- Dynamic enumeration of `z-com-ai/*.txt` (adding a new `.txt` file requires zero script changes, per §5 of the source spec).

### Out of Scope (future phases)
- Any systemd unit file / service installation (the `/etc/newalbumsdiscovery.env` scaffold is prepared for it, not consumed by it yet).
- Any change to `docs/specs/technical-specs.md`'s example env var names.
- Uninstall/rollback script (removing previously-set machine variables).
- Validating or creating the resolved directory.
- CI or automated testing of the scripts (they require elevated/root execution and machine-level state mutation, which isn't practical to sandbox in this phase).

### Acceptance Criteria — all verified ✅
- Running `scripts/setup-env.ps1` from an elevated PowerShell window sets `NewAlbumsDiscovery__Sqlite__Db__Path` at Machine scope to `<USERPROFILE>\DB`, visible via `[Environment]::GetEnvironmentVariable(..., "Machine")` and in the same terminal via `$env:NewAlbumsDiscovery__Sqlite__Db__Path`. **Verified** on this machine via a user-approved elevated relaunch.
- Running `scripts/setup-env.ps1` from a non-elevated window prints a clear instruction and exits non-zero without setting anything. **Verified.**
- Running `source scripts/setup-env.sh` as root updates `/etc/environment` and `/etc/newalbumsdiscovery.env` with `NewAlbumsDiscovery__Sqlite__Db__Path=<$HOME>/DB` and exports it into the current shell. **Verified** via WSL (Ubuntu).
- Running `scripts/setup-env.sh` (not sourced, or not root) prints the appropriate corrective instruction and exits non-zero without partial writes. **Verified** via WSL, including the non-root+sourced combination that surfaced the `return`-in-nested-function bug described above — re-verified clean after the fix.
- Rerunning either script after changing `z-com-ai/sqlite-db-path.txt`'s content updates the existing variable/line in place — no duplicate entries in `/etc/environment` or `/etc/newalbumsdiscovery.env`. **Verified** on both platforms (Windows: inherently idempotent via `SetEnvironmentVariable`; Linux: line count confirmed to stay at 1 after rerun).
- Adding a second `.txt` file (e.g. `gemini-api-key.txt`) to `z-com-ai/` and rerunning either script picks it up automatically as `NewAlbumsDiscovery__Gemini__Api__Key`, with no script edits. **Verified** on both platforms; temporary file and variables removed afterward.

### Implementation Record
- Plan: [`docs/plans/phase2-env-setup-scripts-plan.md`](../plans/phase2-env-setup-scripts-plan.md) — all steps checked off, status marked complete.

## Phase 2.1: Environment Variable Rename — Read-Only Database Path — 📋 PLANNED (2026-08-12)

**Source:** Architecture update in `docs/requirements/high-level-requirements.md` and `docs/specs/technical-specs.md` (dual-database architecture), which renamed the read-only database's env var from the Phase 2-era `NewAlbumsDiscovery__Sqlite__Db__Path` to `NewAlbumsDiscovery__Database__LovedTracksDbPath`. Finalized here after clarification.

### Goal
Update the Phase 2 developer setup scripts (`scripts/setup-env.ps1`, `scripts/setup-env.sh`) so the read-only loved-tracks database path is published under its new name, `NewAlbumsDiscovery__Database__LovedTracksDbPath`, with a value that resolves to the full database file path (not just a folder) — matching the dual-database architecture now specified in `docs/specs/technical-specs.md` §3. Also clean up the now-obsolete `NewAlbumsDiscovery__Sqlite__Db__Path` Machine-level variable created by the original Phase 2 testing.

### Decisions (confirmed with user 2026-08-12)
1. **Mechanical-transform algorithm extended, not abandoned.** Hyphen (`-`) keeps its Phase 2 meaning: it starts a new `__`-joined top-level segment. New rule added: an underscore (`_`) within a segment marks a *compound word* — each `_`-separated sub-word is PascalCased and concatenated with **no** separator between them (separators only go between top-level hyphen segments). Example: `database-loved_tracks_db_path.txt` → `Database` + `__` + `LovedTracksDbPath` → `NewAlbumsDiscovery__Database__LovedTracksDbPath`. Filenames with no underscores behave exactly as before Phase 2.1 — fully backward compatible.
2. **File content now includes the database filename, not just the folder.** Per `docs/specs/technical-specs.md` §2.A (`.../DB/loved-tracks.db`), the source `.txt` file's content changes from `DB` to `DB/loved-tracks.db` — a full path relative to the user's home directory. **Convention: file content always uses forward slashes (`/`) as the separator, regardless of target OS** — both `Join-Path` (PowerShell) and the existing string-concatenation join (Bash) handle `/` correctly; a literal backslash would silently produce a garbage path on Linux.
3. **`z-com-ai/loved-tracks-db-path.txt` (already renamed and content-updated by the user ahead of this plan) will be renamed again, to `z-com-ai/database-loved_tracks_db_path.txt`.** The current on-disk filename is hyphen-only (no `database` segment, no underscore) and does not encode the new compound-word convention — mechanically it would still produce `NewAlbumsDiscovery__Loved__Tracks__Db__Path`, not the approved target `NewAlbumsDiscovery__Database__LovedTracksDbPath`. Content (`DB/loved-tracks.db`) is already correct and stays unchanged.
4. **Old variable is actively cleaned up, not just left stale.** Both scripts gain a small hardcoded "deprecated variable names" list (currently just `NewAlbumsDiscovery__Sqlite__Db__Path`) and remove each one every time the script runs — Machine scope + current session on Windows; `/etc/environment` + `/etc/newalbumsdiscovery.env` lines + current-session `unset` (if sourced) on Linux. Idempotent: running against an already-cleaned machine is a silent no-op for that variable.

### Design Notes
- No change to the elevation/root gating, idempotent-upsert-for-new-variables logic, or sourced-shell handling from Phase 2 — reused as-is.
- The second new variable implied by the updated architecture, `NewAlbumsDiscovery__Database__AppDbPath` (the app's own read-write database, under `<AppBaseDirectory>/Database/`, not the user's home directory), is **out of scope** here — it isn't a home-relative path sourced from a `z-com-ai/*.txt` file the way `LovedTracksDbPath` is, so it doesn't fit this scripts' model. It will be addressed separately.
- `docs/requirements/phase2-requirements.md` remains untouched (Antigravity-owned source spec, per `.agents/rules/multi-agent-governance.md`) — only this file reflects the update.

### In Scope
- Rename `z-com-ai/loved-tracks-db-path.txt` → `z-com-ai/database-loved_tracks_db_path.txt` (content unchanged: `DB/loved-tracks.db`).
- Extend `ConvertTo-EnvVarName` (`setup-env.ps1`) and `to_env_var_name` (`setup-env.sh`) with the underscore-compound-word rule.
- Add deprecated-variable cleanup (`NewAlbumsDiscovery__Sqlite__Db__Path`) to both scripts.
- Update both scripts' header-comment documentation of the naming algorithm.

### Out of Scope
- `NewAlbumsDiscovery__Database__AppDbPath` and any other new variables implied by the updated architecture (Gemini, Telegram, etc.) — not part of this narrow rename task.
- Any application code that actually opens/reads either database (Phase 3+ concern).
- Updating `docs/requirements/phase2-requirements.md`.

### Acceptance Criteria
- Given `z-com-ai/database-loved_tracks_db_path.txt` (content `DB/loved-tracks.db`), running the elevated/root script sets `NewAlbumsDiscovery__Database__LovedTracksDbPath` = `<home>/DB/loved-tracks.db`.
- A filename with no underscores (e.g. a synthetic `gemini-api-key.txt`) still produces the old-style flat name (`NewAlbumsDiscovery__Gemini__Api__Key`) — regression check confirming the extension doesn't break Phase 2 behavior.
- The stale `NewAlbumsDiscovery__Sqlite__Db__Path` variable is removed (Windows: Machine scope + current session; Linux: `/etc/environment`, `/etc/newalbumsdiscovery.env`, current session if sourced) after running either script.
- Rerunning either script a second time is idempotent: no duplicate lines, and the already-removed old variable doesn't reappear or error.

### Implementation Record
- Plan: [`docs/plans/phase2.1-env-var-rename-plan.md`](../plans/phase2.1-env-var-rename-plan.md)

## Phase 3: Music Preference Aggregator Engine & Storage — ✅ COMPLETE (2026-08-12)

**Source:** `docs/requirements/phase3-requirements.md` (Antigravity-owned input spec — see `.agents/rules/multi-agent-governance.md`), finalized here after clarification. This is the first phase to add real feature code on top of the Phase 1 skeleton — no EF Core, `DbContext`, or repository yet exists in the solution.

### Goal
Implement Feature 1 (Music Preference Aggregator): read all `LovedTracks` from the external read-only `loved-tracks.db`, run a pure, deterministic 3-level cascading-threshold aggregation in memory, and atomically replace the `AggregatedBuckets` table in the internal `new-albums-discovery.db` — end-to-end and observable (the Worker actually runs it), not just scaffolded.

### Decisions (confirmed with user 2026-08-12)
1. **Domain purity for Id/timestamp generation:** `BucketAggregatorEngine.Aggregate(...)` takes `DateTime asOfUtc` as an explicit parameter (supplied by the caller) rather than reading a wall clock internally. Each `AggregatedBucket` generates its own `Guid` Id internally via `Guid.NewGuid()` — this doesn't hurt determinism because tests assert on `BucketName`/`BucketType`/`Country`/`Language`/`Genre`/`TrackCount`, never on the Id. `asOfUtc` is sourced by the Application-layer command handler from an injected `TimeProvider` (`TimeProvider.System` in production, `FakeTimeProvider`/stub in tests) — never `DateTime.UtcNow` called directly inside Domain or Application code.
2. **Malformed/missing source data — fallback, never skip or throw.** Null/empty `CountryOrRegion` is normalized to the literal value `"Unknown"`. Null or invalid-JSON `LanguagesJson`/`GenresJson` is normalized to an empty list (contributes zero to language/genre counts for that track). **This normalization happens at the Infrastructure boundary** (`LovedTrackRepository`, mapping raw external rows into the Domain-layer `LovedTrackPreferences` value object) — the value object's constructor then enforces "Country is never null/empty, Languages/Genres are never null" as a Domain invariant, so `BucketAggregatorEngine` itself never needs a null/malformed-data branch. This keeps the engine's 100%-branch-coverage surface limited to genuine aggregation logic, per `docs/constitution/tdd.md` §3 (infrastructure isolation) — the messy boundary is isolated in Infrastructure, tested there.
3. **EF Core migrations: full, applied at startup.** Phase 3 runs `dotnet ef migrations add InitialCreate` for `AppDbContext` (committed to the repo) and calls `Database.Migrate()` once during Worker startup (in `Program.cs`, before `host.Run()`), so `new-albums-discovery.db` and its `AggregatedBuckets` table are created automatically on first run. `LovedTrackDbContext` (the external, read-only database) gets **no** migrations — this app never changes its schema.
4. **Worker wiring: runs once at startup.** A new `AggregationStartupWorker : BackgroundService` sends `AggregateMusicPreferencesCommand` via MediatR once when the Worker starts, on a background thread (`Task.Run`, honoring the host's `CancellationToken` per `phase3-requirements.md` §2), so the full pipeline is observable end-to-end on every run. The existing `HeartbeatWorker` is untouched and keeps running alongside it. Real periodic/monthly scheduling is deferred to a later orchestration phase.

### Design Notes (Solution-Architect-level implementation calls, not re-litigated with the user)
- **Grouping key at Level 2/3 is composite, not global.** Level 2 groups by `(Country, Language)` — not by `Language` alone across all cascading countries — and Level 3 by `(Country, Language, Genre)`. This is implied by the DDL (every bucket has exactly one non-null `Country`) and the worked examples (`"Netherlands/Swahili"` keeps `Country = "Netherlands"`); grouping by `Language` alone would merge tracks from different cascading countries into one bucket with an ambiguous `Country`, which the schema doesn't allow.
- **Threshold values cross the Domain/Application boundary as a Domain value object, not `AggregatorSettings` directly.** `AggregatorSettings` (the `IOptions<T>`-bound class) lives in Application per `phase3-requirements.md` §5 and must not be referenced by Domain (Domain depends on nothing, including Application — the dependency only flows the other way). A new Domain-layer `AggregationThresholds` value object (`CountryRegionThreshold`, `CountryRegionLanguageThreshold`, `MinimumBucketThreshold`) is the actual engine parameter; the command handler maps `AggregatorSettings` → `AggregationThresholds` before calling the engine.
- **`LovedTrackDbContext` maps `LovedTracks` as a keyless entity type (`HasNoKey()`).** The real external schema's primary key column/type is unknown (no sample database or column list beyond `CountryOrRegion`/`LanguagesJson`/`GenresJson` exists in this repo). Since access is strictly read-only and we only ever project the three known columns, treating it as a keyless read-only view avoids guessing a PK that could be wrong. **Flagged for correction** if the real schema turns out to require identity tracking.
- **`NewAlbumsDiscovery__Database__AppDbPath` gets a code-level default, no setup-script entry.** Unlike `LovedTracksDbPath` (which varies per developer's home directory and is why Phase 2/2.1 scripts exist), the app DB path is always `<AppBaseDirectory>/Database/new-albums-discovery.db` — mechanically derivable at runtime via `AppContext.BaseDirectory`. Infrastructure DI registration uses the env var/config value if present (for override flexibility, matching `technical-specs.md` §3's documented key), otherwise falls back to the computed default. The app itself creates the `Database` subfolder if missing (nothing else provisions it); the external `loved-tracks.db` path is never created by this app if missing — that's a hard failure (it's owned by another product).
- **EF Core maps directly to the Domain `AggregatedBucket` entity** (private setters + an EF-only constructor, `BucketType` enum stored as its string name via `HasConversion<string>()`) rather than introducing a separate Infrastructure-only persistence model — the entity is simple enough that a 1:1 mapping doesn't strain `coding-principles.md` §3's "no complex mapping" KISS guidance. `Infrastructure` referencing `Domain` types is expected/required (only `Domain` itself must stay dependency-free).
- **Infrastructure repository tests hit real SQLite** (temp file per test, not mocked) — this is the isolation boundary itself, per `tdd.md` §3; mocking is for the layers that *consume* the repository interfaces (Application), not for testing the repository implementation.
- **100% branch coverage applies to `Domain` and `Application`** (`BucketAggregatorEngine`, `AggregationThresholds`, `LovedTrackPreferences`, `AggregateMusicPreferencesCommandHandler`) per `tdd.md` §2. `Infrastructure` (`LovedTrackRepository`, `AggregatedBucketRepository`, both `DbContext`s) is exercised via integration-style tests against real SQLite but isn't held to the same strict branch-coverage gate — file-system/ADO.NET failure paths there are harder to force deterministically and are lower-value to chase exhaustively.

### In Scope
- **Domain** (`src/NewAlbumsDiscovery.Domain/MusicAggregator/`): `AggregatedBucket` entity, `BucketType` enum, `LovedTrackPreferences` value object (with constructor invariants), `AggregationThresholds` value object, `BucketAggregatorEngine` pure domain service implementing the full 3-level cascade + minimum-threshold filtering.
- **Application** (`src/NewAlbumsDiscovery.Application/MusicAggregator/`): `AggregatorSettings`, `ILovedTrackRepository`, `IAggregatedBucketRepository`, `AggregateMusicPreferencesCommand` + handler (MediatR), registration of `AggregatorSettings` via `.Configure<AggregatorSettings>(...)` and `BucketAggregatorEngine` as a singleton in `AddApplicationServices()`.
- **Infrastructure** (`src/NewAlbumsDiscovery.Infrastructure/Sqlite/`): `LovedTrackDbContext` (keyless, read-only), `LovedTrackRepository`, `AppDbContext` (`AggregatedBuckets` `DbSet`, EF Core Migrations), `AggregatedBucketRepository` (atomic delete+insert transaction), DI registration of both `DbContext`s and repositories in `AddInfrastructureServices()`, initial `AppDbContext` migration.
- **Worker**: `AggregationStartupWorker` hosted service; `Database.Migrate()` call in `Program.cs` startup; `NewAlbumsDiscovery:Aggregator` section added to `appsettings.json`/`appsettings.Development.json` with the documented defaults (10, 10, 2).
- **Tests**: `BucketAggregatorEngineTests` (100% branch coverage per `phase3-requirements.md` §8's boundary/multi-value/threshold/empty-dataset cases), `AggregateMusicPreferencesCommandHandlerTests` (mocked repositories + fake `TimeProvider`), `LovedTrackRepositoryTests` and `AggregatedBucketRepositoryTests` (real temp-file SQLite).

### Out of Scope (future phases)
- Feature 2 (AI Discovery / Gemini) and Feature 3 (Notification / Telegram) — including their `DiscoveredAlbums` table, which will very likely land in a **separate** later EF Core migration for `AppDbContext`.
- Real periodic/monthly scheduling or orchestration across all three features — `AggregationStartupWorker` only runs once per process start.
- Any setup-script (`scripts/setup-env.*`) changes for `NewAlbumsDiscovery__Database__AppDbPath` — it uses a code-level default instead (see Design Notes).
- CI pipeline / automated coverage-gate enforcement (tracked separately, not part of this phase).

### As-Built Corrections (discovered during implementation)
- **EF Core constructor binding needs every mapped property explicitly touched in `OnModelCreating`, not just the ones needing special configuration.** Only calling `HasKey`, `.HasConversion<string>()` on `BucketType`, and `.IsRequired()` on `CreatedAtUtc` left `BucketName`/`Country`/`Language`/`Genre`/`TrackCount` unrecognized as mapped properties — `dotnet ef migrations add` failed with "No suitable constructor was found... Cannot bind 'bucketName', 'country', 'language', 'genre', 'trackCount'". **Fix:** every constructor-bound property on `AggregatedBucket` now gets its own explicit `entity.Property(...)` call.
- **A design-time `IDesignTimeDbContextFactory<AppDbContext>` was required** (`NewAlbumsDiscovery.Infrastructure/Sqlite/AppDbContextFactory.cs`, internal, placeholder connection string). `dotnet ef migrations add` tries to build the real Worker DI container to resolve `AppDbContext`, which fails because `AddInfrastructureServices` eagerly validates `NewAlbumsDiscovery__Database__LovedTracksDbPath` — an env var that isn't set at design time on a fresh dev machine.
- **The EF tools' startup project (`NewAlbumsDiscovery.Worker`) also needed its own `Microsoft.EntityFrameworkCore.Design` package reference**, not just the Infrastructure project being migrated.
- **Two transitive NuGet vulnerabilities pinned above what EF Core 10.0.0 pulls by default**, both dev/build-time only: `SQLitePCLRaw.bundle_e_sqlite3` 2.1.11 → 2.1.12 (CVE-2025-6965) and `System.Security.Cryptography.Xml` 9.0.0 → 10.0.11 (several known CVEs, via `Microsoft.EntityFrameworkCore.Design`). Pinned in `Directory.Packages.props`.
- **Local `dotnet-ef` tool manifest lives at `.config/dotnet-tools.json`** (the conventional location), not the repo root where `dotnet new tool-manifest` initially placed it.
- **`LovedTrackDbContext`'s connection string uses SQLite's `Mode=ReadOnly`** in addition to C#-level read-only repository code, enforcing the "strictly read-only" constraint at the driver level too.

### Acceptance Criteria — all verified ✅
- `dotnet build` succeeds; `dotnet test` passes with 100% branch coverage on the `Domain` and `Application` `MusicAggregator` code. **Verified** — 64/64 tests passing across all four projects, coverage report confirms `line-rate="1" branch-rate="1"` on `AggregatedBucket`, `AggregationThresholds`, `BucketType`-backed logic, `BucketAggregatorEngine`, `LovedTrackPreferences`, and `AggregateMusicPreferencesCommandHandler`.
- Given a synthetic `loved-tracks.db` with tracks crossing every documented boundary (country count 9 vs 10, country/language count 9 vs 10, multi-language/multi-genre expansion, bucket count 1 vs 2), running `AggregateMusicPreferencesCommand` produces exactly the buckets `phase3-requirements.md` §8 describes. **Verified** via `BucketAggregatorEngineTests` (12 targeted boundary/expansion/threshold tests, all passing).
- Running the Worker end-to-end against a real (small, local) `loved-tracks.db` creates `new-albums-discovery.db` with a populated, correctly-filtered `AggregatedBuckets` table, and a second run **replaces** (not duplicates/appends) the table contents atomically. Replace-not-duplicate behavior **verified automatically** via `AggregatedBucketRepositoryTests` against real temp-file SQLite; the full end-to-end Worker run against a real `loved-tracks.db` **verified by the user's own manual testing** (2026-08-12, `/complete Phase 3`).
- A `LovedTracks` row with null `CountryOrRegion` still produces a bucket under `"Unknown"` rather than being dropped or crashing the run. **Verified** via `LovedTrackRepositoryTests.GetAllAsync_WithNullCountry_NormalizesToUnknown`.
- Deleting/renaming the external `loved-tracks.db` before a run causes a clear, loud failure (not a silently-empty aggregation). **Verified** — `AddInfrastructureServices` throws a descriptive `InvalidOperationException` before any `DbContext` is even constructed if the configured path doesn't exist.

### Implementation Record
- Plan: [`docs/plans/phase3-aggregator-engine-plan.md`](../plans/phase3-aggregator-engine-plan.md) — all steps checked off, status marked complete.
