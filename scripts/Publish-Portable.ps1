param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\VideoCutEditor\VideoCutEditor.csproj"
$profileName = "win-$Platform"

if (!(Test-Path $projectPath)) {
    throw "VideoCutEditor project was not found at $projectPath"
}

dotnet publish $projectPath `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:PublishProfile=$profileName `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$targetFramework = "net10.0-windows10.0.26100.0"
$runtimeIdentifier = "win-$Platform"
$publishDirectory = Join-Path $repoRoot "src\VideoCutEditor\bin\$Configuration\$targetFramework\$runtimeIdentifier\publish"
$exePath = Join-Path $publishDirectory "VideoCutEditor.exe"

if (!(Test-Path $exePath)) {
    throw "Publish completed but VideoCutEditor.exe was not found at $exePath"
}

Write-Host "Portable publish completed:"
Write-Host $publishDirectory
Write-Host $exePath
