namespace DPlayer.Core.Enums;

public enum AppTheme
{
    DarkGreen,
    EmeraldGreen,
    NeonGreen,
    Light
}

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    Buffering,
    Error
}

public enum LoopMode
{
    None,
    Single,
    Playlist
}

public enum MediaType
{
    Unknown,
    Video,
    Audio
}

public enum SubtitleProvider
{
    OpenSubtitles,
    SubDL,
    Podnapisi
}

public enum AspectRatioMode
{
    Auto,
    Ratio16x9,
    Ratio4x3,
    Ratio21x9,
    Fill,
    Stretch
}

public enum VideoRotation
{
    None,
    Rotate90,
    Rotate180,
    Rotate270
}

public enum LibraryItemType
{
    Movie,
    TvShow,
    Episode,
    Music,
    Other
}
