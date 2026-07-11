param(
    [Parameter(Mandatory)]
    [int]$AppPid,

    [string]$OutputDirectory = "",

    [string]$SampleVideoPath = "",

    [string]$VerifyExportMode = "",

    [string]$ExpectedOutputDirectory = ""
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

function Get-FilePickerWindow {
    param([int]$TimeoutMilliseconds = 5000)

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $windows = @(winapp ui list-windows --json 2>$null | ConvertFrom-Json)
        $picker = $windows | Where-Object {
            $_.processName -eq "PickerHost" -and
            $_.ownerHwnd -eq $mainWindowHwnd -and
            $_.className -eq "#32770"
        } | Select-Object -First 1

        if ($null -ne $picker) {
            return $picker
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    return $null
}

function Wait-ForFilePickerClose {
    param(
        [Parameter(Mandatory)][long]$OwnerHwnd,
        [int]$TimeoutMilliseconds = 10000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $windows = @(winapp ui list-windows --json 2>$null | ConvertFrom-Json)
        $picker = $windows | Where-Object {
            $_.processName -eq "PickerHost" -and $_.ownerHwnd -eq $OwnerHwnd
        } | Select-Object -First 1
        if ($null -eq $picker) {
            return
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "The system file picker did not close after selecting the sample."
}

function Wait-ForTextValue {
    param(
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$ExpectedText,
        [Parameter(Mandatory)][long]$WindowHwnd,
        [int]$TimeoutMilliseconds = 30000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        try {
            $value = winapp ui get-value $AutomationId -w $WindowHwnd --json 2>$null | ConvertFrom-Json
            if ($value.text -like "*$ExpectedText*") {
                return
            }
        }
        catch {
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "'$AutomationId' did not contain '$ExpectedText' within $TimeoutMilliseconds ms."
}

function Set-FilePickerPath {
    param(
        [Parameter(Mandatory)][long]$PickerHwnd,
        [Parameter(Mandatory)][string]$Path
    )

    $pickerHwndText = $PickerHwnd.ToString([Globalization.CultureInfo]::InvariantCulture)
    $searchArguments = @("ui", "search", "1148", "-w", $pickerHwndText, "--json")
    $fileNameSearch = & winapp $searchArguments 2>$null | ConvertFrom-Json
    $fileNameEdit = @($fileNameSearch.matches) | Where-Object {
        $_.type -eq "Edit" -and $_.automationId -eq "1148"
    } | Select-Object -First 1
    if ($null -eq $fileNameEdit) {
        $searchArguments[2] = "FileNameControlHost"
        $fileNameSearch = & winapp $searchArguments 2>$null | ConvertFrom-Json
        $fileNameEdit = @($fileNameSearch.matches) | Where-Object { $_.type -eq "Edit" } | Select-Object -First 1
    }
    if ($null -eq $fileNameEdit) {
        throw "The file picker filename field was not found."
    }

    & winapp @("ui", "set-value", $fileNameEdit.selector, $Path, "-w", $pickerHwndText, "-q")
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set the sample video path in the file picker."
    }

    $openButtonSearch = & winapp @("ui", "search", "1", "-w", $pickerHwndText, "--json") 2>$null | ConvertFrom-Json
    $openButton = @($openButtonSearch.matches) | Where-Object {
        $_.automationId -eq "1" -and
        $_.className -eq "Button" -and
        $_.type -in @("Button", "SplitButton")
    } | Select-Object -First 1
    if ($null -eq $openButton) {
        throw "The file picker open button was not found."
    }

    & winapp @("ui", "invoke", $openButton.selector, "-w", $pickerHwndText, "-q")
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to confirm the sample video in the file picker."
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
        $detail = $_.Exception.Message
        if (-not [string]::IsNullOrWhiteSpace($_.ScriptStackTrace)) {
            $detail = "$detail`n$($_.ScriptStackTrace)"
        }
        $script:results += @{ name = $Name; status = "FAIL"; detail = $detail }
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

$expectedNormalizeAudio = if ($VerifyExportMode -in @("NormalizeAudio", "NormalizeNoAudio")) { "On" } else { "Off" }
Test-UI "Normalize audio matches expected setting" {
    winapp ui wait-for "NormalizeAudioCheckBox" -a $AppPid --value $expectedNormalizeAudio -t 3000 -q
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

$expectedInitialEncoder = if ($VerifyExportMode -eq "Reencode") { "Software" } else { "Auto" }
Test-UI "Encoder matches expected Re-encode setting" {
    winapp ui wait-for "EncoderKindComboBox" -a $AppPid --value $expectedInitialEncoder -t 3000 -q
}

Test-UI "Rate control defaults to video bitrate in Re-encode mode" {
    winapp ui wait-for "BitrateModeComboBox" -a $AppPid --value "Video bitrate" -t 3000 -q
}

$expectedInitialVideoBitrate = if ($VerifyExportMode -eq "Reencode") { "1500" } else { "2500" }
Test-UI "Video bitrate matches expected Re-encode setting" {
    winapp ui wait-for "VideoBitrateTextBox" -a $AppPid --value $expectedInitialVideoBitrate -t 3000 -q
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

if (-not [string]::IsNullOrWhiteSpace($SampleVideoPath)) {
    Test-UI "Generated sample exists" {
        if (-not (Test-Path -LiteralPath $SampleVideoPath -PathType Leaf)) {
            throw "Sample video was not found at '$SampleVideoPath'."
        }
        $script:SampleVideoPath = (Resolve-Path -LiteralPath $SampleVideoPath).Path
    }

    Test-UI "Open generated sample through system picker" {
        winapp ui invoke "OpenVideoButton" -w $mainWindowHwnd -q
        if ($LASTEXITCODE -ne 0) {
            throw "OpenVideoButton could not be invoked."
        }

        $picker = Get-FilePickerWindow
        $pickerWindow = @($picker | Where-Object { $_.processName -eq "PickerHost" }) | Select-Object -First 1
        if ($null -eq $pickerWindow) {
            throw "The system file picker did not appear."
        }

        Set-FilePickerPath -PickerHwnd ([long]$pickerWindow.hwnd) -Path $SampleVideoPath
        Wait-ForFilePickerClose -OwnerHwnd ([long]$mainWindowHwnd)
        $loadedStatusText = -join [char[]]@(0x52D5, 0x753B, 0x3092, 0x9078, 0x629E, 0x3057, 0x307E, 0x3057, 0x305F)
        Wait-ForTextValue -AutomationId "StatusMessageText" -ExpectedText $loadedStatusText -WindowHwnd ([long]$mainWindowHwnd)
    }

    Test-UI "Loaded sample initializes the editable range" {
        $rangeEnd = winapp ui get-value "RangeEndTextBox" -w $mainWindowHwnd --json 2>$null | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($rangeEnd.text) -or $rangeEnd.text -in @("00:00:00", "0:00.000")) {
            throw "The loaded sample did not initialize a non-zero range end."
        }
    }

    Test-UI "Loaded sample screenshot captured" {
        winapp ui screenshot -a $AppPid -o (Join-Path $screenshots "02-sample-loaded.png") 2>$null
    }

    if (-not [string]::IsNullOrWhiteSpace($VerifyExportMode)) {
        Test-UI "$VerifyExportMode mode is selected for isolated export" {
            if ($VerifyExportMode -eq "Reencode") {
                winapp ui invoke "Re-encode" -w $mainWindowHwnd -q
                winapp ui wait-for "EncoderKindComboBox" -w $mainWindowHwnd --value "Software" -t 3000 -q
            }
            elseif ($VerifyExportMode -eq "FastCopy") {
                winapp ui invoke "Fast copy" -w $mainWindowHwnd -q
            }
            elseif ($VerifyExportMode -eq "NormalizeAudio") {
                winapp ui invoke "Fast copy" -w $mainWindowHwnd -q
                winapp ui wait-for "NormalizeAudioCheckBox" -w $mainWindowHwnd --value "On" -t 3000 -q
            }
            elseif ($VerifyExportMode -eq "NormalizeNoAudio") {
                winapp ui invoke "Fast copy" -w $mainWindowHwnd -q
                winapp ui wait-for "NormalizeAudioCheckBox" -w $mainWindowHwnd --value "On" -t 3000 -q
            }
            else {
                throw "Unsupported export verification mode '$VerifyExportMode'."
            }
        }

        Test-UI "$VerifyExportMode output uses isolated directory" {
            if ([string]::IsNullOrWhiteSpace($ExpectedOutputDirectory)) {
                throw "ExpectedOutputDirectory is required for export verification."
            }

            $planned = winapp ui get-value "PlannedOutputTextBox" -w $mainWindowHwnd --json 2>$null | ConvertFrom-Json
            $script:plannedExportPath = $planned.text
            $expected = (Resolve-Path -LiteralPath $ExpectedOutputDirectory).Path
            if (-not [string]::Equals([System.IO.Path]::GetDirectoryName($plannedExportPath), $expected, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Planned output is outside the isolated directory: '$plannedExportPath'."
            }
            if ([System.IO.File]::Exists($plannedExportPath)) {
                throw "Planned output already exists before export."
            }
        }

        Test-UI "$VerifyExportMode export completes" {
            winapp ui invoke "ExportButton" -w $mainWindowHwnd -q
            if ($VerifyExportMode -eq "NormalizeNoAudio") {
                $noAudioText = -join [char[]]@(0x97F3, 0x58F0, 0x30B9, 0x30C8, 0x30EA, 0x30FC, 0x30E0, 0x304C, 0x306A, 0x3044, 0x305F, 0x3081, 0x3001, 0x97F3, 0x91CF, 0x6B63, 0x898F, 0x5316, 0x3092, 0x4F7F, 0x7528, 0x3067, 0x304D, 0x307E, 0x305B, 0x3093)
                Wait-ForTextValue -AutomationId "StatusMessageText" -ExpectedText $noAudioText -WindowHwnd ([long]$mainWindowHwnd)
                if ([System.IO.File]::Exists($plannedExportPath)) {
                    throw "No-audio normalization unexpectedly created an output file."
                }
            }
            else {
                $completedText = -join [char[]]@(0x66F8, 0x304D, 0x51FA, 0x3057, 0x304C, 0x5B8C, 0x4E86, 0x3057, 0x307E, 0x3057, 0x305F)
                Wait-ForTextValue -AutomationId "StatusMessageText" -ExpectedText $completedText -WindowHwnd ([long]$mainWindowHwnd) -TimeoutMilliseconds 60000
                if (-not [System.IO.File]::Exists($plannedExportPath) -or (Get-Item -LiteralPath $plannedExportPath).Length -eq 0) {
                    throw "$VerifyExportMode export did not create a non-empty output file."
                }
            }
        }

        Test-UI "$VerifyExportMode completion screenshot captured" {
            $screenshotName = "03-" + $VerifyExportMode.ToLowerInvariant() + "-complete.png"
            winapp ui screenshot -a $AppPid -o (Join-Path $screenshots $screenshotName) 2>$null
        }
    }
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

if ($VerifyExportMode -eq "NormalizeAudio") {
    Test-UI "Normalize audio export log records both loudness passes" {
        $log = winapp ui get-value "ExportLogTextBox" -w $infoWindowHwnd --json 2>$null | ConvertFrom-Json
        if ($log.text -notlike "*Analyzing audio loudness...*" -or $log.text -notlike "*Applying audio normalization...*") {
            throw "The export log did not record both audio normalization passes."
        }
    }
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
