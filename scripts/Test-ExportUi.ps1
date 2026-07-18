param(
    [ValidateSet("FastCopy", "FastCopyMultiClip", "Reencode", "ReencodeHdrToSdr", "ReencodeNvenc", "ReencodeNvencQuality", "ReencodeNvencHevc", "ReencodeNvencHevcQuality", "ReencodeNvencAv1", "ReencodeNvencAv1Quality", "NormalizeAudio", "NormalizeNoAudio")]
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

    $isNvenc = $Mode -like "ReencodeNvenc*"
    $isReencode = $Mode -in @("Reencode", "ReencodeHdrToSdr") -or $isNvenc
    $isQuality = $Mode -like "*Quality"

    if ($isNvenc) {
        $encoders = & $ffmpeg -hide_banner -encoders 2>&1
        $requiredEncoder = switch ($Mode) {
            { $_ -like "ReencodeNvencHevc*" } { "hevc_nvenc" }
            { $_ -like "ReencodeNvencAv1*" } { "av1_nvenc" }
            default { "h264_nvenc" }
        }
        if ($LASTEXITCODE -ne 0 -or -not ($encoders -match "\b$requiredEncoder\b")) {
            throw "The selected ffmpeg build does not expose $requiredEncoder."
        }
    }

    $encoderKind = if ($Mode -in @("Reencode", "ReencodeHdrToSdr")) { "Software" } elseif ($isNvenc) { "Nvenc" } else { "Auto" }
    $videoBitrate = if ($isReencode) { 1500 } else { 2500 }
    $exportMode = if ($isReencode) { "Reencode" } else { "FastCopy" }
    $bitrateMode = if ($isQuality) { "Quality" } else { "Bitrate" }
    $normalizeAudio = $Mode -in @("NormalizeAudio", "NormalizeNoAudio")
    $codecFamily = switch ($Mode) {
        "ReencodeNvencHevc" { "H265" }
        "ReencodeNvencHevcQuality" { "H265" }
        "ReencodeNvencAv1" { "Av1" }
        "ReencodeNvencAv1Quality" { "Av1" }
        default { "H264" }
    }

    @{
        ffmpegPath = $ffmpeg
        ffprobePath = $ffprobe
        outputDirectory = $outputDirectory
        lastExportMode = $exportMode
        lastCodecFamily = $codecFamily
        lastEncoderKind = $encoderKind
        lastBitrateMode = $bitrateMode
        lastVideoBitrateKbps = $videoBitrate
        lastQualityValue = 23
        normalizeAudio = $normalizeAudio
        convertHdrToSdr = $Mode -eq "ReencodeHdrToSdr"
    } | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $settingsDirectory "settings.json")

    & dotnet build (Join-Path $root "src\VideoCutEditor\VideoCutEditor.csproj") -p:Platform=x64 -p:WindowsPackageType=None
    if ($LASTEXITCODE -ne 0) { throw "Debug x64 build failed." }

    $env:VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY = $settingsDirectory
    $exe = Join-Path $root "src\VideoCutEditor\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\VideoCutEditor.exe"
    $appProcess = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 2

    $sampleName = switch ($Mode) {
        "ReencodeHdrToSdr" { "hdr-pq.mp4" }
        "NormalizeAudio" { "quiet-audio.mp4" }
        "NormalizeNoAudio" { "video-only.mp4" }
        default { "video-with-audio.mp4" }
    }
    & powershell -ExecutionPolicy Bypass -File (Join-Path $root "tests\ui-tests.ps1") -AppPid $appProcess.Id -SampleVideoPath (Join-Path $mediaDirectory $sampleName) -VerifyExportMode $Mode -ExpectedOutputDirectory $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw "$Mode UI verification failed." }

    if ($Mode -eq "ReencodeHdrToSdr") {
        $outputFile = Get-ChildItem -LiteralPath $outputDirectory -File | Select-Object -First 1
        if ($null -eq $outputFile) { throw "HDR to SDR verification output was not found." }
        $colorMetadata = & $ffprobe -v error -select_streams v:0 -show_entries stream=color_space,color_transfer,color_primaries -of default=noprint_wrappers=1 $outputFile.FullName
        if ($LASTEXITCODE -ne 0) { throw "ffprobe failed while verifying HDR to SDR output." }
        $metadataText = $colorMetadata -join "`n"
        foreach ($expectedValue in @("color_space=bt709", "color_transfer=bt709", "color_primaries=bt709")) {
            if ($metadataText -notmatch "(?m)^$([regex]::Escape($expectedValue))$") {
                throw "HDR to SDR output did not report $expectedValue. Actual metadata:`n$metadataText"
            }
        }
    }
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
