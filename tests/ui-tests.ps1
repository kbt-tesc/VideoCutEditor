param(
    [Parameter(Mandatory)]
    [int]$AppPid,

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Continue"
$pass = 0
$fail = 0
$results = @()

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { Split-Path -Parent $MyInvocation.MyCommand.Path } else { $PSScriptRoot }
    $OutputDirectory = Join-Path $scriptRoot "ui-results"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$screenshots = Join-Path $OutputDirectory "screenshots"
New-Item -ItemType Directory -Force -Path $screenshots | Out-Null

function Test-UI {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [scriptblock]$Script
    )

    $global:LASTEXITCODE = 0
    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:pass++
            $script:results += @{ name = $Name; status = "PASS" }
        }
        else {
            $script:fail++
            $script:results += @{ name = $Name; status = "FAIL"; detail = "$output" }
        }
    }
    catch {
        $script:fail++
        $script:results += @{ name = $Name; status = "FAIL"; detail = "$_" }
    }
}

Test-UI "App has a main window" {
    $windows = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
    $main = @($windows | Where-Object { $_.title -eq "VideoCutEditor" })
    if ($main.Count -lt 1) { throw "VideoCutEditor window was not found." }
}

$expectedElements = @(
    "OpenVideoButton",
    "StatusMessageText",
    "PlayPauseButton",
    "MarkStartButton",
    "MarkEndButton",
    "TimelineScrollViewer",
    "TimelineZoomSlider",
    "WaveformImage",
    "PlaybackRateComboBox",
    "RangeStartTextBox",
    "RangeEndTextBox",
    "ExportModeRadioButtons",
    "NormalizeAudioCheckBox",
    "FfmpegPathTextBox",
    "BrowseFfmpegPathButton",
    "FfprobePathTextBox",
    "BrowseFfprobePathButton",
    "OutputDirectoryTextBox",
    "BrowseOutputDirectoryButton",
    "PlannedOutputTextBox",
    "ExportLogTextBox",
    "EncoderSummaryTextBox",
    "MediaInfoTextBox",
    "SaveSettingsButton",
    "CancelExportButton",
    "ExportButton"
)

foreach ($element in $expectedElements) {
    Test-UI "$element exists" {
        winapp ui wait-for $element -a $AppPid -t 3000 -q
    }
}

Test-UI "Export mode selector is enabled" {
    winapp ui wait-for "ExportModeRadioButtons" -a $AppPid -p IsEnabled --value "True" -t 3000 -q
}

Test-UI "Select Fast copy mode" {
    winapp ui invoke "Fast copy" -a $AppPid
    winapp ui wait-for "CodecFamilyComboBox" -a $AppPid --gone -t 3000 -q
}

Test-UI "Normalize audio defaults off" {
    winapp ui wait-for "NormalizeAudioCheckBox" -a $AppPid --value "Off" -t 3000 -q
}

Test-UI "Normalize audio remains available in Fast copy mode" {
    winapp ui wait-for "NormalizeAudioCheckBox" -a $AppPid -p IsEnabled --value "True" -t 3000 -q
}

Test-UI "Codec is hidden in Fast copy mode" {
    winapp ui wait-for "CodecFamilyComboBox" -a $AppPid --gone -t 3000 -q
}

Test-UI "Rate control is hidden in Fast copy mode" {
    winapp ui wait-for "BitrateModeComboBox" -a $AppPid --gone -t 3000 -q
}

Test-UI "Fades are hidden in Fast copy mode" {
    winapp ui wait-for "VideoFadeInCheckBox" -a $AppPid --gone -t 3000 -q
}

Test-UI "Switch to Re-encode mode" {
    winapp ui invoke "Re-encode" -a $AppPid
    winapp ui wait-for "CodecFamilyComboBox" -a $AppPid -t 3000 -q
}

Test-UI "Normalize audio remains available in Re-encode mode" {
    winapp ui wait-for "NormalizeAudioCheckBox" -a $AppPid -p IsEnabled --value "True" -t 3000 -q
}

Test-UI "Codec defaults to H.264 in Re-encode mode" {
    winapp ui wait-for "CodecFamilyComboBox" -a $AppPid --value "H.264" -t 3000 -q
}

Test-UI "Encoder defaults to Auto in Re-encode mode" {
    winapp ui wait-for "EncoderKindComboBox" -a $AppPid --value "Auto" -t 3000 -q
}

Test-UI "Rate control defaults to video bitrate in Re-encode mode" {
    winapp ui wait-for "BitrateModeComboBox" -a $AppPid --value "Video bitrate" -t 3000 -q
}

Test-UI "Video bitrate defaults to 2500 in Re-encode mode" {
    winapp ui wait-for "VideoBitrateTextBox" -a $AppPid --value "2500" -t 3000 -q
}

Test-UI "Target size is disabled in bitrate mode in Re-encode mode" {
    winapp ui wait-for "TargetSizeNumberBox" -a $AppPid -p IsEnabled --value "False" -t 3000 -q
}

Test-UI "Quality is disabled in bitrate mode in Re-encode mode" {
    winapp ui wait-for "QualityNumberBox" -a $AppPid -p IsEnabled --value "False" -t 3000 -q
}

Test-UI "Predicted output size starts unavailable" {
    winapp ui wait-for "PredictedOutputSizeTextBox" -a $AppPid --value "Estimated size unavailable" -t 3000 -q
}

Test-UI "Video fade in defaults off" {
    winapp ui wait-for "VideoFadeInCheckBox" -a $AppPid --value "Off" -t 3000 -q
}

Test-UI "Video fade out defaults off" {
    winapp ui wait-for "VideoFadeOutCheckBox" -a $AppPid --value "Off" -t 3000 -q
}

Test-UI "Audio fade in defaults off" {
    winapp ui wait-for "AudioFadeInCheckBox" -a $AppPid --value "Off" -t 3000 -q
}

Test-UI "Audio fade out defaults off" {
    winapp ui wait-for "AudioFadeOutCheckBox" -a $AppPid --value "Off" -t 3000 -q
}

Test-UI "Cancel is disabled when idle" {
    winapp ui wait-for "CancelExportButton" -a $AppPid -p IsEnabled --value "False" -t 3000 -q
}

Test-UI "Export is enabled when idle" {
    winapp ui wait-for "ExportButton" -a $AppPid -p IsEnabled --value "True" -t 3000 -q
}

Test-UI "Range start defaults to zero" {
    winapp ui wait-for "RangeStartTextBox" -a $AppPid --value "00:00:00" -t 3000 -q
}

Test-UI "Range end defaults to zero" {
    winapp ui wait-for "RangeEndTextBox" -a $AppPid --value "00:00:00" -t 3000 -q
}

Test-UI "Initial screenshot captured" {
    winapp ui screenshot -a $AppPid -o (Join-Path $screenshots "01-initial.png") 2>$null
}

Test-UI "UI automation tree can be inspected" {
    $inspectPath = Join-Path $OutputDirectory "inspect.json"
    winapp ui inspect -a $AppPid --interactive --json 2>$null | Out-File -Encoding utf8 $inspectPath
    if (!(Test-Path $inspectPath) -or (Get-Item $inspectPath).Length -eq 0) {
        throw "inspect.json was not created."
    }
}

Write-Host ""
Write-Host "Passed: $pass | Failed: $fail"
$results | Where-Object { $_.status -eq "FAIL" } | ForEach-Object {
    Write-Host "  FAIL: $($_.name) - $($_.detail)" -ForegroundColor Red
}

$results | ConvertTo-Json -Depth 4 | Out-File -Encoding utf8 (Join-Path $OutputDirectory "test-results.json")

if ($fail -gt 0) {
    exit 1
}

exit 0
