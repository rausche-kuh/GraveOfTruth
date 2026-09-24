#!/usr/bin/env bash
# Builds mods and installs them into the profile configured by scripts/setup.sh.
# With no mod names, every mod in the repo is deployed.
#
# Usage: scripts/deploy.sh [-c Debug|Release] [--profile-dir DIR] [mod ...]
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

configuration=Release
profile=""
mods=()

while [ $# -gt 0 ]; do
    case "$1" in
        -c|--configuration) configuration=${2:-}; [ -n "$configuration" ] || die "$1 needs a value"; shift 2 ;;
        --profile-dir)      profile=${2:-}; [ -n "$profile" ] || die "$1 needs a value"; shift 2 ;;
        -h|--help)          usage; exit 0 ;;
        -*)                 die "unknown argument: $1" ;;
        *)                  mods+=("$1"); shift ;;
    esac
done
[ "$configuration" = Debug ] || [ "$configuration" = Release ] ||
    die "configuration must be Debug or Release"

mods=($(resolve_mods "${mods[@]+"${mods[@]}"}"))
[ -n "$profile" ] || profile=$(profile_dir)

# Mono maps a plugin DLL into memory and reads method bodies from it lazily, so overwriting one
# under a running game corrupts what it has not compiled yet ("BadImageFormatException: Method
# has zero rva", garbled method names in stack traces). The copy still happens - the game has
# to be restarted for the new build anyway - but say so.
if pgrep -x valheim.x86_64 >/dev/null 2>&1 || pgrep -x valheim.exe >/dev/null 2>&1; then
    note "Valheim is running: the game keeps the old build and may throw BadImageFormatException until restarted."
fi

for mod in "${mods[@]}"; do
    step "Deploying $mod..."
    dotnet build "$root/$mod/$mod.csproj" -c "$configuration"

    plugin_dir="$profile/BepInEx/plugins/$author-$mod"
    mkdir -p "$plugin_dir"
    cp "$root/$mod/bin/$configuration/$mod.dll" "$plugin_dir"
    [ -d "$root/$mod/assets" ] && cp -r "$root/$mod/assets/." "$plugin_dir" || true
    cp "$root/$mod/package/manifest.json" "$plugin_dir"
    cp "$root/$mod/package/icon.png" "$plugin_dir"

    ok "Installed to $plugin_dir"
done
