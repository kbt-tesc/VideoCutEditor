param(
    [ValidatePattern("^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")]
    [string]$Version = "0.5.1",

    [ValidateSet("x64")]
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
$baseName = "VideoCutEditor-$Version-win-$Platform-setup"
$installerPath = Join-Path $releaseDirectory "$baseName.exe"
$checksumPath = "$installerPath.sha256"

if ($WhatIf) {
    Write-Host "Would create: $installerPath"
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
$makeNsisCommand = Get-Command makensis.exe -ErrorAction SilentlyContinue
$makeNsisCandidates = @(
    $makeNsisCommand.Source
    (Join-Path ${env:ProgramFiles(x86)} "NSIS\makensis.exe")
    (Join-Path $env:ProgramFiles "NSIS\makensis.exe")
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_ -PathType Leaf) }
$makeNsisPath = $makeNsisCandidates | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($makeNsisPath)) {
    throw "NSIS compiler (makensis.exe) was not found. Install NSIS.NSIS with winget."
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $checksumPath -Force -ErrorAction SilentlyContinue

$installerDefinition = Join-Path $repoRoot "installer\VideoCutEditor.nsi"
& $makeNsisPath `
    "/WX" `
    "/DAPP_VERSION=$Version" `
    "/DSOURCE_DIR=$publishDirectory" `
    "/DOUTPUT_DIR=$releaseDirectory" `
    "/DOUTPUT_BASE_NAME=$baseName" `
    $installerDefinition
if ($LASTEXITCODE -ne 0) {
    throw "NSIS compilation failed with exit code $LASTEXITCODE"
}
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer was not created at '$installerPath'."
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([System.IO.Path]::GetFileName($installerPath))" | Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Per-user installer created:"
Write-Host $installerPath
Write-Host $checksumPath
