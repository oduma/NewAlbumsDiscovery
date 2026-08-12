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
