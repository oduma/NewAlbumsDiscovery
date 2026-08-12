# Phase 2 Implementation Plan: Environment Variable Developer Setup Scripts

**Requirements source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 2 (finalized from `docs/requirements/phase2-requirements.md`)
**Status:** ✅ DONE (2026-08-12) — implemented, runtime-verified on both platforms (Windows directly, Linux via WSL), and confirmed by the user's own manual testing. Two real bugs found and fixed during verification (see Step 4 and `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 2 → "As-Built Corrections").

## Recap of Confirmed Decisions
- Filename → env var name: pure mechanical transform (`strip .txt` → split on `-` → PascalCase each segment → join `__` → prefix `NewAlbumsDiscovery__`). `sqlite-db-path.txt` → `NewAlbumsDiscovery__Sqlite__Db__Path`.
- Linux: write to both `/etc/environment` and `/etc/newalbumsdiscovery.env` (systemd scaffold, unused until a later phase).
- Both scripts also set the variable in their own process for immediate session feedback, with the sourcing caveat documented and enforced on Linux.
- No directory creation.
- Elevation is a hard gate: check first, exit non-zero with instructions if not elevated/root, before any writes.
- Idempotent upsert everywhere (no duplicate entries on rerun).

## Step-by-Step Plan

### Step 1 — `scripts/setup-env.ps1`
- [x] Resolve `z-com-ai/` relative to `$PSScriptRoot` (script's own folder, not caller's CWD): `Join-Path $PSScriptRoot '..\z-com-ai'`.
- [x] **Elevation check first, before any other work:** `([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)`. If false: `Write-Host` a clear message ("Re-run this script from an elevated PowerShell window: right-click PowerShell → Run as Administrator, then re-run `.\scripts\setup-env.ps1`"), `exit 1`.
- [x] Enumerate `Get-ChildItem -Path $zComAiDir -Filter '*.txt'`. If the directory or no matching files exist, print a message and exit 0 (nothing to do is not an error).
- [x] For each file:
  - Compute `$varName` via the mechanical transform (helper function `ConvertTo-EnvVarName($fileBaseName)`).
  - Read content (`Get-Content -Raw`), `Trim()` it, treat as home-relative path.
  - Resolve absolute path via `Join-Path $env:USERPROFILE $relativePath` (no existence/creation check).
  - `[Environment]::SetEnvironmentVariable($varName, $absolutePath, 'Machine')` — naturally idempotent (overwrite semantics), no extra upsert logic needed on Windows.
  - Also `Set-Item -Path "Env:$varName" -Value $absolutePath` so the *current* process/session sees it immediately (process-scoped $env: write persists in the calling terminal since `.ps1` runs in-process by default).
  - Print a confirmation line: `Set NewAlbumsDiscovery__X = <value> (Machine)`.
- [x] Summary line at the end: how many variables were set, pointing out a new terminal is only needed for *other* already-open windows/processes, not the one that ran the script.

### Step 2 — `scripts/setup-env.sh`
- [x] Resolve script's own directory robustly (works whether sourced or executed): `SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"`; `Z_COM_AI_DIR="$SCRIPT_DIR/../z-com-ai"`.
- [x] **Sourced-vs-executed detection first:** `(return 0 2>/dev/null) && SOURCED=1 || SOURCED=0`. If `SOURCED=0`, print a warning that Machine-level persistence will still work but the current shell won't see the variables until a new shell is opened, and suggest `source scripts/setup-env.sh` instead — then continue (this is a warning, not a hard failure, since the Machine-level part is still valid and useful).
- [x] **Root check next, before any writes:** `if [ "$(id -u)" -ne 0 ]; then echo "Re-run with sudo: sudo bash scripts/setup-env.sh (or 'sudo -E bash -c \"source scripts/setup-env.sh\"' to also update this shell)"; return 1 2>/dev/null || exit 1; fi`. (Using `return`/`exit` fallback pattern so it behaves correctly whether sourced or executed.)
- [x] Enumerate `"$Z_COM_AI_DIR"/*.txt` (guard with a `shopt -s nullglob`-equivalent check so a missing/empty directory doesn't error on the glob).
- [x] For each file:
  - Compute `var_name` via a shell function `to_env_var_name` implementing the same mechanical transform (strip `.txt`, split on `-`, capitalize each segment's first letter, join with `__`, prefix `NewAlbumsDiscovery__`).
  - Read content, trim whitespace/newline, treat as home-relative path.
  - Resolve absolute path: `"$HOME/$relative_path"` via a path-join that avoids doubled slashes.
  - **Upsert into `/etc/environment`:** if a line starting with `var_name=` exists, replace it (`sed -i "s|^${var_name}=.*|${var_name}=${abs_path}|" /etc/environment`); else append `${var_name}=${abs_path}`.
  - **Upsert into `/etc/newalbumsdiscovery.env`** the same way (create the file with safe permissions, e.g. `touch` + `chmod 644`, if it doesn't exist yet).
  - `export "$var_name=$abs_path"` in the current process (effective for the calling shell only if sourced, consistent with the Step 2 warning above).
  - Print a confirmation line.
- [x] Summary line, plus a one-line reminder of the `/etc/newalbumsdiscovery.env` scaffold's purpose (for a future systemd `EnvironmentFile=` directive — not wired to anything yet).

### Step 3 — Shared mechanical-transform algorithm (documented once, implemented twice)
- [x] Write the exact algorithm as a short comment block duplicated verbatim (as a comment, not code — PowerShell and Bash can't share a source file) at the top of both scripts, so the two implementations can be diffed against the same spec:
  ```
  1. Strip the .txt extension.
  2. Split the remaining filename on '-'.
  3. For each segment: uppercase the first character, lowercase the rest.
  4. Join the segments with '__'.
  5. Prefix with 'NewAlbumsDiscovery__'.
  ```
- [x] Manually verify both implementations against the same two examples during Step 4 testing: `sqlite-db-path` → `Sqlite__Db__Path`, and a synthetic `gemini-api-key` → `Gemini__Api__Key`.

### Step 4 — Verification (as actually performed)
- [x] Run `scripts/setup-env.ps1` from a **non-elevated** terminal — confirmed it prints the instruction and exits non-zero (exit code 1), and confirmed via `[Environment]::GetEnvironmentVariable('NewAlbumsDiscovery__Sqlite__Db__Path','Machine')` that nothing was written.
- [x] Run `scripts/setup-env.ps1` **elevated** — real UAC prompt triggered via `Start-Process -Verb RunAs`, approved live by the user (a tool cannot click through UAC on its own). Confirmed `NewAlbumsDiscovery__Sqlite__Db__Path` set at Machine scope to `C:\Users\<user>\DB`.
- [x] Added a temporary `z-com-ai/gemini-api-key.txt` (`models`), reran elevated, confirmed `NewAlbumsDiscovery__Gemini__Api__Key` was picked up automatically with **no script changes** and set to `...\models`. Temp file and its Machine variable both removed afterward.
- [x] Reran the elevated script a second time with `sqlite-db-path.txt` unchanged — confirmed no error, same value (Windows `SetEnvironmentVariable` is inherently idempotent, no separate upsert logic needed).
- [x] **Correction from the original plan: the Linux script *was* runtime-verified, not just reviewed.** WSL (Ubuntu) turned out to be available in this environment, so `setup-env.sh` was actually executed rather than only read. This surfaced two real, previously-undetected bugs (see `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` → Phase 2 → As-Built Corrections for full detail):
  1. `fail()`'s `return 1` (called from a nested function) couldn't actually abort a sourced script — non-root + sourced would print the error and then keep running. Proven with an isolated repro, then fixed by inlining the abort logic at the script's top level at each of the four exit points instead of centralizing it in a function.
  2. Sourcing leaked `set -euo pipefail` into the caller's own interactive shell (since `source` merges shell-option changes into the caller). Fixed by snapshotting the caller's options before applying the script's own and restoring them at every exit point.
  - After both fixes, re-verified via WSL: non-root+executed (blocked), root+executed-not-sourced (persisted, correct warning), root+sourced (persisted and immediately visible via `$NewAlbumsDiscovery__Sqlite__Db__Path` in that same shell), idempotent rerun (line count stayed at 1 in both `/etc/environment` and `/etc/newalbumsdiscovery.env`), dynamic pickup of a temporary second `.txt` file. All WSL-side test state cleaned up (`/etc/environment`, `/etc/newalbumsdiscovery.env`, temp files).
  - The user also independently ran their own manual tests before signing off on this phase as complete.
- [x] Cleanup: the real `NewAlbumsDiscovery__Sqlite__Db__Path` Machine value (derived from the actual `z-com-ai/sqlite-db-path.txt` content, not a synthetic test) was left in place — the user chose to keep it rather than have it removed.

### Step 5 — Wrap-up
- [x] Update `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` Phase 2 with any as-built corrections discovered during Step 4 (mirroring how Phase 1 was closed out).
- [x] Mark this plan file's status as done once Step 4 passes to the extent possible in this environment.

## Files To Be Created
```
scripts/
  setup-env.ps1
  setup-env.sh
```
No changes to `src/`, `tests/`, or the solution in this phase — this is pure developer tooling, not application code.

## Explicitly Not Done In This Phase
- No systemd unit file (only the `/etc/newalbumsdiscovery.env` scaffold, unconsumed).
- No update to `docs/specs/technical-specs.md`'s `Database__Path`-style example (flagged as a follow-up, not actioned here).
- No uninstall/rollback tooling.
- No automated test coverage for the scripts themselves (elevated/root, machine-state-mutating — impractical to unit test; see TDD constitution's infrastructure-isolation rule, which doesn't cleanly apply to OS-level setup scripts outside the .NET solution).
