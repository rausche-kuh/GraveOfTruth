#!/usr/bin/env bash
# Builds Thunderstore-ready zips in dist/, one per mod. A mod's version comes from the VERSION
# const in its plugin source and is stamped into its package/manifest.json.
# With no mod names, every mod in the repo is packaged.
#
# Usage: scripts/package.sh [mod ...]
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

mods=()
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help) usage; exit 0 ;;
        -*)        die "unknown argument: $1" ;;
        *)         mods+=("$1"); shift ;;
    esac
done

mods=($(resolve_mods "${mods[@]+"${mods[@]}"}"))

for mod in "${mods[@]}"; do
    version=$(mod_version "$mod")
    step "Packaging $mod $version..."

    manifest="$root/$mod/package/manifest.json"
    # Thunderstore dislikes a BOM in manifest.json; sed in place keeps the file plain UTF-8.
    sed -i -E "s;(\"version_number\"[[:space:]]*:[[:space:]]*\")[^\"]+;\1$version;" "$manifest"

    dotnet build "$root/$mod/$mod.csproj" -c Release

    stage="$root/dist/stage"
    rm -rf "$stage"
    mkdir -p "$stage"

    cp "$root/$mod/bin/Release/$mod.dll" "$stage"
    [ -d "$root/$mod/assets" ] && cp -r "$root/$mod/assets/." "$stage" || true
    cp "$manifest" "$stage"
    cp "$root/$mod/package/icon.png" "$stage"
    cp "$root/$mod/README.md" "$stage"

    zip="$root/dist/$mod-$version.zip"
    rm -f "$zip"
    # Thunderstore wants the files at the root of the zip, no stage/ prefix.
    if command -v zip >/dev/null 2>&1; then
        (cd "$stage" && zip -q -r "$zip" .)
    else
        python3 - "$zip" "$stage" <<'PY'
import os, sys, zipfile
zip_path, stage = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as z:
    for dirpath, _, names in os.walk(stage):
        for n in sorted(names):
            full = os.path.join(dirpath, n)
            z.write(full, os.path.relpath(full, stage))
PY
    fi
    rm -rf "$stage"

    ok "Packaged $zip"
done
