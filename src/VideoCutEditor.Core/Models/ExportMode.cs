namespace VideoCutEditor.Core.Models;

public enum ExportMode
{
    FastCopy,
    Reencode,

    // Legacy settings value migrated to FastCopy plus NormalizeAudio.
    AudioNormalize,
}
