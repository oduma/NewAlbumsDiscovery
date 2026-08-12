#!/usr/bin/env bash
#
# Scans z-com-ai/*.txt and persists each as a machine-wide NewAlbumsDiscovery__* environment
# variable, in /etc/environment (read by PAM at login) and /etc/newalbumsdiscovery.env (a
# scaffold for a future systemd unit's EnvironmentFile= directive - unused until that phase).
# See docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 2 for the full spec.
#
# Filename -> env var name (mechanical transform):
#   1. Strip the .txt extension.
#   2. Split the remaining filename on '-'.
#   3. For each segment: uppercase the first character, lowercase the rest.
#   4. Join the segments with '__'.
#   5. Prefix with 'NewAlbumsDiscovery__'.
#   Example: sqlite-db-path.txt -> NewAlbumsDiscovery__Sqlite__Db__Path
#
# File content is treated as a path relative to $HOME.
#
# Must be run as root (sudo) - machine-wide persistence requires it. Must be *sourced*
# (`source scripts/setup-env.sh`) for the current shell to see the new variables immediately;
# running it as a plain executed script still persists correctly, it just won't be visible in
# that same shell afterward (a child process cannot modify its parent shell's environment).
#
# Note on control flow: every abort/early-return point below is inlined at the script's TOP
# LEVEL (not hidden inside a helper function). `return` only unwinds one function call frame -
# calling it from inside a nested function when this script is sourced would NOT stop the
# sourced script, it would just return control to whatever called that function and execution
# would continue. Only a `return` issued directly at the script's own top level correctly aborts
# a sourced run, so the SOURCED-conditional return/exit choice is repeated at each call site
# rather than centralized in a function.

if (return 0 2>/dev/null); then
    SOURCED=1
else
    SOURCED=0
fi

# When sourced, `set -euo pipefail` below would otherwise leak into the caller's interactive
# shell (source merges shell-option changes into the caller). Save the caller's current options
# now and restore them at every exit point so sourcing this script has no side effect on the
# developer's shell beyond the intended NewAlbumsDiscovery__* variables.
if [ "$SOURCED" -eq 1 ]; then
    PREVIOUS_SHELL_OPTS="$(set +o)"
fi

restore_shell_opts() {
    if [ "$SOURCED" -eq 1 ] && [ -n "${PREVIOUS_SHELL_OPTS:-}" ]; then
        eval "$PREVIOUS_SHELL_OPTS"
        unset PREVIOUS_SHELL_OPTS
    fi
}

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
Z_COM_AI_DIR="$SCRIPT_DIR/../z-com-ai"
SYSTEMD_SCAFFOLD_FILE="/etc/newalbumsdiscovery.env"

if [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: this script must be run as root to persist machine-wide environment variables.

  Persist only (this shell will not see the new variables):
    sudo bash scripts/setup-env.sh

  Persist AND make the new variables visible in your shell immediately:
    sudo -s
    source scripts/setup-env.sh" >&2
    restore_shell_opts
    if [ "$SOURCED" -eq 1 ]; then return 1; else exit 1; fi
fi

if [ "$SOURCED" -eq 0 ]; then
    echo "NOTE: running as a script (not sourced) - /etc/environment and $SYSTEMD_SCAFFOLD_FILE will be updated, but this shell will not see the new variables. Re-run as 'source scripts/setup-env.sh' (while already root) if you want that." >&2
fi

to_env_var_name() {
    local base_name="$1"
    local -a segments
    IFS='-' read -ra segments <<< "$base_name"
    local var_name="NewAlbumsDiscovery"
    local seg first rest
    for seg in "${segments[@]}"; do
        [ -z "$seg" ] && continue
        first="$(printf '%s' "${seg:0:1}" | tr '[:lower:]' '[:upper:]')"
        rest="$(printf '%s' "${seg:1}" | tr '[:upper:]' '[:lower:]')"
        var_name="${var_name}__${first}${rest}"
    done
    printf '%s' "$var_name"
}

upsert_env_file() {
    local file="$1" key="$2" value="$3"
    if [ ! -f "$file" ]; then
        touch "$file"
        chmod 644 "$file"
    fi
    if grep -q "^${key}=" "$file" 2>/dev/null; then
        sed -i "s|^${key}=.*|${key}=${value}|" "$file"
    else
        echo "${key}=${value}" >> "$file"
    fi
}

if [ ! -d "$Z_COM_AI_DIR" ]; then
    echo "No z-com-ai directory found at $Z_COM_AI_DIR. Nothing to do."
    restore_shell_opts
    if [ "$SOURCED" -eq 1 ]; then return 0; else exit 0; fi
fi

shopt -s nullglob
files=("$Z_COM_AI_DIR"/*.txt)
shopt -u nullglob

if [ ${#files[@]} -eq 0 ]; then
    echo "No .txt files found in $Z_COM_AI_DIR. Nothing to do."
    restore_shell_opts
    if [ "$SOURCED" -eq 1 ]; then return 0; else exit 0; fi
fi

processed_count=0
for f in "${files[@]}"; do
    filename="$(basename "$f")"
    base_name="${filename%.txt}"
    var_name="$(to_env_var_name "$base_name")"

    relative_path=""
    IFS= read -r relative_path < "$f" || true
    relative_path="$(printf '%s' "$relative_path" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/\r$//')"

    if [ -z "$relative_path" ]; then
        echo "Skipping $filename: file is empty." >&2
        continue
    fi

    relative_path="${relative_path#/}"
    relative_path="${relative_path%/}"
    abs_path="${HOME%/}/$relative_path"

    upsert_env_file /etc/environment "$var_name" "$abs_path"
    upsert_env_file "$SYSTEMD_SCAFFOLD_FILE" "$var_name" "$abs_path"
    export "$var_name=$abs_path"

    echo "Set $var_name = $abs_path (/etc/environment + $SYSTEMD_SCAFFOLD_FILE)"
    processed_count=$((processed_count + 1))
done

echo ""
echo "Done. $processed_count variable(s) processed."
echo "$SYSTEMD_SCAFFOLD_FILE is a scaffold for a future systemd unit's EnvironmentFile= directive; nothing consumes it yet."

restore_shell_opts
