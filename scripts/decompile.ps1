#Requires -Version 5.1
<#
.SYNOPSIS
    Decompiles the game assemblies into decompiled/ (gitignored) so the game's API can be read
    and grepped. Shared by every mod in the repo. Re-run after a Valheim update.
#>
[CmdletBinding()]
param(
    [string[]]$Assemblies = @('assembly_valheim', 'assembly_utils'),
    # Decompile again even if output already exists.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib.ps1')

$lib = Join-Path $Root 'lib'
if (-not (Test-Path $lib)) { throw 'lib/ missing - run scripts\setup.ps1 first.' }

# Pinned: ilspycmd 10.x ships as a net10.0 tool and will not install on the .NET 8 SDK.
$ilspy = Join-Path $env:USERPROFILE '.dotnet\tools\ilspycmd.exe'
if (-not (Test-Path $ilspy)) {
    Write-Host 'Installing ilspycmd...' -ForegroundColor Yellow
    dotnet tool install -g ilspycmd --version 9.1.0.7988
    if (-not (Test-Path $ilspy)) { throw 'Could not install ilspycmd.' }
}

foreach ($name in $Assemblies) {
    $out = Join-Path $Root "decompiled\$name"
    if ((Test-Path $out) -and -not $Force) { Write-Host "$name already decompiled (use -Force)"; continue }
    New-Item -ItemType Directory $out -Force | Out-Null

    Write-Host "Decompiling $name..." -ForegroundColor Cyan
    & $ilspy -p -o $out -r $lib (Join-Path $lib "$name.dll")
    if ($LASTEXITCODE -ne 0) { throw "ilspycmd failed for $name" }
}

Write-Host 'Done. Source is in decompiled/ (gitignored).' -ForegroundColor Green
