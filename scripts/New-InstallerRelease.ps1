param(
    [ValidatePattern("^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")]
    [string]$Version = "0.6.1",

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

$targetFramework = "net11.0-windows10.0.26100.0"
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
$uninstallInclude = Join-Path $releaseDirectory (".uninstall-" + [Guid]::NewGuid().ToString("N") + ".nsh")
# Match the NSIS payload and remove its files without deleting unrelated files in the install directory.
$uninstallCommands = @(
    Get-ChildItem -LiteralPath $publishDirectory -File -Recurse -Exclude "*.pdb" | ForEach-Object {
        $relativePath = $_.FullName.Substring($publishDirectory.Length + 1).Replace('$', '$$')
        'Delete "$INSTDIR\{0}"' -f $relativePath
    }
    Get-ChildItem -LiteralPath $publishDirectory -Directory -Recurse |
        Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
            $relativePath = $_.FullName.Substring($publishDirectory.Length + 1).Replace('$', '$$')
            'RMDir "$INSTDIR\{0}"' -f $relativePath
        }
)

try {
    Set-Content -LiteralPath $uninstallInclude -Value $uninstallCommands -Encoding UTF8
    & $makeNsisPath `
        "/WX" `
        "/DAPP_VERSION=$Version" `
        "/DSOURCE_DIR=$publishDirectory" `
        "/DUNINSTALL_INCLUDE=$uninstallInclude" `
        "/DOUTPUT_DIR=$releaseDirectory" `
        "/DOUTPUT_BASE_NAME=$baseName" `
        $installerDefinition
    if ($LASTEXITCODE -ne 0) {
        throw "NSIS compilation failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item -LiteralPath $uninstallInclude -Force -ErrorAction SilentlyContinue
}
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer was not created at '$installerPath'."
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([System.IO.Path]::GetFileName($installerPath))" | Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Per-user installer created:"
Write-Host $installerPath
Write-Host $checksumPath
