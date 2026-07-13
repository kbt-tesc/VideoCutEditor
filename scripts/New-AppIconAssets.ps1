param(
    [string]$SourcePath = "",
    [string]$AssetsDirectory = "",
    [string]$MasterOutputPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    Join-Path $repoRoot "design\VideoCutEditor-icon-source.png"
} else {
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($SourcePath)
}
$assets = if ([string]::IsNullOrWhiteSpace($AssetsDirectory)) {
    Join-Path $repoRoot "src\VideoCutEditor\Assets"
} else {
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($AssetsDirectory)
}
$master = if ([string]::IsNullOrWhiteSpace($MasterOutputPath)) {
    Join-Path $repoRoot "design\VideoCutEditor-icon-master.png"
} else {
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($MasterOutputPath)
}

if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Icon source was not found at '$source'."
}

$magickCommand = Get-Command magick.exe -ErrorAction SilentlyContinue
$magickCandidates = @(
    $magickCommand.Source
    (Get-ChildItem "C:\Program Files\ImageMagick-*\magick.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_ -PathType Leaf) }
$magickPath = $magickCandidates | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($magickPath)) {
    throw "ImageMagick (magick.exe) was not found. Install ImageMagick.ImageMagick with winget."
}

function Invoke-Magick([string[]]$Arguments) {
    & $magickPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "ImageMagick failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Force -Path $assets | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $master) | Out-Null
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("VideoCutEditor-icon-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    $croppedPath = Join-Path $temporaryDirectory "cropped.png"
    $maskPath = Join-Path $temporaryDirectory "mask.png"
    $tilePath = Join-Path $temporaryDirectory "tile.png"

    # The source contains a rendered checkerboard. Crop to the dark tile and
    # apply a conservative rounded mask so no checkerboard reaches app assets.
    Invoke-Magick @($source, "-crop", "1000x1000+127+126", "+repage", "-filter", "Lanczos", "-resize", "1024x1024!", $croppedPath)
    Invoke-Magick @("-size", "1024x1024", "xc:black", "-fill", "white", "-draw", "roundrectangle 0,0 1023,1023 250,250", $maskPath)
    Invoke-Magick @($croppedPath, $maskPath, "-alpha", "off", "-compose", "CopyOpacity", "-composite", $tilePath)
    Invoke-Magick @(
        "-size", "1024x1024", "xc:none",
        "(", $tilePath, "-filter", "Lanczos", "-resize", "840x840!", ")",
        "-gravity", "center", "-composite",
        $master)

    $squareAssets = @(
        @{ Name = "LockScreenLogo.scale-200.png"; Size = "48x48" }
        @{ Name = "Square150x150Logo.scale-200.png"; Size = "300x300" }
        @{ Name = "Square44x44Logo.scale-200.png"; Size = "88x88" }
        @{ Name = "Square44x44Logo.targetsize-24_altform-unplated.png"; Size = "24x24" }
        @{ Name = "Square44x44Logo.targetsize-48_altform-lightunplated.png"; Size = "48x48" }
        @{ Name = "StoreLogo.png"; Size = "50x50" }
    )
    foreach ($asset in $squareAssets) {
        Invoke-Magick @($master, "-filter", "Lanczos", "-resize", ($asset.Size + "!"), (Join-Path $assets $asset.Name))
    }

    Invoke-Magick @(
        "-size", "620x300", "xc:none",
        "(", $master, "-filter", "Lanczos", "-resize", "260x260!", ")",
        "-gravity", "center", "-composite",
        (Join-Path $assets "Wide310x150Logo.scale-200.png"))
    Invoke-Magick @(
        "-size", "1240x600", "xc:none",
        "(", $master, "-filter", "Lanczos", "-resize", "420x420!", ")",
        "-gravity", "center", "-composite",
        (Join-Path $assets "SplashScreen.scale-200.png"))
    Invoke-Magick @(
        $master,
        "-define", "icon:auto-resize=256,128,64,48,40,32,24,20,16",
        (Join-Path $assets "AppIcon.ico"))
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}

Write-Host "VideoCutEditor icon assets created from:"
Write-Host $source
Write-Host $master
Write-Host $assets
