param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\verification-media"),
    [string]$FfmpegPath = "",
    [double]$DurationSeconds = 4,
    [switch]$Force,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Resolve-FfmpegPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "ffmpeg was not found at '$ExplicitPath'."
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $command = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        $command = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
    }

    if ($null -eq $command) {
        throw "ffmpeg was not found on PATH. Pass -FfmpegPath or install ffmpeg before generating sample media."
    }

    return $command.Source
}

function Format-DisplayArgument {
    param([string]$Argument)

    if ($Argument -match "\s") {
        return '"' + ($Argument -replace '"', '\"') + '"'
    }

    return $Argument
}

function Invoke-SampleFfmpeg {
    param(
        [string]$ResolvedFfmpegPath,
        [string[]]$Arguments,
        [string]$OutputPath
    )

    if ((Test-Path -LiteralPath $OutputPath) -and -not $Force) {
        Write-Output "Exists: $OutputPath"
        return
    }

    if ($WhatIf) {
        $displayArguments = ($Arguments | ForEach-Object { Format-DisplayArgument $_ }) -join " "
        Write-Output "Would create: $OutputPath"
        Write-Output "  $ResolvedFfmpegPath $displayArguments"
        return
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null

    & $ResolvedFfmpegPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed with exit code $LASTEXITCODE while creating '$OutputPath'."
    }

    if (-not (Test-Path -LiteralPath $OutputPath)) {
        throw "ffmpeg completed but did not create '$OutputPath'."
    }

    Write-Output "Created: $OutputPath"
}

$duration = [Math]::Max(1, $DurationSeconds).ToString("0.###", [Globalization.CultureInfo]::InvariantCulture)
$resolvedOutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$resolvedFfmpegPath = if ($WhatIf) {
    if ([string]::IsNullOrWhiteSpace($FfmpegPath)) { "ffmpeg" } else { $FfmpegPath }
} else {
    Resolve-FfmpegPath -ExplicitPath $FfmpegPath
}

$samples = @(
    @{
        Name = "video-with-audio.mp4"
        Arguments = @(
            "-hide_banner", "-loglevel", "error", $(if ($Force) { "-y" } else { "-n" }),
            "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30",
            "-f", "lavfi", "-i", "sine=frequency=1000:sample_rate=48000",
            "-t", $duration,
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-shortest",
            "-movflags", "+faststart"
        )
    },
    @{
        Name = "video-only.mp4"
        Arguments = @(
            "-hide_banner", "-loglevel", "error", $(if ($Force) { "-y" } else { "-n" }),
            "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30",
            "-t", $duration,
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-an",
            "-movflags", "+faststart"
        )
    },
    @{
        Name = "quiet-audio.mp4"
        Arguments = @(
            "-hide_banner", "-loglevel", "error", $(if ($Force) { "-y" } else { "-n" }),
            "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000,volume=0.05",
            "-t", $duration,
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-shortest",
            "-movflags", "+faststart"
        )
    },
    @{
        Name = "hdr-pq.mp4"
        Arguments = @(
            "-hide_banner", "-loglevel", "error", $(if ($Force) { "-y" } else { "-n" }),
            "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30",
            "-f", "lavfi", "-i", "sine=frequency=1000:sample_rate=48000",
            "-t", $duration,
            "-vf", "zscale=pin=bt709:tin=bt709:min=bt709:p=bt2020:t=smpte2084:m=bt2020nc,format=yuv420p10le",
            "-c:v", "libx265",
            "-x265-params", "colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc",
            "-c:a", "aac",
            "-shortest",
            "-movflags", "+faststart"
        )
    }
)

foreach ($sample in $samples) {
    $outputPath = Join-Path $resolvedOutputDirectory $sample.Name
    $arguments = [string[]]($sample.Arguments + @($outputPath))
    Invoke-SampleFfmpeg -ResolvedFfmpegPath $resolvedFfmpegPath -Arguments $arguments -OutputPath $outputPath
}
