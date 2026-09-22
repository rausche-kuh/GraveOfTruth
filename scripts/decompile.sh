#!/usr/bin/env bash
# Decompiles the game assemblies into decompiled/ (gitignored) so the game's API can be read
# and grepped. Shared by every mod in the repo. Re-run after a Valheim update.
#
# Usage: scripts/decompile.sh [--force] [assembly ...]
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

lib="$root/lib"
force=0
assemblies=()

while [ $# -gt 0 ]; do
    case "$1" in
        -f|--force) force=1; shift ;;
        -h|--help)  usage; exit 0 ;;
        -*)         die "unknown argument: $1" ;;
        *)          assemblies+=("$1"); shift ;;
    esac
done
[ ${#assemblies[@]} -gt 0 ] || assemblies=(assembly_valheim assembly_utils)

[ -d "$lib" ] || die 'lib/ missing - run scripts/setup.sh first.'

# Pinned: ilspycmd 10.x ships as a net10.0 tool and will not install on the .NET 8 SDK.
ilspy="${DOTNET_TOOLS_DIR:-$HOME/.dotnet/tools}/ilspycmd"
if [ ! -x "$ilspy" ]; then
    note 'Installing ilspycmd...'
    dotnet tool install -g ilspycmd --version 9.1.0.7988
    [ -x "$ilspy" ] || die 'Could not install ilspycmd.'
fi

for name in "${assemblies[@]}"; do
    out="$root/decompiled/$name"
    if [ -d "$out" ] && [ "$force" -eq 0 ]; then
        info "$name already decompiled (use --force)"
        continue
    fi
    mkdir -p "$out"
    step "Decompiling $name..."
    "$ilspy" -p -o "$out" -r "$lib" "$lib/$name.dll" || die "ilspycmd failed for $name"
done

ok 'Done. Source is in decompiled/ (gitignored).'
