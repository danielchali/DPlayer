using DPlayer.Core.Enums;

namespace DPlayer.Core.Models;

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.DarkGreen;
    public bool HardwareAcceleration { get; set; } = true;
    public bool AutoDownloadSubtitles { get; set; } = true;
    public string DefaultSubtitleLanguage { get; set; } = "eng";
    public List<string> SubtitleDownloadLanguages { get; set; } = ["eng"];
    public List<SubtitleProvider> EnabledSubtitleProviders { get; set; } =
    [
        SubtitleProvider.OpenSubtitles,
        SubtitleProvider.SubDL,
        SubtitleProvider.Podnapisi
    ];
    public double DefaultVolume { get; set; } = 80;
    public double DefaultPlaybackSpeed { get; set; } = 1.0;
    public bool ResumePlayback { get; set; } = true;
    public bool LoopPlaylist { get; set; }
    public bool ShufflePlaylist { get; set; }
    public bool CheckForUpdates { get; set; } = true;
    public string? OpenSubtitlesApiKey { get; set; }
    public string? OpenSubtitlesUsername { get; set; }
    public string? OpenSubtitlesPassword { get; set; }
    public SubtitleStyle SubtitleStyle { get; set; } = new();
    public VideoFilterSettings VideoFilters { get; set; } = new();
    public Dictionary<string, string> KeyboardShortcuts { get; set; } = DefaultShortcuts.Create();
    public List<string> LibraryFolders { get; set; } = [];
    public bool EnableSurroundSimulation { get; set; }
    public bool AudioNormalization { get; set; }
    public double BassBoost { get; set; }
    public bool MiniPlayerOnMinimize { get; set; }
    public bool PictureInPictureEnabled { get; set; }
}

public static class DefaultShortcuts
{
    public static Dictionary<string, string> Create() => new()
    {
        ["PlayPause"] = "Space",
        ["Fullscreen"] = "F",
        ["SeekBackward"] = "Left",
        ["SeekForward"] = "Right",
        ["VolumeUp"] = "Up",
        ["VolumeDown"] = "Down",
        ["OpenFile"] = "Ctrl+O",
        ["OpenUrl"] = "Ctrl+U",
        ["OpenFolder"] = "Ctrl+Shift+O",
        ["SubtitleSearch"] = "Ctrl+S",
        ["Stop"] = "S",
        ["Mute"] = "M",
        ["FrameBack"] = "Comma",
        ["FrameForward"] = "Period",
        ["SpeedDown"] = "BracketLeft",
        ["SpeedUp"] = "BracketRight",
        ["CycleAudioTrack"] = "A",
        ["CycleSubtitleTrack"] = "T",
        ["AudioDelayDown"] = "J",
        ["AudioDelayUp"] = "K",
        ["SubtitleDelayDown"] = "G",
        ["SubtitleDelayUp"] = "H",
        ["Screenshot"] = "Ctrl+Shift+S",
        ["TogglePlaylist"] = "L",
        ["AbRepeat"] = "R"
    };
}
