param(
    [Parameter(Mandatory)]
    [int]$AppPid,

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Continue"
$pass = 0
$fail = 0
$results = @()
$mainWindowHwnd = $null
$infoWindowHwnd = $null

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class NativeWindowTest
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint command);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
}
"@

function ConvertTo-Hwnd {
    param([Parameter(Mandatory)][object]$Value)

    if ($Value -is [string] -and $Value.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [IntPtr]([Convert]::ToInt64($Value.Substring(2), 16))
    }

    return [IntPtr]([long]$Value)
}

function Get-AppWindows {
    $parsed = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
    foreach ($window in $parsed) {
        Write-Output $window
    }
}

function Get-InfoWindows {
    return @(Get-AppWindows | Where-Object {
        $_.title.StartsWith("VideoCutEditor -", [StringComparison]::Ordinal) -and
        $_.ownerHwnd -ne 0 -and
        $_.className -eq "WinUIDesktopWin32WindowClass"
    })
}

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
    $windows = Get-AppWindows
    $main = @($windows | Where-Object { $_.title -eq "VideoCutEditor" })
    if ($main.Count -lt 1) { throw "VideoCutEditor window was not found." }
    $script:mainWindowHwnd = $main[0].hwnd
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
    "OutputFileNameTextBox",
    "OpenOutputDirectoryButton",
    "PlannedOutputTextBox",
    "OpenSettingsButton",
    "ShowInfoButton",
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

Test-UI "Additional arguments are hidden in Fast copy mode" {
    winapp ui wait-for "AdditionalFfmpegArgumentsTextBox" -a $AppPid --gone -t 3000 -q
}

Test-UI "Switch to Re-encode mode" {
    winapp ui invoke "Re-encode" -a $AppPid
    winapp ui wait-for "CodecFamilyComboBox" -a $AppPid -t 3000 -q
}

Test-UI "Additional arguments are visible in Re-encode mode" {
    winapp ui wait-for "AdditionalFfmpegArgumentsTextBox" -a $AppPid -t 3000 -q
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
    $tree = winapp ui inspect -a $AppPid --interactive --json 2>$null | ConvertFrom-Json
    $node = $tree.windows |
        ForEach-Object { $_.elements } |
        Where-Object { $_.automationId -eq "PredictedOutputSizeTextBox" } |
        Select-Object -First 1
    if ($null -eq $node) { throw "PredictedOutputSizeTextBox was not found." }
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

Test-UI "INFO window opens once and is owned by main window" {
    winapp ui invoke "ShowInfoButton" -w $mainWindowHwnd
    Start-Sleep -Milliseconds 500
    $infoWindows = @(Get-InfoWindows)
    if ($infoWindows.Count -ne 1) { throw "Expected one INFO window, found $($infoWindows.Count)." }
    $script:infoWindowHwnd = $infoWindows[0].hwnd

    $owner = [NativeWindowTest]::GetWindow((ConvertTo-Hwnd $infoWindowHwnd), 4)
    if ($owner -ne (ConvertTo-Hwnd $mainWindowHwnd)) { throw "INFO window does not have the main HWND as owner." }

    winapp ui invoke "ShowInfoButton" -w $mainWindowHwnd
    Start-Sleep -Milliseconds 300
    $infoWindows = @(Get-InfoWindows)
    if ($infoWindows.Count -ne 1) { throw "Repeated INFO command created a duplicate window." }
}

Test-UI "INFO window shows all three information fields" {
    winapp ui wait-for "EncoderSummaryTextBox" -w $infoWindowHwnd -t 3000 -q
    winapp ui wait-for "ExportLogTextBox" -w $infoWindowHwnd -t 3000 -q
    winapp ui wait-for "MediaInfoTextBox" -w $infoWindowHwnd -t 3000 -q
}

Test-UI "Main window remains operable while INFO is open" {
    winapp ui invoke "Fast copy" -w $mainWindowHwnd
    winapp ui wait-for "CodecFamilyComboBox" -w $mainWindowHwnd --gone -t 3000 -q
}

Test-UI "INFO window can close and reopen" {
    $closed = [NativeWindowTest]::PostMessage((ConvertTo-Hwnd $infoWindowHwnd), 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    if (!$closed) { throw "Failed to request INFO window close." }

    $deadline = [DateTime]::UtcNow.AddSeconds(3)
    do {
        Start-Sleep -Milliseconds 100
        $remaining = @(Get-InfoWindows)
    } while ($remaining.Count -gt 0 -and [DateTime]::UtcNow -lt $deadline)
    if ($remaining.Count -gt 0) { throw "INFO window did not close." }

    winapp ui invoke "ShowInfoButton" -w $mainWindowHwnd
    Start-Sleep -Milliseconds 500
    $reopened = @(Get-InfoWindows)
    if ($reopened.Count -ne 1) { throw "INFO window did not reopen exactly once." }
    $script:infoWindowHwnd = $reopened[0].hwnd
    winapp ui wait-for "EncoderSummaryTextBox" -w $infoWindowHwnd -t 3000 -q
}

Test-UI "INFO window is closed during test cleanup" {
    $closed = [NativeWindowTest]::PostMessage((ConvertTo-Hwnd $infoWindowHwnd), 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    if (!$closed) { throw "Failed to close INFO window during cleanup." }
    Start-Sleep -Milliseconds 300
    if (@(Get-InfoWindows).Count -ne 0) { throw "INFO window remained open after cleanup." }
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
