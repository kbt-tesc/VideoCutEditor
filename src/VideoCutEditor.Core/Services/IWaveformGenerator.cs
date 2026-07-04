namespace VideoCutEditor.Core.Services;

public interface IWaveformGenerator
{
    Task<WaveformResult> GenerateAsync(WaveformPlan plan, CancellationToken cancellationToken = default);
}
