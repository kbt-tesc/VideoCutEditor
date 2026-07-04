using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class MediaProbeServiceTests
{
    [Fact]
    public void ParseJson_reads_format_and_stream_metadata()
    {
        const string json = """
            {
              "streams": [
                {
                  "index": 0,
                  "codec_name": "h264",
                  "codec_type": "video",
                  "width": 1920,
                  "height": 1080,
                  "avg_frame_rate": "30000/1001",
                  "bit_rate": "4500000"
                },
                {
                  "index": 1,
                  "codec_name": "aac",
                  "codec_type": "audio",
                  "sample_rate": "48000",
                  "channels": 2,
                  "channel_layout": "stereo",
                  "bit_rate": "192000"
                }
              ],
              "format": {
                "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                "duration": "123.456000",
                "bit_rate": "4700000"
              }
            }
            """;

        var info = MediaProbeService.ParseJson(@"C:\video\source.mp4", json);

        Assert.Equal(@"C:\video\source.mp4", info.SourcePath);
        Assert.Equal(TimeSpan.FromSeconds(123.456), info.Duration);
        Assert.Equal("mov,mp4,m4a,3gp,3g2,mj2", info.ContainerFormat);
        Assert.Equal(4_700_000, info.Bitrate);

        var video = Assert.Single(info.Streams, stream => stream.CodecType == "video");
        Assert.Equal("h264", video.CodecName);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Equal(29.970, video.FrameRate!.Value, precision: 3);
        Assert.Equal(4_500_000, video.Bitrate);

        var audio = Assert.Single(info.Streams, stream => stream.CodecType == "audio");
        Assert.Equal("aac", audio.CodecName);
        Assert.Equal(48_000, audio.SampleRate);
        Assert.Equal(2, audio.Channels);
        Assert.Equal("stereo", audio.ChannelLayout);
        Assert.Equal(192_000, audio.Bitrate);
    }

    [Fact]
    public void ParseJson_uses_stream_duration_when_format_duration_is_missing()
    {
        const string json = """
            {
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "duration": "5.250000",
                  "avg_frame_rate": "24/1"
                }
              ],
              "format": {
                "format_name": "matroska,webm"
              }
            }
            """;

        var info = MediaProbeService.ParseJson(@"C:\video\source.mkv", json);

        Assert.Equal(TimeSpan.FromSeconds(5.25), info.Duration);
        Assert.Equal(24, info.Streams[0].FrameRate);
    }
}
