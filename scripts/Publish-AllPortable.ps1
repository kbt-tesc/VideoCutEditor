param(
    [string[]]$Platforms = @("x64", "x86", "arm64"),

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$publishScript = Join-Path $PSScriptRoot "Publish-Portable.ps1"
if (!(Test-Path $publishScript)) {
    throw "Publish script was not found at $publishScript"
}

$validPlatforms = @("x64", "x86", "arm64")
$selectedPlatforms = @($Platforms | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($platform in $selectedPlatforms) {
    if ($validPlatforms -inotcontains $platform) {
        throw "Unsupported platform '$platform'. Expected one of: $($validPlatforms -join ', ')"
    }

    if ($WhatIf) {
        Write-Host "Would publish: $platform ($Configuration)"
        continue
    }

    Write-Host "Publishing portable $platform ($Configuration)..."
    & $publishScript -Platform $platform -Configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "portable publish failed for $platform with exit code $LASTEXITCODE"
    }
}

if ($WhatIf) {
    Write-Host "Portable publish dry run completed."
} else {
    Write-Host "All portable publishes completed."
}
