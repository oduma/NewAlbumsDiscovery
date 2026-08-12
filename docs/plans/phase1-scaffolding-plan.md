# Phase 1 Implementation Plan: Cross-Platform Application Scaffolding

**Requirements source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 1
**Status:** ✅ DONE (2026-08-12) — all steps complete and verified (`dotnet build`, `dotnet test`, `dotnet run` all pass). Corrections applied during implementation are noted inline in Step 5 and folded back into `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` under "As-Built Corrections".

## Design Decisions Made By This Plan
These are implementation-detail calls within the Solution Architect's remit, not re-litigated with the user:
- **Logging:** `Microsoft.Extensions.Logging` console provider (built into the Generic Host). No Serilog/third-party sink in Phase 1 — keeps footprint minimal (KISS); can be swapped later behind the same `ILogger<T>` abstraction without touching call sites.
- **Mediator package:** `MediatR` (matches `coding-principles.md` §1's explicit example), registered in `Application` via `AddMediatR()`, scanning the Application assembly. No handlers yet.
- **Mocking library:** `Moq` (first example listed in `tdd.md` §3), added to test projects now so Phase 2 can start writing tests immediately without a packaging detour.
- **Central package management:** `Directory.Packages.props` (NuGet CPM) so every `.csproj` references packages without inline versions — avoids version drift across 7 projects (DRY, per `coding-principles.md` §4).

## Step-by-Step Plan

### Step 1 — Repo-level MSBuild scaffolding
- [x] Create `Directory.Build.props` at repo root: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `RootNamespace`/`AssemblyName` prefix convention.
- [x] Create `Directory.Packages.props` at repo root with `ManagePackageVersionsCentrally=true` and version entries for: `MediatR`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.Extensions.Hosting.Systemd`, `xunit`, `xunit.runner.visualstudio`, `Moq`, `Microsoft.NET.Test.Sdk`.
- [x] Create solution file at repo root (empty, projects added as they're created in later steps). Note: `dotnet new sln` on the .NET 10 SDK generated `NewAlbumsDiscovery.slnx` (the new XML solution format) rather than the classic `.sln` — kept as-is since it's the SDK's current default and `dotnet sln add`/`dotnet build`/`dotnet test` all work against it transparently.

### Step 2 — Domain project (`src/NewAlbumsDiscovery.Domain`)
- [x] `dotnet new classlib` targeting the shared TFM via `Directory.Build.props`.
- [x] Delete the template's default `Class1.cs`.
- [x] Create empty namespace folders: `MusicAggregator/`, `AIDiscovery/`, `Notification/` (each with a `.gitkeep` so Git tracks the empty folder, since no entities exist yet).
- [x] Verify **zero** `<PackageReference>` entries — enforces the Zero-Dependency Rule from `DDD-architecture.md` §1.
- [x] Add to solution.

### Step 3 — Application project (`src/NewAlbumsDiscovery.Application`)
- [x] `dotnet new classlib`; project reference → `Domain`.
- [x] Add `MediatR` package reference (version from CPM).
- [x] Namespace folders: `MusicAggregator/`, `AIDiscovery/`, `Notification/`, plus `Common/` for cross-context DI registration.
- [x] `Common/ApplicationServiceCollectionExtensions.cs` — a single `AddApplicationServices(this IServiceCollection)` extension method that calls `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly))`. This is the only "logic" in Phase 1's Application layer.
- [x] Add to solution.

### Step 4 — Infrastructure project (`src/NewAlbumsDiscovery.Infrastructure`)
- [x] `dotnet new classlib`; project references → `Application`, `Domain`.
- [x] Empty namespace folders (with `.gitkeep`): `Sqlite/`, `Gemini/`, `Telegram/` — reserved for Phase 2+ implementations of interfaces that will be defined in `Application`.
- [x] `InfrastructureServiceCollectionExtensions.cs` at project root — `AddInfrastructureServices(this IServiceCollection)`, currently a no-op stub (`return services;`) so `Worker` has a stable call site to wire up before any real infrastructure exists.
- [x] Add to solution.

### Step 5 — Worker host project (`src/NewAlbumsDiscovery.Worker`)
- [x] `dotnet new worker` (gives a `BackgroundService` template) targeting net10.0; project references → `Application`, `Infrastructure`, `Domain`.
- [x] Add packages: `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.Extensions.Hosting.Systemd`.
- [x] `Program.cs`: `Host.CreateDefaultBuilder(args)` → `.UseWindowsService().UseSystemd()` → `.ConfigureServices(services => services.AddApplicationServices().AddInfrastructureServices().AddHostedService<HeartbeatWorker>())` → `.Build().Run()`.
  - Correction from the original plan: `Domain` does **not** get an `AddDomainServices()` extension. Doing so would require a `Microsoft.Extensions.DependencyInjection` package reference in `Domain`, violating the Zero-Dependency Rule (`DDD-architecture.md` §1). `Domain` stays out of the DI wiring chain entirely until it has something to register.
  - Correction: `HostApplicationBuilder` (from `Host.CreateApplicationBuilder`) has no `.Host`/`.UseWindowsService()`/`.UseSystemd()` surface in this SDK — those extensions require `IHostBuilder`, so the plan switched to `Host.CreateDefaultBuilder(args)`, which returns `IHostBuilder` and supports them directly.
- [x] Rename the template's `Worker.cs` to `HeartbeatWorker.cs` — `ExecuteAsync` logs an informational heartbeat on a configurable interval (default 60s, read via `IConfiguration`) until cancellation. This exists purely to prove the host runs on both platforms; it is replaced by the real orchestrator in a later phase.
- [x] `appsettings.json` + `appsettings.Development.json` with a `Logging` section (default levels) — no feature-specific config sections yet.
- [x] Confirm `NewAlbumsDiscovery__*` double-underscore env vars would bind correctly by inspection (Generic Host's environment variable provider is on by default) — no code changes needed to prove this, just documented in a short comment in `Program.cs`.
- [x] Add to solution.

### Step 6 — Test projects
- [x] `tests/NewAlbumsDiscovery.Domain.Tests` — `dotnet new xunit`; project reference → `Domain`; package reference → `Moq`. Delete template's sample test file. No test files added (nothing to test yet).
- [x] `tests/NewAlbumsDiscovery.Application.Tests` — same pattern; project reference → `Application`.
- [x] `tests/NewAlbumsDiscovery.Infrastructure.Tests` — same pattern; project reference → `Infrastructure`.
- [x] Add all three to solution, under a `tests` solution folder (mirroring the `src` solution folder for the four projects above).

### Step 7 — Build & run verification
- [x] `dotnet restore` from repo root.
- [x] `dotnet build` — must succeed with zero warnings introduced by the scaffolding itself.
- [x] `dotnet test` — must succeed (0 tests collected is expected and acceptable).
- [x] `dotnet run --project src/NewAlbumsDiscovery.Worker` — confirm startup log line and at least one heartbeat log line appear, then Ctrl+C confirms clean shutdown.
- [x] Spot-check `Domain.csproj` has no `<PackageReference>` nodes.

### Step 8 — Wrap-up
- [x] Review final tree structure against `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` Phase 1 acceptance criteria; check each one off.
- [x] Report back to the user with the final directory tree and confirmation that all acceptance criteria pass, before considering Phase 1 complete.

## Final Directory Tree (target state)
```
NewAlbumsDiscovery.slnx
global.json
Directory.Build.props
Directory.Packages.props
src/
  NewAlbumsDiscovery.Domain/
    MusicAggregator/.gitkeep
    AIDiscovery/.gitkeep
    Notification/.gitkeep
  NewAlbumsDiscovery.Application/
    MusicAggregator/.gitkeep
    AIDiscovery/.gitkeep
    Notification/.gitkeep
    Common/ApplicationServiceCollectionExtensions.cs
  NewAlbumsDiscovery.Infrastructure/
    Sqlite/.gitkeep
    Gemini/.gitkeep
    Telegram/.gitkeep
    InfrastructureServiceCollectionExtensions.cs
  NewAlbumsDiscovery.Worker/
    Program.cs
    HeartbeatWorker.cs
    appsettings.json
    appsettings.Development.json
tests/
  NewAlbumsDiscovery.Domain.Tests/
  NewAlbumsDiscovery.Application.Tests/
  NewAlbumsDiscovery.Infrastructure.Tests/
```

## Explicitly Not Done In This Phase
- No SQLite/Gemini/Telegram packages or code.
- No domain entities, value objects, or aggregates.
- No CI workflow.
- No test files (only test project scaffolding).
