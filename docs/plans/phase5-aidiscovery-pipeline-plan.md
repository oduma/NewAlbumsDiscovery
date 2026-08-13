# Phase 5 Implementation Plan: AIDiscovery Scaffolding & Bucket Processing Pipeline

**Status:** ✅ COMPLETE (2026-08-13) — all steps below implemented and automated-tested; Step 6's live Worker run was not performed (see that step and FUNCTIONAL_REQUIREMENTS.md → Phase 5 → Acceptance Criteria for the explicit caveat).
**Requirements:** [`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`](../requirements/FUNCTIONAL_REQUIREMENTS.md) → Phase 5

Strict TDD (`docs/constitution/tdd.md`): every step below writes a failing test before the implementation that makes it pass. No Domain layer changes this phase, so 100% branch coverage applies to every new `Application` class. `ConsoleDiscoveryNotifier` (Infrastructure) is ordinary orchestration logic over `Console.Out` — held to normal coverage via output-capture tests, same treatment `GeminiDiscoveryClient` got in Phase 4 (not treated as an exempted SDK-wrapper edge).

---

## Step 0 — Asset relocation (no C# yet) ✅

1. Copy `docs/specs/assets/prompts/country-genres-prompt.md` and `docs/specs/assets/prompts/genre-expansion-prompt.md` into `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/` (alongside the three already copied in Phase 4). Source files stay untouched (Antigravity-owned).
2. Create `src/NewAlbumsDiscovery.Infrastructure/Gemini/Assets/` and copy `docs/specs/assets/countries-languages.json` into it.
3. Extend `NewAlbumsDiscovery.Infrastructure.csproj`'s embedded-resource item group to also include the new `Assets\*.json` glob:
   ```xml
   <ItemGroup>
     <EmbeddedResource Include="Gemini\Prompts\*.md" />
     <EmbeddedResource Include="Gemini\Assets\*.json" />
   </ItemGroup>
   ```
4. **Test-first:** `PromptAssetsTests` (Infrastructure) — loads `NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.country-genres-prompt.md`, `...genre-expansion-prompt.md`, and `NewAlbumsDiscovery.Infrastructure.Gemini.Assets.countries-languages.json` via `Assembly.GetManifestResourceStream` (same reflection helper `GeminiDiscoveryClient` already uses — extract/reuse it if convenient, or a small local helper), asserts each stream is non-null and its content is non-empty; the JSON asset additionally round-trips through `JsonDocument.Parse` without throwing. This is a pure wiring sanity check — no business logic consumes these files yet.

## Step 1 — Application: AIDiscovery pipeline plumbing (`src/NewAlbumsDiscovery.Application/AIDiscovery/Pipeline/`) ✅

1. `AIDiscoveryOptions` — `InterBucketDelaySeconds` (default `10`). No test needed (plain data holder, same as `GeminiOptions`).
2. `AIDiscoveryPipelineContext` — immutable `record AIDiscoveryPipelineContext(IReadOnlyList<AggregatedBucket> SortedBuckets, int ProcessedBucketCount = 0)`.
3. `IDiscoveryNotifier` — port:
   ```csharp
   Task NotifyPipelineStartingAsync(int bucketCount, CancellationToken cancellationToken);
   Task NotifyBucketProcessedAsync(string bucketName, int trackCount, CancellationToken cancellationToken);
   Task NotifyPipelineCompletedAsync(int processedBucketCount, CancellationToken cancellationToken);
   ```
4. `IAIDiscoveryStage` — `Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken);`
5. `IBucketProcessingStep` — `Task ProcessAsync(AggregatedBucket bucket, CancellationToken cancellationToken);`
6. **Test-first:** `StartNotificationStageTests` — given a context with N sorted buckets, `ExecuteAsync` calls `IDiscoveryNotifier.NotifyPipelineStartingAsync(N, ct)` exactly once and returns the same context unchanged (including the zero-bucket case).
   **Implement:** `StartNotificationStage : IAIDiscoveryStage`.
7. **Test-first:** `PrintBucketStepTests` — `ProcessAsync(bucket, ct)` calls `IDiscoveryNotifier.NotifyBucketProcessedAsync(bucket.BucketName, bucket.TrackCount, ct)` exactly once.
   **Implement:** `PrintBucketStep : IBucketProcessingStep`.
8. **Test-first:** `BucketProcessingStageTests` (mock `IEnumerable<IBucketProcessingStep>`, `IOptions<AIDiscoveryOptions>`, and a `RecordingTimeProvider` mirroring Phase 4's pattern):
   - Zero buckets → no step calls, no delay calls, returned context has `ProcessedBucketCount == 0`.
   - One bucket → the step(s) run exactly once for that bucket, **no delay** is scheduled (nothing follows the only/last bucket), `ProcessedBucketCount == 1`.
   - Two-plus buckets → delay is scheduled exactly `count - 1` times, each for `InterBucketDelaySeconds`, verified via the fake `TimeProvider`; the delay is never scheduled after the final bucket.
   - Multiple registered steps → for a single bucket, all steps run in registration order; across buckets, all steps for bucket *N* complete before any step for bucket *N+1* starts (assert via a shared call-order list in the test).
   - Returned context preserves `SortedBuckets` and reflects the final `ProcessedBucketCount`.
   **Implement:** `BucketProcessingStage : IAIDiscoveryStage`.
9. **Test-first:** `ReportPublicationStageTests` — `ExecuteAsync` calls `IDiscoveryNotifier.NotifyPipelineCompletedAsync(context.ProcessedBucketCount, ct)` exactly once, returns the context unchanged.
   **Implement:** `ReportPublicationStage : IAIDiscoveryStage`.
10. `RunAIDiscoveryPipelineCommand : IRequest` (no params).
11. **Test-first:** `RunAIDiscoveryPipelineCommandHandlerTests` (mock `IAggregatedBucketRepository` and `IEnumerable<IAIDiscoveryStage>`):
    - Buckets returned out of `TrackCount` order → the initial context passed to the first stage has them sorted descending.
    - Three mocked stages → all three run in the exact order provided by the `IEnumerable`, each receiving the context returned by the previous one (verify via a stage sequence that mutates/replaces the context and asserting the final call received the prior stage's output).
    - Zero buckets → all stages still run once each, with an empty `SortedBuckets` list.
    **Implement:** `RunAIDiscoveryPipelineCommandHandler` — fetches buckets, sorts by `TrackCount` descending, builds the initial context, folds it through the ordered stages.
12. Register in `ApplicationServiceCollectionExtensions.AddApplicationServices`:
    ```csharp
    services.Configure<AIDiscoveryOptions>(configuration.GetSection("NewAlbumsDiscovery:AIDiscovery"));
    services.AddScoped<IAIDiscoveryStage, StartNotificationStage>();
    services.AddScoped<IAIDiscoveryStage, BucketProcessingStage>();
    services.AddScoped<IAIDiscoveryStage, ReportPublicationStage>();
    services.AddScoped<IBucketProcessingStep, PrintBucketStep>();
    ```
    Registration order is load-bearing (`IEnumerable<T>` resolves in registration order) — comment this fact directly above the three `IAIDiscoveryStage` lines so it can't be silently reordered later.

## Step 2 — Application: CoreOperations orchestration (`src/NewAlbumsDiscovery.Application/CoreOperations/`) ✅

**As-built note:** required one small fix not in the original plan — a missing `using NewAlbumsDiscovery.Application.CoreOperations;` in `ApplicationServiceCollectionExtensions.cs`, caught by the first `dotnet test` run and fixed immediately. No other deviation.

1. `CoreOperationsOptions` — `TriggerDelaySeconds` (default `30`). No test needed (plain data holder).
2. `RunOrchestrationCommand : IRequest` (no params).
3. **Test-first:** `RunOrchestrationCommandHandlerTests` (mock `ISender`, `IOptions<CoreOperationsOptions>`, `RecordingTimeProvider`):
   - Happy path: `ISender.Send` is called first with an `AggregateMusicPreferencesCommand`, then a delay of `TriggerDelaySeconds` is observed via the fake `TimeProvider`, then `ISender.Send` is called with a `RunAIDiscoveryPipelineCommand` — assert both the exact sequence and that no extra sends happen.
   - If the first `Send` (aggregation) throws, the delay is never scheduled and the pipeline command is never sent — the exception propagates out of `Handle` unchanged (`Assert.ThrowsAsync`, then `Verify(... RunAIDiscoveryPipelineCommand ..., Times.Never)`).
   **Implement:** `RunOrchestrationCommandHandler` — sequential `await`s, no try/catch (natural short-circuit on exception satisfies "AIDiscovery starts only after aggregation *successfully* finishes").
4. Register in `ApplicationServiceCollectionExtensions.AddApplicationServices`:
   ```csharp
   services.Configure<CoreOperationsOptions>(configuration.GetSection("NewAlbumsDiscovery:CoreOperations"));
   ```
   (`RunOrchestrationCommandHandler`/`RunAIDiscoveryPipelineCommandHandler` need no explicit registration — MediatR already scans the whole Application assembly, same as every prior phase's handlers.)

## Step 3 — Infrastructure: notifier (`src/NewAlbumsDiscovery.Infrastructure/Notifications/`) ✅

1. **Test-first:** `ConsoleDiscoveryNotifierTests` — redirect `Console.Out` to a `StringWriter` for the duration of each test (restore the original writer in a `finally`, to avoid cross-test pollution), then assert each of the three notify methods writes output containing the expected values (bucket count / bucket name + track count / processed count).
   **Implement:** `ConsoleDiscoveryNotifier : IDiscoveryNotifier` — three `Console.WriteLine` calls, each returning `Task.CompletedTask`.
2. Register in `InfrastructureServiceCollectionExtensions.AddInfrastructureServices`:
   ```csharp
   services.AddScoped<IDiscoveryNotifier, ConsoleDiscoveryNotifier>();
   ```

## Step 4 — Worker: orchestrator hosted service ✅

1. Delete `src/NewAlbumsDiscovery.Worker/AggregationStartupWorker.cs`.
2. Add `src/NewAlbumsDiscovery.Worker/OrchestrationStartupWorker.cs` — same thin shape as the file it replaces (`BackgroundService`, `Task.Run` on a scoped `ISender`, honoring `stoppingToken`), but sends `RunOrchestrationCommand` instead of `AggregateMusicPreferencesCommand` directly. Doc comment updated to state this runs Features 1 and 2 sequentially once per process start, with Feature 3 expected to join the sequence (inside `RunOrchestrationCommandHandler`, not here) in a future phase.
3. Update `Program.cs`: swap the `AddHostedService<AggregationStartupWorker>()` registration for `AddHostedService<OrchestrationStartupWorker>()`. `HeartbeatWorker` registration is untouched.
4. No dedicated test for `OrchestrationStartupWorker` itself — consistent with `AggregationStartupWorker` never having one; the real sequencing/timing logic it delegates to (`RunOrchestrationCommandHandler`) is fully unit-tested in Step 2.

## Step 5 — Config files ✅

Add to both `appsettings.json` and `appsettings.Development.json`, under the existing `NewAlbumsDiscovery` section, alongside `Aggregator`/`Gemini`:
```json
"CoreOperations": {
  "TriggerDelaySeconds": 30
},
"AIDiscovery": {
  "InterBucketDelaySeconds": 10
}
```

## Step 6 — Full-suite verification (partially ✅ — see item 4)

1. `dotnet build` — solution builds clean. **Done.**
2. `dotnet test` — all existing Phase 1–4 tests still pass (no regressions), all new Phase 5 tests pass. **Done** — 138/138 at the time this phase shipped (77 Domain, 29 Application, 32 Infrastructure). Note: the subsequent Phase 4 rollback (see FUNCTIONAL_REQUIREMENTS.md → Phase 4 → Rollback) later deleted Phase 4's tests, not Phase 5's — the current baseline is 88/88 (48 Domain, 23 Application, 17 Infrastructure), all of which are Phase 1–3 and Phase 5 tests.
3. Coverage report confirms 100% branch coverage on every new `Application` class (`RunOrchestrationCommandHandler`, `RunAIDiscoveryPipelineCommandHandler`, `StartNotificationStage`, `BucketProcessingStage`, `ReportPublicationStage`, `PrintBucketStep`) and that `Domain`'s existing coverage is unaffected (no new Domain code this phase). **Done** — confirmed both at original ship time and again post-rollback.
4. Manual sanity check (documented, not automated — matches Phase 3/4 precedent for end-to-end Worker runs): start the Worker against a populated `loved-tracks.db`, observe the console for the aggregation run, the 30-second gap, the "starting N buckets" notice, N bucket-processed lines paced 10 seconds apart in descending `TrackCount` order, and the final "processed N" report. This is a `Console.WriteLine`-only run — **no Gemini API key is required for this phase**, unlike Phase 4's Step 8 item 4. **Not done.** The user has since pointed `NewAlbumsDiscovery__Database__LovedTracksDbPath` at a real database via the elevated setup script and was given the `dotnet run --project src/NewAlbumsDiscovery.Worker` command, but no confirmation of an actual run has been reported back. This remains the one recommended follow-up before considering Phase 5's runtime behavior fully validated.

## Explicit non-goals (see FUNCTIONAL_REQUIREMENTS.md → Phase 5 → Out of Scope)

- No real Gemini calls anywhere in `BucketProcessingStage` — `PrintBucketStep` is the only step this phase.
- No Feature 3 / Notification bounded context, no Telegram implementation of `IDiscoveryNotifier`.
- No code consumes the newly-copied `countries-languages.json`, `country-genres-prompt.md`, or `genre-expansion-prompt.md` yet.
- No change to `DiscoverAlbumsCommand` at the time this phase shipped — it was later deleted outright by the Phase 4 rollback (see FUNCTIONAL_REQUIREMENTS.md → Phase 4 → Rollback), not absorbed into this pipeline.
- No real periodic/monthly scheduling — `OrchestrationStartupWorker` still runs once per process start.
