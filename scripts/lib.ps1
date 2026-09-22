# Shared helpers for the scripts in this folder. Dot-sourced, never run directly.
#
# A mod is any directory at the repo root that holds <Name>\<Name>.csproj. Scripts that take mod
# names default to every mod in the repo.

$Root = Split-Path $PSScriptRoot -Parent
# Thunderstore team name - the install folder and zip are named <author>-<mod>.
$Author = 'rauschekuh'

function Get-Mods {
    Get-ChildItem $Root -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } |
        ForEach-Object Name
}

# Mod names as passed on the command line, or every mod if there were none.
function Resolve-Mods([string[]]$Names) {
    $all = @(Get-Mods)
    if (-not $Names -or $Names.Count -eq 0) {
        if (-not $all) { throw "no mods found in $Root" }
        return $all
    }
    foreach ($n in $Names) {
        if ($all -notcontains $n) { throw "unknown mod '$n' (have: $($all -join ', '))" }
    }
    return $Names
}

# The VERSION const out of a mod's plugin source - the single source of truth for its version.
function Get-ModVersion([string]$Mod) {
    foreach ($file in Get-ChildItem (Join-Path $Root "$Mod\src") -Filter *.cs -Recurse) {
        if ((Get-Content $file.FullName -Raw) -match 'VERSION\s*=\s*"([^"]+)"') { return $Matches[1] }
    }
    throw "could not read VERSION from $Mod\src"
}

# The mod manager profile scripts\setup.ps1 picked, e.g. ...\gale\valheim\profiles\Default.
function Get-ProfileDir {
    $props = Join-Path $Root 'Valheim.props'
    if (-not (Test-Path $props)) { throw 'Valheim.props missing - run scripts\setup.ps1 first.' }
    $dir = ([xml](Get-Content $props -Raw)).Project.PropertyGroup.ProfileDir
    if (-not $dir) { throw 'no <ProfileDir> in Valheim.props - re-run scripts\setup.ps1.' }
    return $dir
}
