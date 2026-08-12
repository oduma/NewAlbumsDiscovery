# Phase 2.1 Implementation Plan: Rename Read-Only Database Env Var

**Requirements source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 2.1
**Status:** 🛠️ Implemented, algorithm verified in isolation — awaiting the user's own manual elevated/root testing before being marked done.

## Recap of Confirmed Decisions
- Naming algorithm extended (not replaced): hyphen (`-`) still starts a new `__`-joined top-level segment; a new rule adds underscore (`_`) within a segment as a compound-word marker — sub-words are PascalCased and concatenated with no separator. `database-loved_tracks_db_path.txt` → `NewAlbumsDiscovery__Database__LovedTracksDbPath`.
- `z-com-ai/loved-tracks-db-path.txt` → renamed to `z-com-ai/database-loved_tracks_db_path.txt`; content stays `DB/loved-tracks.db` (already correct on disk).
- File content convention: always forward slashes, regardless of OS.
- Old variable `NewAlbumsDiscovery__Sqlite__Db__Path` is actively removed by both scripts on every run (hardcoded deprecated-name list), not just left stale.
- `NewAlbumsDiscovery__Database__AppDbPath` is explicitly out of scope for this phase.

## Step-by-Step Plan

### Step 1 — Rename & confirm the z-com-ai source file
- [x] Rename `z-com-ai/loved-tracks-db-path.txt` → `z-com-ai/database-loved_tracks_db_path.txt`.
- [x] Confirm content is `DB/loved-tracks.db` (already correct — no edit needed, just verified after rename).

### Step 2 — Extend `scripts/setup-env.ps1`
- [x] Update `ConvertTo-EnvVarName`: for each hyphen-segment, if it contains `_`, split on `_`, PascalCase each sub-word (uppercase first char, lowercase rest — same rule as before, just applied per sub-word), concatenate with no separator; segments without `_` keep the existing single-segment PascalCase behavior unchanged. (Implemented as a new `ConvertTo-PascalWord` helper reused per sub-word.)
- [x] Add `$DeprecatedVarNames = @('NewAlbumsDiscovery__Sqlite__Db__Path')` near the top of the script.
- [x] After the elevation check, iterate `$DeprecatedVarNames`: if `[Environment]::GetEnvironmentVariable($name, 'Machine')` is non-null, call `[Environment]::SetEnvironmentVariable($name, $null, 'Machine')` to delete it, `Remove-Item "Env:$name" -ErrorAction SilentlyContinue` for the current session, and print a confirmation line (`Removed deprecated variable: <name>`).
- [x] Update the script's header comment block to document the underscore compound-word rule alongside the existing hyphen rule.

### Step 3 — Extend `scripts/setup-env.sh`
- [x] Update `to_env_var_name`: same underscore-compound-word extension — for each `-`-split segment, if it contains `_`, split on `_`, PascalCase each sub-word, concatenate with no separator; segments without `_` keep existing behavior. (Implemented as a new `to_pascal_compound` helper reused per sub-word.)
- [x] Add `DEPRECATED_VAR_NAMES=("NewAlbumsDiscovery__Sqlite__Db__Path")` near the top; `remove_deprecated_vars` called right after the root check passes (removal requires root).
- [x] For each name in the list: remove matching lines from `/etc/environment` and `/etc/newalbumsdiscovery.env` (`sed -i "/^${name}=/d" "$file"`, guarded by file existence), `unset "$name" 2>/dev/null || true` (effective only if sourced, consistent with the existing platform-asymmetry note), and print a confirmation line per variable actually removed (skip silently if the line wasn't present).
- [x] Update the script's header comment block similarly.

### Step 4 — Verification
- [x] **Isolated algorithm check (no elevation needed):** exercised `ConvertTo-EnvVarName` / `to_env_var_name` in throwaway scratch scripts against `database-loved_tracks_db_path` → confirmed exact output `NewAlbumsDiscovery__Database__LovedTracksDbPath` on both PowerShell and Bash. Also tested `sqlite-db-path` and a synthetic `gemini-api-key` (no underscores) → confirmed unchanged legacy-style output on both platforms (regression check passed).
- [ ] **Elevated Windows run:** confirm `NewAlbumsDiscovery__Database__LovedTracksDbPath` set to `<USERPROFILE>\DB\loved-tracks.db` at Machine scope, and confirm `NewAlbumsDiscovery__Sqlite__Db__Path` is gone from Machine scope afterward. **Deferred to the user's own manual testing** (declined automated verification for this round).
- [ ] **WSL root+sourced run:** same two checks on Linux — new var present in `/etc/environment` + `/etc/newalbumsdiscovery.env`, old var's lines removed from both files. **Deferred to the user's own manual testing.**
- [ ] **Idempotency:** rerun both scripts a second time; confirm no duplicate lines for the new variable and no error/duplicate-removal-attempt side effects from the already-removed deprecated variable. **Deferred to the user's own manual testing.**

### Step 5 — Wrap-up
- [ ] Update `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` Phase 2.1 with any as-built corrections found during Step 4.
- [ ] Mark this plan done — **only after the user's own manual testing and explicit go-ahead**, consistent with the standing instruction from Phase 2.

## Files To Be Changed
```
z-com-ai/
  loved-tracks-db-path.txt  →  database-loved_tracks_db_path.txt  (renamed)
scripts/
  setup-env.ps1  (modified: naming algorithm + deprecated-variable cleanup)
  setup-env.sh   (modified: naming algorithm + deprecated-variable cleanup)
```
No changes to `src/`, `tests/`, or the solution.

## Explicitly Not Done In This Phase
- No handling of `NewAlbumsDiscovery__Database__AppDbPath` or any other variable from the updated architecture (Gemini, Telegram).
- No application-side database connection code.
- No change to `docs/requirements/phase2-requirements.md`.
- No generic/extensible deprecated-variable framework — a small hardcoded list is enough for this one rename (KISS).
