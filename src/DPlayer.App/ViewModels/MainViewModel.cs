using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;
using DPlayer.Core.Enums;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using DPlayer.App.Services;
using Microsoft.Extensions.Logging;

namespace DPlayer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMediaPlayerService _player;
    private readonly IPlaylistService _playlist;
    private readonly ILibraryService _library;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IFileService _files;
    private readonly ISubtitleService _subtitles;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty] private string _title = "DPlayer";
    [ObservableProperty] private string _currentFileName = "No media loaded";
    [ObservableProperty] private TimeSpan _position;
    [ObservableProperty] private TimeSpan _duration;
    [ObservableProperty] private double _volume = 80;
    [ObservableProperty] private double _playbackSpeed = 1.0;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isFullscreen;
    [ObservableProperty] private bool _isPlaylistPanelOpen;
    [ObservableProperty] private bool _isSubtitlePanelOpen;
    [ObservableProperty] private bool _isControlsVisible = true;
    [ObservableProperty] private bool _isShuffle;
    [ObservableProperty] private bool _isLooping;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private PlaybackState _playbackState;
    [ObservableProperty] private string _playbackSpeedText = "1.0x";
    [ObservableProperty] private double _brightness = 1.0;
    [ObservableProperty] private double _contrast = 1.0;
    [ObservableProperty] private double _saturation = 1.0;
    [ObservableProperty] private AspectRatioMode _aspectRatio = AspectRatioMode.Auto;
    [ObservableProperty] private VideoRotation _rotation = VideoRotation.None;
    [ObservableProperty] private ObservableCollection<AudioTrack> _audioTracks = [];
    [ObservableProperty] private ObservableCollection<SubtitleTrack> _subtitleTracks = [];
    [ObservableProperty] private string _selectedAudioTrackText = "Audio";
    [ObservableProperty] private string _selectedSubtitleTrackText = "Subtitles";
    [ObservableProperty] private string _osdText = string.Empty;
    [ObservableProperty] private bool _isOsdVisible;

  public static readonly double[] SpeedOptions = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 4.0];
    private int _speedIndex = 3;
    private CancellationTokenSource? _osdCts;

    public PlaylistViewModel PlaylistVm { get; }
    public SubtitleViewModel SubtitleVm { get; }

    public MainViewModel(
        IMediaPlayerService player,
        IPlaylistService playlist,
        ILibraryService library,
        ISettingsService settings,
        IDialogService dialogs,
        IFileService files,
        ISubtitleService subtitles,
        PlaylistViewModel playlistVm,
        SubtitleViewModel subtitleVm,
        ILogger<MainViewModel> logger)
    {
        _player = player;
        _playlist = playlist;
        _library = library;
        _settings = settings;
        _dialogs = dialogs;
        _files = files;
        _subtitles = subtitles;
        _logger = logger;
        PlaylistVm = playlistVm;
        SubtitleVm = subtitleVm;

        Volume = settings.Settings.DefaultVolume;
        PlaybackSpeed = settings.Settings.DefaultPlaybackSpeed;
        IsShuffle = settings.Settings.ShufflePlaylist;
        IsLooping = settings.Settings.LoopPlaylist;

        _player.StateChanged += (_, state) =>
        {
            PlaybackState = state;
            IsPlaying = state == PlaybackState.Playing;
            if (state == PlaybackState.Playing)
                RefreshTrackLists();
        };
        _player.PositionChanged += (_, pos) => Position = pos;
        _player.DurationChanged += (_, dur) => Duration = dur;
        _player.MediaEnded += async (_, _) =>
        {
            if (_playlist.ActivePlaylist is not null)
            {
                await _playlist.PlayNextAsync(_player);
                UpdateCurrentMediaUi();
            }
        };
        _player.ErrorOccurred += (_, msg) => _dialogs.ShowMessage("Playback Error", msg);
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var path = await _dialogs.OpenFileAsync(_files.GetMediaFilter());
        if (path is not null) await OpenFileAsync(path);
    }

    public async Task OpenFileAsync(string path)
    {
        try
        {
            if (_settings.Settings.ResumePlayback)
            {
                var savedPos = await _library.GetPlaybackPositionAsync(path);
                await _player.LoadAsync(path);
                if (savedPos.HasValue && savedPos.Value > TimeSpan.Zero)
                    _player.Seek(savedPos.Value);
            }
            else
            {
                await _player.LoadAsync(path);
            }

            _player.Volume = Volume;
            _player.PlaybackSpeed = PlaybackSpeed;
            _player.Play();
            RefreshTrackLists();

            CurrentFileName = Path.GetFileName(path);
            Title = $"DPlayer - {CurrentFileName}";

            if (_settings.Settings.AutoDownloadSubtitles)
            {
                var subPath = await _subtitles.AutoDetectAndDownloadAsync(
                    path, _settings.Settings.DefaultSubtitleLanguage);
                if (subPath is not null)
                {
                    await _player.LoadSubtitleAsync(subPath);
                    RefreshTrackLists();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file {Path}", path);
            _dialogs.ShowMessage("Error", $"Could not open file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        var folder = await _dialogs.OpenFolderAsync();
        if (folder is null) return;
        await _library.ScanFolderAsync(folder);
        _dialogs.ShowMessage("Library", $"Scanned folder: {folder}");
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        _player.TogglePlayPause();
        ShowOsd(IsPlaying ? "Pause" : "Play");
    }

    [RelayCommand]
    private void Stop()
    {
        _player.Stop();
        ShowOsd("Stop");
    }

    [RelayCommand]
    private async Task PlayNext()
    {
        await _playlist.PlayNextAsync(_player);
        UpdateCurrentMediaUi();
    }

    [RelayCommand]
    private async Task PlayPrevious()
    {
        await _playlist.PlayPreviousAsync(_player);
        UpdateCurrentMediaUi();
    }

    [RelayCommand]
    private void SeekForward()
    {
        _player.SeekRelative(TimeSpan.FromSeconds(10));
        ShowOsd($"+10s  {FormatTime(_player.Position)}");
    }

    [RelayCommand]
    private void SeekBackward()
    {
        _player.SeekRelative(TimeSpan.FromSeconds(-10));
        ShowOsd($"-10s  {FormatTime(_player.Position)}");
    }

    [RelayCommand]
    private void FastForward()
    {
        _player.SeekRelative(TimeSpan.FromSeconds(30));
        ShowOsd($"+30s  {FormatTime(_player.Position)}");
    }

    [RelayCommand]
    private void Rewind()
    {
        _player.SeekRelative(TimeSpan.FromSeconds(-30));
        ShowOsd($"-30s  {FormatTime(_player.Position)}");
    }

    [RelayCommand]
    private void FrameForward() => _player.FrameStep(true);

    [RelayCommand]
    private void FrameBack() => _player.FrameStep(false);

    [RelayCommand]
    private void SpeedUp()
    {
        var next = SpeedOptions.FirstOrDefault(s => s > PlaybackSpeed);
        if (next <= 0) next = SpeedOptions[^1];
        SetPlaybackSpeed(next);
    }

    [RelayCommand]
    private void SpeedDown()
    {
        var next = SpeedOptions.LastOrDefault(s => s < PlaybackSpeed);
        if (next <= 0) next = SpeedOptions[0];
        SetPlaybackSpeed(next);
    }

    [RelayCommand]
    private void SeekToPosition(double seconds)
    {
        var position = TimeSpan.FromSeconds(seconds);
        _player.Seek(position);
        ShowOsd(FormatTime(position));
    }

    partial void OnVolumeChanged(double value)
    {
        _player.Volume = value;
        if (_player is Infrastructure.Playback.LibVlcMediaPlayerService vlc)
            vlc.SetVolume();
        ShowOsd($"Volume {value:0}%");
    }

    partial void OnIsMutedChanged(bool value)
    {
        _player.IsMuted = value;
        if (_player is Infrastructure.Playback.LibVlcMediaPlayerService vlc)
            vlc.SetVolume();
        ShowOsd(value ? "Muted" : $"Volume {Volume:0}%");
    }

    [RelayCommand]
    private void VolumeUp()
    {
        Volume = Math.Min(100, Volume + 5);
    }

    [RelayCommand]
    private void VolumeDown()
    {
        Volume = Math.Max(0, Volume - 5);
    }

    [RelayCommand]
    private void CyclePlaybackSpeed()
    {
        _speedIndex = (_speedIndex + 1) % SpeedOptions.Length;
        PlaybackSpeed = SpeedOptions[_speedIndex];
        PlaybackSpeedText = $"{PlaybackSpeed:0.##}x";
        _player.PlaybackSpeed = PlaybackSpeed;
        if (_player is Infrastructure.Playback.LibVlcMediaPlayerService vlc)
            vlc.SetPlaybackRate();
        ShowOsd($"Speed {PlaybackSpeedText}");
    }

    [RelayCommand]
    private void SetPlaybackSpeed(double speed)
    {
        PlaybackSpeed = speed;
        PlaybackSpeedText = $"{speed:0.##}x";
        _player.PlaybackSpeed = speed;
        if (_player is Infrastructure.Playback.LibVlcMediaPlayerService vlc)
            vlc.SetPlaybackRate();
        ShowOsd($"Speed {PlaybackSpeedText}");
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        _playlist.Shuffle = IsShuffle;
        ShowOsd(IsShuffle ? "Shuffle on" : "Shuffle off");
    }

    [RelayCommand]
    private void ToggleLoop()
    {
        IsLooping = !IsLooping;
        _player.LoopMode = IsLooping ? LoopMode.Single : LoopMode.None;
        _playlist.LoopMode = IsLooping ? LoopMode.Playlist : LoopMode.None;
        ShowOsd(IsLooping ? "Loop on" : "Loop off");
    }

    [RelayCommand]
    private void SetAbPointA() => _player.SetAbPointA();

    [RelayCommand]
    private void SetAbPointB() => _player.SetAbPointB();

    [RelayCommand]
    private void ClearAbRepeat() => _player.ClearAbRepeat();

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        ShowOsd(IsFullscreen ? "Fullscreen" : "Windowed");
    }

    [RelayCommand]
    private void TogglePlaylistPanel() => IsPlaylistPanelOpen = !IsPlaylistPanelOpen;

    [RelayCommand]
    private void ToggleSubtitlePanel() => IsSubtitlePanelOpen = !IsSubtitlePanelOpen;

    [RelayCommand]
    private void OpenSubtitleSearch() => _dialogs.ShowSubtitleSearchWindow();

    [RelayCommand]
    private async Task LoadSubtitle()
    {
        var path = await _dialogs.OpenFileAsync(_files.GetSubtitleFilter());
        if (path is not null)
        {
            await _player.LoadSubtitleAsync(path);
            RefreshTrackLists();
            ShowOsd($"Subtitle loaded: {Path.GetFileName(path)}");
        }
    }

    [RelayCommand]
    private void CycleAudioTrack()
    {
        RefreshTrackLists();
        if (AudioTracks.Count == 0)
        {
            ShowOsd("No audio tracks");
            return;
        }

        var index = AudioTracks.ToList().FindIndex(t => t.Index == _player.CurrentAudioTrack);
        var next = AudioTracks[(index + 1 + AudioTracks.Count) % AudioTracks.Count];
        SelectAudioTrack(next.Index);
    }

    [RelayCommand]
    private void CycleSubtitleTrack()
    {
        RefreshTrackLists();
        if (SubtitleTracks.Count == 0)
        {
            ShowOsd("No subtitle tracks");
            return;
        }

        var index = SubtitleTracks.ToList().FindIndex(t => t.Index == _player.CurrentSubtitleTrack);
        var next = SubtitleTracks[(index + 1 + SubtitleTracks.Count) % SubtitleTracks.Count];
        SelectSubtitleTrack(next.Index);
    }

    [RelayCommand]
    private void SelectAudioTrack(object? parameter)
    {
        if (!TryGetTrackIndex(parameter, out var trackIndex)) return;
        _player.SelectAudioTrack(trackIndex);
        RefreshTrackLists();
        ShowOsd($"Audio: {SelectedAudioTrackText}");
    }

    [RelayCommand]
    private void SelectSubtitleTrack(object? parameter)
    {
        if (!TryGetTrackIndex(parameter, out var trackIndex)) return;
        _player.SelectSubtitleTrack(trackIndex);
        RefreshTrackLists();
        ShowOsd($"Subtitle: {SelectedSubtitleTrackText}");
    }

    [RelayCommand]
    private void AdjustAudioDelay(string milliseconds)
    {
        if (!double.TryParse(milliseconds, out var value)) return;
        _player.AudioDelay += TimeSpan.FromMilliseconds(value);
        _player.ApplyAudioDelay();
        ShowOsd($"Audio delay {FormatSignedMilliseconds(_player.AudioDelay)}");
    }

    [RelayCommand]
    private void AdjustSubtitleDelay(string milliseconds)
    {
        if (!double.TryParse(milliseconds, out var value)) return;
        _player.SubtitleDelay += TimeSpan.FromMilliseconds(value);
        _player.ApplySubtitleDelay();
        ShowOsd($"Subtitle delay {FormatSignedMilliseconds(_player.SubtitleDelay)}");
    }

    [RelayCommand]
    private void ResetAvSync()
    {
        _player.AudioDelay = TimeSpan.Zero;
        _player.SubtitleDelay = TimeSpan.Zero;
        _player.ApplyAudioDelay();
        _player.ApplySubtitleDelay();
        ShowOsd("A/V sync reset");
    }

    [RelayCommand]
    private void ShowMediaInfo()
    {
        var media = _player.CurrentMedia;
        if (media is null)
        {
            _dialogs.ShowMessage("Media Info", "No media loaded.");
            return;
        }

        var info = new FileInfo(media.FilePath);
        var text = new StringBuilder()
            .AppendLine($"Title: {media.Title}")
            .AppendLine($"Path: {media.FilePath}")
            .AppendLine($"Type: {media.Type}")
            .AppendLine($"Duration: {FormatTime(Duration)}")
            .AppendLine($"Size: {FormatBytes(info.Exists ? info.Length : media.FileSizeBytes)}")
            .AppendLine($"Audio tracks: {_player.AudioTracks.Count}")
            .AppendLine($"Subtitle tracks: {_player.SubtitleTracks.Count}")
            .AppendLine($"Playback speed: {PlaybackSpeedText}")
            .AppendLine($"Volume: {Volume:0}%")
            .ToString();

        _dialogs.ShowMessage("Media Info", text);
    }

    [RelayCommand]
    private async Task TakeScreenshot()
    {
        var bytes = await _player.TakeScreenshotAsync();
        if (bytes is null) return;
        var path = await _dialogs.SaveFileAsync("PNG Image|*.png", "screenshot.png");
        if (path is not null)
            await File.WriteAllBytesAsync(path, bytes);
    }

    [RelayCommand]
    private void OpenSettings() => _dialogs.ShowSettingsWindow();

    [RelayCommand]
    private void SetAspectRatio(string mode)
    {
        if (!Enum.TryParse<AspectRatioMode>(mode, out var aspectRatio)) return;
        AspectRatio = aspectRatio;
        ShowOsd($"Aspect ratio {GetAspectRatioLabel(aspectRatio)}");
    }

    [RelayCommand]
    private async Task HandleFileDrop(string[] files)
    {
        foreach (var file in files)
        {
            if (_files.IsMediaFile(file))
            {
                await OpenFileAsync(file);
                break;
            }
            if (_files.IsSubtitleFile(file))
                await _player.LoadSubtitleAsync(file);
        }
    }

    partial void OnBrightnessChanged(double value) => ApplyVideoFilters();
    partial void OnContrastChanged(double value) => ApplyVideoFilters();
    partial void OnSaturationChanged(double value) => ApplyVideoFilters();
    partial void OnRotationChanged(VideoRotation value) => ApplyVideoFilters();
    partial void OnAspectRatioChanged(AspectRatioMode value) => ApplyVideoFilters();

    private void ApplyVideoFilters()
    {
        _player.VideoFilters = new VideoFilterSettings
        {
            Brightness = Brightness,
            Contrast = Contrast,
            Saturation = Saturation,
            Rotation = Rotation,
            AspectRatio = AspectRatio
        };
        if (_player is Infrastructure.Playback.LibVlcMediaPlayerService vlc)
            vlc.ApplyVideoFilters();
        ShowOsd("Video settings applied");
    }

    public async Task SavePositionAsync()
    {
        if (_player.CurrentMedia is not null && Position > TimeSpan.Zero)
            await _library.SavePlaybackPositionAsync(_player.CurrentMedia.FilePath, Position);
    }

    private void RefreshTrackLists()
    {
        AudioTracks = new ObservableCollection<AudioTrack>(_player.AudioTracks);
        SubtitleTracks = new ObservableCollection<SubtitleTrack>(_player.SubtitleTracks);
        SelectedAudioTrackText = AudioTracks.FirstOrDefault(t => t.Index == _player.CurrentAudioTrack)?.Label ?? "Audio";
        SelectedSubtitleTrackText = _player.CurrentSubtitleTrack < 0
            ? "Subtitles off"
            : SubtitleTracks.FirstOrDefault(t => t.Index == _player.CurrentSubtitleTrack)?.Label ?? "Subtitles";
    }

    private void UpdateCurrentMediaUi()
    {
        if (_player.CurrentMedia is null) return;
        CurrentFileName = Path.GetFileName(_player.CurrentMedia.FilePath);
        Title = $"DPlayer - {CurrentFileName}";
        RefreshTrackLists();
    }

    private async void ShowOsd(string message)
    {
        _osdCts?.Cancel();
        var cts = new CancellationTokenSource();
        _osdCts = cts;
        OsdText = message;
        IsOsdVisible = true;

        try
        {
            await Task.Delay(1400, cts.Token);
            if (!cts.IsCancellationRequested)
                IsOsdVisible = false;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"mm\:ss");

    private static string FormatSignedMilliseconds(TimeSpan value) =>
        $"{(value >= TimeSpan.Zero ? "+" : "-")}{Math.Abs(value.TotalMilliseconds):0} ms";

    private static string GetAspectRatioLabel(AspectRatioMode aspectRatio) => aspectRatio switch
    {
        AspectRatioMode.Ratio16x9 => "16:9",
        AspectRatioMode.Ratio4x3 => "4:3",
        AspectRatioMode.Ratio21x9 => "21:9",
        AspectRatioMode.Fill => "Fill",
        AspectRatioMode.Stretch => "Stretch",
        _ => "Auto"
    };

    private static bool TryGetTrackIndex(object? value, out int index)
    {
        switch (value)
        {
            case int i:
                index = i;
                return true;
            case long l:
                index = (int)l;
                return true;
            case string s when int.TryParse(s, out var parsed):
                index = parsed;
                return true;
            default:
                index = 0;
                return false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
