#Requires -Version 5.1
<#
.SYNOPSIS
    Builds Thunderstore-ready zips in dist/, one per mod. A mod's version comes from the VERSION
    const in its plugin source and is stamped into its package/manifest.json.
    With no mod names, every mod in the repo is packaged.
.EXAMPLE
    .\scripts\package.ps1 GraveOfTruth
#>
[CmdletBinding()]
param(
    # Mods to package. Empty means all of them.
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$Mods
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib.ps1')

$Mods = Resolve-Mods $Mods

foreach ($mod in $Mods) {
    $version = Get-ModVersion $mod
    Write-Host "Packaging $mod $version..." -ForegroundColor Cyan

    $manifestPath = Join-Path $Root "$mod\package\manifest.json"
    $manifest = (Get-Content $manifestPath -Raw) -replace '("version_number"\s*:\s*")[^"]+', "`${1}$version"
    # Thunderstore dislikes a BOM in manifest.json, so bypass Set-Content's encoding defaults.
    [IO.File]::WriteAllText($manifestPath, $manifest.TrimEnd() + "`n", (New-Object Text.UTF8Encoding $false))

    dotnet build (Join-Path $Root "$mod\$mod.csproj") -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $mod." }

    $stage = Join-Path $Root 'dist\stage'
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory $stage -Force | Out-Null

    Copy-Item (Join-Path $Root "$mod\bin\Release\$mod.dll") $stage
    $assets = Join-Path $Root "$mod\assets"
    if (Test-Path $assets) { Copy-Item "$assets\*" $stage -Recurse -Force }
    Copy-Item $manifestPath $stage
    Copy-Item (Join-Path $Root "$mod\package\icon.png") $stage
    Copy-Item (Join-Path $Root "$mod\README.md") $stage

    $zip = Join-Path $Root "dist\$mod-$version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    # Thunderstore wants the files at the root of the zip, no stage\ prefix.
    Compress-Archive -Path "$stage\*" -DestinationPath $zip
    Remove-Item $stage -Recurse -Force

    Write-Host "Packaged $zip" -ForegroundColor Green
}
