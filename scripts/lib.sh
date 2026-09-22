# Shared helpers for the scripts in this folder. Sourced, never executed.
#
# A mod is any directory at the repo root that holds <Name>/<Name>.csproj. Scripts that take mod
# names default to every mod in the repo.

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
# Thunderstore team name - the install folder and zip are named <author>-<mod>.
author=rauschekuh

die()  { printf '\033[31merror:\033[0m %s\n' "$*" >&2; exit 1; }
info() { printf '%s\n' "$*"; }
note() { printf '\033[33m%s\033[0m\n' "$*"; }
step() { printf '\033[36m%s\033[0m\n' "$*"; }
ok()   { printf '\033[32m%s\033[0m\n' "$*"; }

# The leading comment block of the calling script, minus the shebang.
usage() { awk 'NR==1 {next} /^#/ {sub(/^# ?/, ""); print; next} {exit}' "$0"; }

all_mods() {
    local d n
    for d in "$root"/*/; do
        n=${d%/}; n=${n##*/}
        [ -f "$d$n.csproj" ] && printf '%s\n' "$n"
    done
    return 0
}

# Mod names given as arguments, or every mod if there were none.
resolve_mods() {
    local m
    if [ $# -eq 0 ]; then
        all_mods
        [ -n "$(all_mods)" ] || die "no mods found in $root"
        return 0
    fi
    for m in "$@"; do
        [ -f "$root/$m/$m.csproj" ] || die "unknown mod '$m' (have: $(all_mods | tr '\n' ' '))"
        printf '%s\n' "$m"
    done
}

# The VERSION const out of a mod's plugin source - the single source of truth for its version.
mod_version() {
    local v
    v=$(grep -rhoE 'VERSION[[:space:]]*=[[:space:]]*"[^"]+"' "$root/$1/src" 2>/dev/null |
        head -1 | sed -E 's/.*"([^"]+)"/\1/')
    [ -n "$v" ] || die "could not read VERSION from $1/src"
    printf '%s\n' "$v"
}

# The mod manager profile scripts/setup.sh picked, e.g. .../gale/valheim/profiles/Default.
profile_dir() {
    local props="$root/Valheim.props" p
    [ -f "$props" ] || die 'Valheim.props missing - run scripts/setup.sh first.'
    p=$(sed -nE 's;.*<ProfileDir>(.*)</ProfileDir>.*;\1;p' "$props")
    [ -n "$p" ] || die 'no <ProfileDir> in Valheim.props - re-run scripts/setup.sh.'
    printf '%s\n' "$p"
}
