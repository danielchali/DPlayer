using DPlayer.Core.Enums;
using DPlayer.Core.Models;

namespace DPlayer.Core.Interfaces;

public interface IMediaPlayerService
{
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    double PlaybackSpeed { get; set; }
    bool IsMuted { get; set; }
    bool IsFullscreen { get; set; }
    LoopMode LoopMode { get; set; }
    AbRepeatRange AbRepeat { get; }
    MediaItem? CurrentMedia { get; }
    IReadOnlyList<SubtitleTrack> SubtitleTracks { get; }
    IReadOnlyList<AudioTrack> AudioTracks { get; }
    int CurrentSubtitleTrack { get; set; }
    int CurrentAudioTrack { get; set; }
    TimeSpan AudioDelay { get; set; }
    TimeSpan SubtitleDelay { get; set; }
    VideoFilterSettings VideoFilters { get; set; }
    SubtitleStyle SubtitleStyle { get; set; }

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<TimeSpan>? DurationChanged;
    event EventHandler? MediaEnded;
    event EventHandler<string>? ErrorOccurred;

    Task LoadAsync(string path, CancellationToken cancellationToken = default);
    Task LoadStreamAsync(string url, CancellationToken cancellationToken = default);
    void Play();
    void Pause();
    void Stop();
    void TogglePlayPause();
    void Seek(TimeSpan position);
    void SeekRelative(TimeSpan offset);
    void FrameStep(bool forward);
    void SetAbPointA();
    void SetAbPointB();
    void ClearAbRepeat();
    Task LoadSubtitleAsync(string path);
    void SelectAudioTrack(int trackIndex);
    void SelectSubtitleTrack(int trackIndex);
    void ApplyAudioDelay();
    void ApplySubtitleDelay();
    void SetHardwareAcceleration(bool enabled);
    Task<byte[]?> TakeScreenshotAsync();
    void ApplyEqualizer(IReadOnlyList<EqualizerBand> bands);
    void DisposePlayer();
}

public interface IPlaylistService
{
    IReadOnlyList<Playlist> Playlists { get; }
    Playlist? ActivePlaylist { get; }
    int CurrentIndex { get; }
    bool Shuffle { get; set; }
    LoopMode LoopMode { get; set; }

    event EventHandler? PlaylistChanged;
    event EventHandler<int>? CurrentIndexChanged;

    Task LoadPlaylistsAsync();
    Task<Playlist> CreatePlaylistAsync(string name);
    Task DeletePlaylistAsync(Guid id);
    Task AddToPlaylistAsync(Guid playlistId, string filePath, string title);
    Task RemoveFromPlaylistAsync(Guid playlistId, Guid entryId);
    Task ClearPlaylistAsync(Guid playlistId);
    void SetActivePlaylist(Guid playlistId);
    Task PlayNextAsync(IMediaPlayerService player);
    Task PlayPreviousAsync(IMediaPlayerService player);
    Task ImportPlaylistAsync(string path);
    Task ExportPlaylistAsync(Guid playlistId, string path);
    Task ReorderAsync(Guid playlistId, int fromIndex, int toIndex);
}

public interface ISubtitleService
{
    Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(
        SubtitleSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<string> DownloadAsync(
        SubtitleSearchResult result,
        string saveDirectory,
        CancellationToken cancellationToken = default);

    Task<string?> AutoDetectAndDownloadAsync(
        string mediaPath,
        string languageCode,
        CancellationToken cancellationToken = default);

    Task<string> ComputeFileHashAsync(string filePath);
}

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task<T?> GetValueAsync<T>(string key);
    Task SetValueAsync<T>(string key, T value);
}

public interface ILibraryService
{
    Task ScanFolderAsync(string folderPath, IProgress<int>? progress = null);
    Task<IReadOnlyList<MediaItem>> SearchAsync(string query, LibraryItemType? type = null);
    Task<IReadOnlyList<MediaItem>> GetRecentAsync(int count = 20);
    Task AddToFavoritesAsync(string filePath);
    Task RemoveFromFavoritesAsync(string filePath);
    Task<IReadOnlyList<MediaItem>> GetFavoritesAsync();
    Task AddToWatchLaterAsync(string filePath);
    Task<IReadOnlyList<MediaItem>> GetWatchLaterAsync();
    Task SavePlaybackPositionAsync(string filePath, TimeSpan position);
    Task<TimeSpan?> GetPlaybackPositionAsync(string filePath);
    Task<IReadOnlyList<SceneBookmark>> GetBookmarksAsync(string filePath);
    Task AddBookmarkAsync(SceneBookmark bookmark);
}

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

public sealed class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}
