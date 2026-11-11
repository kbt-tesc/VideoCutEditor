param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$PublishDirectory
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$targetFramework = "net11.0-windows10.0.26100.0"
$runtimeIdentifier = "win-$Platform"

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $repoRoot "src\VideoCutEditor\bin\$Configuration\$targetFramework\$runtimeIdentifier\publish"
}

if (!(Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
    throw "Publish directory was not found: $PublishDirectory"
}

$requiredFiles = @(
    "VideoCutEditor.exe"
    "VideoCutEditor.dll"
    "VideoCutEditor.deps.json"
    "VideoCutEditor.runtimeconfig.json"
    "Microsoft.UI.Xaml.dll"
    "Microsoft.WindowsAppRuntime.dll"
    "VideoCutEditor.pri"
)
foreach ($name in $requiredFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $PublishDirectory $name) -PathType Leaf)) {
        throw "$name was not found in $PublishDirectory"
    }
}

$runtimeConfig = Get-Content -LiteralPath (Join-Path $PublishDirectory "VideoCutEditor.runtimeconfig.json") -Raw | ConvertFrom-Json
$options = $runtimeConfig.runtimeOptions
$netRuntime = @($options.framework) + @($options.frameworks) | Where-Object {
    $_.name -eq "Microsoft.NETCore.App" -and $_.version -match '^11\.0\.\d+$'
}
if (!$netRuntime -or $options.includedFrameworks) {
    throw "VideoCutEditor.runtimeconfig.json must depend on a separately installed .NET 11 runtime."
}

$deps = Get-Content -LiteralPath (Join-Path $PublishDirectory "VideoCutEditor.deps.json") -Raw | ConvertFrom-Json
if ($deps.libraries.PSObject.Properties.Name -match '^runtimepack\.Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.') {
    throw "VideoCutEditor.deps.json lists a bundled .NET runtime pack."
}

$unexpectedFiles = @(Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File | Where-Object {
    $_.Name -match '^(coreclr|clrjit|hostfxr|hostpolicy|System\.Private\.CoreLib)\.dll$|^(dotnet\.exe|ff(mpeg|probe)(\.exe)?)$' -or
    $_.Extension -in '.appx', '.appxbundle', '.msix', '.msixbundle'
})
if ($unexpectedFiles.Count -gt 0) {
    $names = ($unexpectedFiles | ForEach-Object { $_.Name }) -join ", "
    throw "Portable publish contains unexpected runtime, package, or tool files: $names"
}

Write-Host "Portable publish validation passed:"
Write-Host $PublishDirectory
