param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$PublishDirectory
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$targetFramework = "net10.0-windows10.0.26100.0"
$runtimeIdentifier = "win-$Platform"

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $repoRoot "src\VideoCutEditor\bin\$Configuration\$targetFramework\$runtimeIdentifier\publish"
}

if (!(Test-Path $PublishDirectory)) {
    throw "Publish directory was not found: $PublishDirectory"
}

$exePath = Join-Path $PublishDirectory "VideoCutEditor.exe"
if (!(Test-Path $exePath)) {
    throw "VideoCutEditor.exe was not found in $PublishDirectory"
}

$files = @(Get-ChildItem -LiteralPath $PublishDirectory -File)
$unexpectedExecutables = @($files | Where-Object { $_.Extension -ieq ".exe" -and $_.Name -ine "VideoCutEditor.exe" })
if ($unexpectedExecutables.Count -gt 0) {
    $names = ($unexpectedExecutables | ForEach-Object { $_.Name }) -join ", "
    throw "Portable publish contains unexpected executable sidecars: $names"
}

$blockedExtensions = @(".dll", ".json", ".xbf", ".pri", ".winmd", ".appx", ".msix")
$unexpectedSidecars = @($files | Where-Object { $blockedExtensions -icontains $_.Extension })
if ($unexpectedSidecars.Count -gt 0) {
    $names = ($unexpectedSidecars | ForEach-Object { $_.Name }) -join ", "
    throw "Portable publish contains files that should be inside the single-file EXE: $names"
}

$bundledTools = @($files | Where-Object { $_.Name -imatch "^ff(mpeg|probe)(\.exe)?$" })
if ($bundledTools.Count -gt 0) {
    $names = ($bundledTools | ForEach-Object { $_.Name }) -join ", "
    throw "Portable publish must not bundle external ffmpeg tools: $names"
}

Write-Host "Portable publish validation passed:"
Write-Host $PublishDirectory
Write-Host $exePath
