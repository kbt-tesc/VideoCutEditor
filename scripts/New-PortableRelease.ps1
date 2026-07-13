param(
    [ValidatePattern("^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")]
    [string]$Version = "0.2.0",

    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputDirectory = "",

    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $repoRoot "artifacts\releases"
} else {
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
}
$baseName = "VideoCutEditor-$Version-win-$Platform"
$zipPath = Join-Path $releaseDirectory "$baseName.zip"
$checksumPath = "$zipPath.sha256"

if ($WhatIf) {
    Write-Host "Would create: $zipPath"
    Write-Host "Would create: $checksumPath"
    exit 0
}

$publishScript = Join-Path $PSScriptRoot "Publish-Portable.ps1"
& $publishScript -Platform $Platform -Configuration $Configuration -Version $Version
if ($LASTEXITCODE -ne 0) {
    throw "Portable publish failed with exit code $LASTEXITCODE"
}

$targetFramework = "net10.0-windows10.0.26100.0"
$publishDirectory = Join-Path $repoRoot "src\VideoCutEditor\bin\$Configuration\$targetFramework\win-$Platform\publish"
$exePath = Join-Path $publishDirectory "VideoCutEditor.exe"
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "Published executable was not found at '$exePath'."
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$stagingDirectory = Join-Path $releaseDirectory ("." + $baseName + "-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $stagingDirectory | Out-Null
    Copy-Item -LiteralPath $exePath -Destination (Join-Path $stagingDirectory "VideoCutEditor.exe")

    $readmePath = Join-Path $repoRoot "distribution\README.txt"
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
        throw "Portable release README was not found at '$readmePath'."
    }
    Copy-Item -LiteralPath $readmePath -Destination (Join-Path $stagingDirectory "README.txt")

    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $checksumPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $checksumPath -Encoding ascii
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Host "Portable release created:"
Write-Host $zipPath
Write-Host $checksumPath
