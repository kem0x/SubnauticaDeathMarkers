#requires -Version 5
<#
  Builds the mod in Release config and zips it into a Thunderstore-style
  drop-in folder layout, ready to upload to GitHub Releases / Thunderstore.

  Output: SubnauticaDeathMarkers.zip at the repo root.

  Usage (from anywhere):
    pwsh ./scripts/package.ps1
#>
$ErrorActionPreference = "Stop"

$root      = Resolve-Path (Join-Path $PSScriptRoot "..")
$modDir    = Join-Path $root "mod"
$stage     = Join-Path $root "release"
$pluginDir = Join-Path $stage "SubnauticaDeathMarkers"
$zipPath   = Join-Path $root "SubnauticaDeathMarkers.zip"

Write-Host "→ Building Release config…"
& dotnet build -c Release $modDir | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

if (Test-Path $stage)   { Remove-Item $stage -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

$dll = Join-Path $modDir "bin\Release\SubnauticaDeathMarkers.dll"
if (-not (Test-Path $dll)) { throw "Built DLL not found at $dll" }
Copy-Item $dll $pluginDir

@"
# Death Markers — Subnautica

Drop the SubnauticaDeathMarkers/ folder into:

    <Subnautica>\BepInEx\plugins\SubnauticaDeathMarkers\

Requires BepInExPack_Subnautica + Nautilus (install both from NexusMods first).

Configuration: <Subnautica>\BepInEx\config\com.kareem.deathmarkers.cfg
"@ | Set-Content -Path (Join-Path $pluginDir "README.txt") -Encoding UTF8

Compress-Archive -Path $pluginDir -DestinationPath $zipPath -Force
Write-Host "✓ Packaged $zipPath"
