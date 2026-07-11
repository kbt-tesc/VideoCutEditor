param(
    [ValidateSet("FastCopy", "Reencode", "NormalizeAudio")]
    [string]$Mode = "FastCopy",
    [string]$FfmpegPath = "",
    [string]$FfprobePath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $env:TEMP ("VideoCutEditor-E2E-" + [Guid]::NewGuid().ToString("N"))
$settingsDirectory = Join-Path $testRoot "settings"
$outputDirectory = Join-Path $testRoot "output"
$mediaDirectory = Join-Path $testRoot "media"
$appProcess = $null
$previousSettingsDirectory = $env:VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY

try {
    New-Item -ItemType Directory -Force -Path $settingsDirectory, $outputDirectory | Out-Null
    $ffmpeg = if ([string]::IsNullOrWhiteSpace($FfmpegPath)) { (Get-Command ffmpeg -ErrorAction Stop).Source } else { (Resolve-Path -LiteralPath $FfmpegPath).Path }
    $ffprobe = if ([string]::IsNullOrWhiteSpace($FfprobePath)) { (Get-Command ffprobe -ErrorAction Stop).Source } else { (Resolve-Path -LiteralPath $FfprobePath).Path }

    & powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\New-SampleMedia.ps1") -OutputDirectory $mediaDirectory -FfmpegPath $ffmpeg -DurationSeconds 4 -Force
    if ($LASTEXITCODE -ne 0) { throw "Sample media generation failed." }

    $encoderKind = if ($Mode -eq "Reencode") { "Software" } else { "Auto" }
    $videoBitrate = if ($Mode -eq "Reencode") { 1500 } else { 2500 }
    $exportMode = if ($Mode -eq "Reencode") { "Reencode" } else { "FastCopy" }
    $normalizeAudio = $Mode -eq "NormalizeAudio"

    @{
        ffmpegPath = $ffmpeg
        ffprobePath = $ffprobe
        outputDirectory = $outputDirectory
        lastExportMode = $exportMode
        lastCodecFamily = "H264"
        lastEncoderKind = $encoderKind
        lastBitrateMode = "Bitrate"
        lastVideoBitrateKbps = $videoBitrate
        normalizeAudio = $normalizeAudio
    } | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $settingsDirectory "settings.json")

    & dotnet build (Join-Path $root "src\VideoCutEditor\VideoCutEditor.csproj") -p:Platform=x64 -p:WindowsPackageType=None
    if ($LASTEXITCODE -ne 0) { throw "Debug x64 build failed." }

    $env:VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY = $settingsDirectory
    $exe = Join-Path $root "src\VideoCutEditor\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\VideoCutEditor.exe"
    $appProcess = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 2

    $sampleName = if ($Mode -eq "NormalizeAudio") { "quiet-audio.mp4" } else { "video-with-audio.mp4" }
    & powershell -ExecutionPolicy Bypass -File (Join-Path $root "tests\ui-tests.ps1") -AppPid $appProcess.Id -SampleVideoPath (Join-Path $mediaDirectory $sampleName) -VerifyExportMode $Mode -ExpectedOutputDirectory $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw "$Mode UI verification failed." }
}
finally {
    if ($null -ne $appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    }
    $env:VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY = $previousSettingsDirectory
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
