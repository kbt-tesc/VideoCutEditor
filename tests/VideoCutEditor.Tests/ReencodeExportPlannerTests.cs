using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class ReencodeExportPlannerTests
{
    [Fact]
    public void CreatePlan_uses_nvenc_for_auto_when_available()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "h264_nvenc",
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Auto,
                    LastVideoBitrateKbps = 4500,
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Equal(@"C:\tools\ffmpeg.exe", plan.FfmpegPath);
            Assert.Equal(outputPath, plan.FinalOutputPath);
            Assert.Contains(".partial-", plan.TemporaryOutputPath);
            Assert.Equal(
                [
                    "-hide_banner",
                    "-nostdin",
                    "-y",
                    "-ss",
                    "00:00:05.000",
                    "-i",
                    @"C:\video\source.mp4",
                    "-t",
                    "00:00:15.000",
                    "-map",
                    "0",
                    "-c",
                    "copy",
                    "-c:v",
                    "h264_nvenc",
                    "-b:v",
                    "4500k",
                    "-map_metadata",
                    "0",
                    "-avoid_negative_ts",
                    "make_zero",
                    plan.TemporaryOutputPath,
                ],
                plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_falls_back_to_software_for_auto_when_nvenc_is_missing()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx265",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H265,
                    LastEncoderKind = EncoderKind.Auto,
                    LastVideoBitrateKbps = 3000,
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("libx265", plan.Arguments);
            Assert.Contains("3000k", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_appends_additional_ffmpeg_arguments_before_output_path()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    AdditionalFfmpegArguments = "-preset slow -metadata title=\"Sample clip\"",
                });

            ExportPlan plan = planner.CreatePlan(request);
            int mapMetadataIndex = Array.IndexOf(plan.Arguments.ToArray(), "-map_metadata");
            int presetIndex = Array.IndexOf(plan.Arguments.ToArray(), "-preset");

            Assert.True(presetIndex > 0);
            Assert.True(mapMetadataIndex > 0);
            Assert.True(mapMetadataIndex < presetIndex);
            Assert.Equal("slow", plan.Arguments[presetIndex + 1]);
            Assert.Equal("-metadata", plan.Arguments[presetIndex + 2]);
            Assert.Equal("title=Sample clip", plan.Arguments[presetIndex + 3]);
            Assert.True(presetIndex < plan.Arguments.Count - 1);
            Assert.Equal(plan.TemporaryOutputPath, plan.Arguments[^1]);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_rejects_app_managed_additional_ffmpeg_arguments()
    {
        var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "libx264",
        });
        var planner = new ReencodeExportPlanner(capabilities);
        var request = new ExportRequest(
            @"C:\video\source.mp4",
            @"C:\output\clip_cut.mp4",
            new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
            new AppSettings
            {
                FfmpegPath = @"C:\tools\ffmpeg.exe",
                LastExportMode = ExportMode.Reencode,
                LastCodecFamily = CodecFamily.H264,
                LastEncoderKind = EncoderKind.Software,
                AdditionalFfmpegArguments = "-i other.mp4",
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.CreatePlan(request));

        Assert.Contains("-i", exception.Message);
        Assert.Contains("managed by VideoCutEditor", exception.Message);
    }

    [Fact]
    public void CreatePlan_uses_crf_for_software_quality_mode()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastBitrateMode = BitrateMode.Quality,
                    LastQualityValue = 22,
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("-crf", plan.Arguments);
            Assert.Contains("22", plan.Arguments);
            Assert.DoesNotContain("-b:v", plan.Arguments);
            Assert.DoesNotContain("2500k", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_uses_cq_for_nvenc_quality_mode()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "h264_nvenc",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Nvenc,
                    LastBitrateMode = BitrateMode.Quality,
                    LastQualityValue = 19,
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("-cq", plan.Arguments);
            Assert.Contains("19", plan.Arguments);
            Assert.DoesNotContain("-b:v", plan.Arguments);
            Assert.DoesNotContain("2500k", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_rejects_explicit_nvenc_when_encoder_is_unavailable()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Nvenc,
                    LastVideoBitrateKbps = 2500,
                });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.CreatePlan(request));
            Assert.Contains("No supported video encoder", exception.Message);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_adds_video_fade_filters_at_clip_edges()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    Fade = new FadeSettings
                    {
                        VideoFadeIn = true,
                        VideoFadeOut = true,
                        DurationSeconds = 1.5,
                    },
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("-vf", plan.Arguments);
            Assert.Contains("fade=t=in:st=0:d=1.5,fade=t=out:st=8.5:d=1.5", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_adds_audio_fade_filters_and_aac_reencode()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    Fade = new FadeSettings
                    {
                        AudioFadeIn = true,
                        AudioFadeOut = true,
                        DurationSeconds = 2,
                    },
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("-af", plan.Arguments);
            Assert.Contains("afade=t=in:st=0:d=2,afade=t=out:st=8:d=2", plan.Arguments);
            Assert.Contains("-c:a", plan.Arguments);
            Assert.Contains("aac", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_adds_loudnorm_arguments_when_audio_normalization_is_enabled()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    NormalizeAudio = true,
                },
                new MediaInfo(
                    @"C:\video\source.mp4",
                    TimeSpan.FromSeconds(10),
                    "mov,mp4,m4a,3gp,3g2,mj2",
                    2_500_000,
                    [
                        new MediaStreamInfo(0, "video", "h264", 2_500_000),
                        new MediaStreamInfo(1, "audio", "aac", 128_000),
                    ]));

            ExportPlan plan = planner.CreatePlan(request);

            Assert.NotNull(plan.AudioNormalizationAnalysis);
            Assert.Contains("-af", plan.Arguments);
            Assert.Contains("loudnorm=I=-14:TP=-1.5:LRA=11", plan.Arguments);
            Assert.Contains("-c:a", plan.Arguments);
            Assert.Contains("aac", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_combines_loudnorm_and_audio_fade_filters()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    NormalizeAudio = true,
                    Fade = new FadeSettings
                    {
                        AudioFadeIn = true,
                        AudioFadeOut = true,
                        DurationSeconds = 2,
                    },
                },
                new MediaInfo(
                    @"C:\video\source.mp4",
                    TimeSpan.FromSeconds(10),
                    "mov,mp4,m4a,3gp,3g2,mj2",
                    2_500_000,
                    [
                        new MediaStreamInfo(0, "video", "h264", 2_500_000),
                        new MediaStreamInfo(1, "audio", "aac", 128_000),
                    ]));

            ExportPlan plan = planner.CreatePlan(request);

            Assert.NotNull(plan.AudioNormalizationAnalysis);
            Assert.Contains(
                "loudnorm=I=-14:TP=-1.5:LRA=11,afade=t=in:st=0:d=2,afade=t=out:st=8:d=2",
                plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_adds_hdr_to_sdr_video_filter_for_hdr_media_when_enabled()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\hdr.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    ConvertHdrToSdr = true,
                },
                new MediaInfo(
                    @"C:\video\hdr.mp4",
                    TimeSpan.FromSeconds(10),
                    "mov,mp4,m4a,3gp,3g2,mj2",
                    2_500_000,
                    [
                        new MediaStreamInfo(
                            0,
                            "video",
                            "hevc",
                            2_500_000,
                            Width: 3840,
                            Height: 2160,
                            FrameRate: 30,
                            ColorSpace: "bt2020nc",
                            ColorTransfer: "smpte2084",
                            ColorPrimaries: "bt2020"),
                    ]));

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("-vf", plan.Arguments);
            Assert.Contains("zscale=t=linear:npl=100,format=gbrpf32le,tonemap=tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=tv,format=yuv420p", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_combines_hdr_to_sdr_and_video_fade_filters()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\hdr.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    ConvertHdrToSdr = true,
                    Fade = new FadeSettings
                    {
                        VideoFadeIn = true,
                        VideoFadeOut = true,
                        DurationSeconds = 1,
                    },
                },
                new MediaInfo(
                    @"C:\video\hdr.mp4",
                    TimeSpan.FromSeconds(10),
                    "mov,mp4,m4a,3gp,3g2,mj2",
                    2_500_000,
                    [
                        new MediaStreamInfo(
                            0,
                            "video",
                            "hevc",
                            2_500_000,
                            ColorTransfer: "arib-std-b67"),
                    ]));

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains(
                "zscale=t=linear:npl=100,format=gbrpf32le,tonemap=tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=tv,format=yuv420p,fade=t=in:st=0:d=1,fade=t=out:st=9:d=1",
                plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_rejects_audio_normalization_when_media_has_no_audio_stream()
    {
        var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "libx264",
        });
        var planner = new ReencodeExportPlanner(capabilities);
        var request = new ExportRequest(
            @"C:\video\source.mp4",
            @"C:\output\clip_cut.mp4",
            new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
            new AppSettings
            {
                FfmpegPath = @"C:\tools\ffmpeg.exe",
                LastExportMode = ExportMode.Reencode,
                LastCodecFamily = CodecFamily.H264,
                LastEncoderKind = EncoderKind.Software,
                LastVideoBitrateKbps = 2500,
                NormalizeAudio = true,
            },
            new MediaInfo(
                @"C:\video\source.mp4",
                TimeSpan.FromSeconds(10),
                "mov,mp4,m4a,3gp,3g2,mj2",
                2_500_000,
                [
                    new MediaStreamInfo(0, "video", "h264", 2_500_000),
                ]));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.CreatePlan(request));

        Assert.Contains("audio stream", exception.Message);
    }

    [Fact]
    public void CreatePlan_omits_audio_fade_filters_when_media_has_no_audio_stream()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    Fade = new FadeSettings
                    {
                        AudioFadeIn = true,
                        AudioFadeOut = true,
                        DurationSeconds = 2,
                    },
                },
                new MediaInfo(
                    @"C:\video\source.mp4",
                    TimeSpan.FromSeconds(10),
                    "mov,mp4,m4a,3gp,3g2,mj2",
                    2_500_000,
                    [
                        new MediaStreamInfo(
                            0,
                            "video",
                            "h264",
                            2_500_000,
                            Width: 1920,
                            Height: 1080,
                            FrameRate: 30),
                    ]));

            ExportPlan plan = planner.CreatePlan(request);

            Assert.DoesNotContain("-af", plan.Arguments);
            Assert.DoesNotContain("afade=t=in:st=0:d=2,afade=t=out:st=8:d=2", plan.Arguments);
            Assert.DoesNotContain("-c:a", plan.Arguments);
            Assert.DoesNotContain("aac", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_truncates_fade_duration_to_two_decimal_places()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 2500,
                    Fade = new FadeSettings
                    {
                        VideoFadeIn = true,
                        VideoFadeOut = true,
                        DurationSeconds = 1.239,
                    },
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("fade=t=in:st=0:d=1.23,fade=t=out:st=8.77:d=1.23", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
