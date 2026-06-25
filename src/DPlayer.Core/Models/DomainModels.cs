using DPlayer.Core.Enums;

namespace DPlayer.Core.Models;

public sealed class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public TimeSpan Duration { get; set; }
    public MediaType Type { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayed { get; set; }
    public TimeSpan LastPosition { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? PosterPath { get; set; }
    public LibraryItemType LibraryType { get; set; }
    public string? SeriesTitle { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSmartPlaylist { get; set; }
    public string? SmartFilter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public List<PlaylistEntry> Items { get; set; } = [];
}

public sealed class PlaylistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaylistId { get; set; }
    public int OrderIndex { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

public sealed class SubtitleTrack
{
    public int Index { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
    public string? FilePath { get; set; }
}

public sealed class AudioTrack
{
    public int Index { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public int Channels { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class SubtitleSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string? Release { get; set; }
    public double Rating { get; set; }
    public int DownloadCount { get; set; }
    public SubtitleProvider Provider { get; set; }
    public string? PreviewText { get; set; }
    public string? Season { get; set; }
    public string? Episode { get; set; }
}

public sealed class SubtitleSearchQuery
{
    public string? Title { get; set; }
    public string? SeriesTitle { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string LanguageCode { get; set; } = "eng";
    public string? FileHash { get; set; }
    public long? FileSizeBytes { get; set; }
}

public sealed class SceneBookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MediaPath { get; set; } = string.Empty;
    public TimeSpan Position { get; set; }
    public string? Label { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AbRepeatRange
{
    public TimeSpan? PointA { get; set; }
    public TimeSpan? PointB { get; set; }
    public bool IsActive => PointA.HasValue && PointB.HasValue;
}

public sealed class EqualizerBand
{
    public int FrequencyHz { get; set; }
    public double GainDb { get; set; }
}

public sealed class SubtitleStyle
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 24;
    public string ForegroundColor { get; set; } = "#FFFFFF";
    public string OutlineColor { get; set; } = "#000000";
    public double OutlineWidth { get; set; } = 2;
    public bool ShadowEnabled { get; set; } = true;
    public double ShadowDepth { get; set; } = 2;
    public double DelayMs { get; set; }
}

public sealed class VideoFilterSettings
{
    public double Brightness { get; set; } = 1.0;
    public double Contrast { get; set; } = 1.0;
    public double Saturation { get; set; } = 1.0;
    public double Gamma { get; set; } = 1.0;
    public double Hue { get; set; }
    public double Zoom { get; set; } = 1.0;
    public VideoRotation Rotation { get; set; } = VideoRotation.None;
    public AspectRatioMode AspectRatio { get; set; } = AspectRatioMode.Auto;
}
