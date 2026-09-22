#Requires -Version 5.1
<#
.SYNOPSIS
    Builds mods and installs them into the profile configured by scripts/setup.ps1.
    With no mod names, every mod in the repo is deployed.
.EXAMPLE
    .\scripts\deploy.ps1 GraveOfTruth -Configuration Debug
#>
[CmdletBinding()]
param(
    # Mods to deploy. Empty means all of them.
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$Mods,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    # Install into another profile than the ProfileDir in Valheim.props.
    [string]$ProfileDir
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib.ps1')

$Mods = Resolve-Mods $Mods
if (-not $ProfileDir) { $ProfileDir = Get-ProfileDir }

foreach ($mod in $Mods) {
    Write-Host "Deploying $mod..." -ForegroundColor Cyan
    dotnet build (Join-Path $Root "$mod\$mod.csproj") -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $mod." }

    $pluginDir = Join-Path $ProfileDir "BepInEx\plugins\$Author-$mod"
    New-Item -ItemType Directory $pluginDir -Force | Out-Null
    Copy-Item (Join-Path $Root "$mod\bin\$Configuration\$mod.dll") $pluginDir -Force
    $assets = Join-Path $Root "$mod\assets"
    if (Test-Path $assets) { Copy-Item "$assets\*" $pluginDir -Recurse -Force }
    Copy-Item (Join-Path $Root "$mod\package\manifest.json") $pluginDir -Force
    Copy-Item (Join-Path $Root "$mod\package\icon.png") $pluginDir -Force

    Write-Host "Installed to $pluginDir" -ForegroundColor Green
}
