# Phase 2 Requirements: Environment Variable Developer Setup Scripts

## 1. Objective
Develop cross-platform developer setup scripts (PowerShell for Windows and Bash for Linux) that read configuration inputs from the `.gitignore`'d `z-com-ai/` directory, resolve user-home-relative paths to absolute paths, and persist environment variables at the **Machine** level.

## 2. Input Directory & Dynamic Variable Mapping
- **Target Directory**: `z-com-ai/` (subfolder in the project root, ignored by Git).
- **Input Files**: All `.txt` files present in `z-com-ai/`.
  - Current Database Path File: `sqlite-db_path.txt` (Files ending in `_path.txt` are treated as relative paths)
  - Current API Key File: `gemini-api-key.txt` (Files NOT ending in `_path.txt` are treated as raw strings)
- **Dynamic Variable Mapping**:
  - The script maps filenames to .NET configuration tree environment variables prefixed with `NewAlbumsDiscovery__`.
  - The filename (without `.txt` and without `_path`) is converted to PascalCase or left as is for the config key.
  - Example 1: `sqlite-db_path.txt` -> `NewAlbumsDiscovery__SqliteDb`
  - Example 2: `gemini-api-key.txt` -> `NewAlbumsDiscovery__GeminiApiKey`

## 3. Path Resolution Logic
- For files ending in `_path.txt`, the content represents a path relative to the current user's home directory (e.g., `DB`).
- The script must resolve this relative path to a full absolute path using the active user's home folder:
  - **Windows**: `$env:USERPROFILE` (e.g., `C:\Users\<Username>\DB`)
  - **Linux**: `$HOME` (e.g., `/home/<Username>/DB`)

## 4. Machine-Level Persistence & Elevation
- **Machine-Level Scope**:
  - Environment variables must be set at the **Machine** level (not User or Process level) so Windows Services and system daemons running under system accounts can access them.
- **Windows Script (`scripts/setup-env.ps1`)**:
  - Sets environment variables using `[Environment]::SetEnvironmentVariable($name, $value, "Machine")`.
  - Must check for Administrator elevation and inform the user if elevation is required.
- **Linux Script (`scripts/setup-env.sh`)**:
  - Persists environment variables globally (e.g., updating `/etc/environment` or system profile).
  - Must check for `sudo` privileges.

## 5. Extensibility
- The script must dynamically scan for all `.txt` files in `z-com-ai/`.
- Adding new configuration `.txt` files in future phases will automatically set corresponding `NewAlbumsDiscovery__*` machine environment variables without needing code changes to the setup script.
