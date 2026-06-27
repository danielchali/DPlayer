using DPlayer.Core.Enums;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using VlcMedia = LibVLCSharp.Shared.Media;
using SubtitleTrackInfo = DPlayer.Core.Models.SubtitleTrack;
using AudioTrackInfo = DPlayer.Core.Models.AudioTrack;
using DomainMediaType = DPlayer.Core.Enums.MediaType;

namespace DPlayer.Infrastructure.Playback;

/// <summary>
/// LibVLC-based media player with hardware acceleration (NVDEC, AMF, Quick Sync).
/// </summary>
public sealed class LibVlcMediaPlayerService : IMediaPlayerService, IDisposable
{
    private readonly ILogger<LibVlcMediaPlayerService> _logger;
    private readonly LibVLC _libVlc;
    private MediaPlayer? _player;
    private VlcMedia? _currentMedia;
    private readonly AbRepeatRange _abRepeat = new();
    private readonly List<SubtitleTrackInfo> _subtitleTracks = [];
    private readonly List<AudioTrackInfo> _audioTracks = [];
    private System.Timers.Timer? _positionTimer;
    private bool _hardwareAcceleration = true;

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public double Volume { get; set; } = 80;
    public double PlaybackSpeed { get; set; } = 1.0;
    public bool IsMuted { get; set; }
    public bool IsFullscreen { get; set; }
    public LoopMode LoopMode { get; set; }
    public AbRepeatRange AbRepeat => _abRepeat;
    public MediaItem? CurrentMedia { get; private set; }
    public IReadOnlyList<SubtitleTrackInfo> SubtitleTracks => _subtitleTracks;
    public IReadOnlyList<AudioTrackInfo> AudioTracks => _audioTracks;
    public int CurrentSubtitleTrack { get; set; } = -1;
    public int CurrentAudioTrack { get; set; }
    public TimeSpan AudioDelay { get; set; }
    public TimeSpan SubtitleDelay { get; set; }
    public VideoFilterSettings VideoFilters { get; set; } = new();
    public SubtitleStyle SubtitleStyle { get; set; } = new();

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<TimeSpan>? DurationChanged;
    public event EventHandler? MediaEnded;
    public event EventHandler<string>? ErrorOccurred;

    public LibVlcMediaPlayerService(ILogger<LibVlcMediaPlayerService> logger)
    {
        _logger = logger;
        var libVlcDir = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
        LibVLCSharp.Shared.Core.Initialize(libVlcDir);

        var options = new[]
        {
            "--intf=dummy",
            "--no-video-title-show",
            "--file-caching=300",
            "--network-caching=1000",
            "--avcodec-hw=any",           // NVDEC, AMF, Quick Sync
            "--vout=direct3d11",          // GPU rendering on Windows
            "--audio-filter=compressor",
            "--sub-autodetect-file",
            "--freetype-rel-fontsize=16"
        };

        _libVlc = new LibVLC(options);
    }

    public MediaPlayer CreateAndAttachPlayer()
    {
        var player = new MediaPlayer(_libVlc);
        AttachPlayer(player);
        return player;
    }

    public void AttachPlayer(MediaPlayer player)
    {
        _player = player;
        _player.EndReached += OnEndReached;
        _player.EncounteredError += OnError;
        _player.Buffering += OnBuffering;
        _player.Playing += OnPlaying;
        _player.Paused += OnPaused;
        _player.Stopped += OnStopped;

        _positionTimer = new System.Timers.Timer(250);
        _positionTimer.Elapsed += (_, _) => UpdatePosition();
        _positionTimer.Start();
    }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            _currentMedia?.Dispose();
            _currentMedia = new VlcMedia(_libVlc, path, FromType.FromPath);
            ConfigureMedia(_currentMedia);
            if (_player is not null)
                _player.Media = _currentMedia;

            CurrentMedia = new MediaItem
            {
                FilePath = path,
                Title = Path.GetFileNameWithoutExtension(path),
                FileSizeBytes = new FileInfo(path).Length,
                Type = IsAudioFile(path) ? DomainMediaType.Audio : DomainMediaType.Video
            };

            RefreshTracks();
        }, cancellationToken);
    }

    public async Task LoadStreamAsync(string url, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            _currentMedia?.Dispose();
            _currentMedia = new VlcMedia(_libVlc, url, FromType.FromLocation);
            ConfigureMedia(_currentMedia);
            if (_player is not null)
                _player.Media = _currentMedia;

            CurrentMedia = new MediaItem
            {
                FilePath = url,
                Title = url,
                Type = DomainMediaType.Video
            };
        }, cancellationToken);
    }

    public void Play() => _player?.Play();
    public void Pause() => _player?.Pause();
    public void Stop() => _player?.Stop();

    public void TogglePlayPause()
    {
        if (_player is null) return;
        if (_player.IsPlaying) Pause();
        else Play();
    }

    public void Seek(TimeSpan position)
    {
        if (_player is null) return;
        _player.Time = (long)position.TotalMilliseconds;
        Position = position;
        PositionChanged?.Invoke(this, position);
    }

    public void SeekRelative(TimeSpan offset) => Seek(Position + offset);

    public void FrameStep(bool forward)
    {
        if (_player is null) return;
        _player.SetPause(true);
        _player.NextFrame();
    }

    public void SetAbPointA() => _abRepeat.PointA = Position;
    public void SetAbPointB() => _abRepeat.PointB = Position;
    public void ClearAbRepeat() { _abRepeat.PointA = null; _abRepeat.PointB = null; }

    public async Task LoadSubtitleAsync(string path)
    {
        if (_player is null) return;
        await Task.Run(() => _player.AddSlave(MediaSlaveType.Subtitle, path, true));
        RefreshTracks();
    }

    public void SelectAudioTrack(int trackIndex)
    {
        if (_player is null) return;
        CurrentAudioTrack = trackIndex;
        InvokePlayerMember("SetAudioTrack", trackIndex);
        SetPlayerProperty("AudioTrack", trackIndex);
    }

    public void SelectSubtitleTrack(int trackIndex)
    {
        if (_player is null) return;
        CurrentSubtitleTrack = trackIndex;
        InvokePlayerMember("SetSpu", trackIndex);
        SetPlayerProperty("Spu", trackIndex);
    }

    public void ApplyAudioDelay()
    {
        if (_player is null) return;
        SetPlayerProperty("AudioDelay", AudioDelay.Ticks / 10);
    }

    public void ApplySubtitleDelay()
    {
        if (_player is null) return;
        SubtitleStyle.DelayMs = SubtitleDelay.TotalMilliseconds;
        SetPlayerProperty("SpuDelay", SubtitleDelay.Ticks / 10);
    }

    public void SetHardwareAcceleration(bool enabled)
    {
        _hardwareAcceleration = enabled;
        _logger.LogInformation("Hardware acceleration {Status}", enabled ? "enabled" : "disabled");
    }

    public async Task<byte[]?> TakeScreenshotAsync()
    {
        if (_player is null) return null;
        var tcs = new TaskCompletionSource<byte[]?>();
        _player.TakeSnapshot(0, $"{Path.GetTempPath()}dplayer_snap.png", 0, 0);
        await Task.Delay(500);
        var path = Directory.GetFiles(Path.GetTempPath(), "dplayer_snap*.png").FirstOrDefault();
        if (path is not null && File.Exists(path))
        {
            var bytes = await File.ReadAllBytesAsync(path);
            File.Delete(path);
            return bytes;
        }
        return null;
    }

    public void ApplyEqualizer(IReadOnlyList<EqualizerBand> bands)
    {
        if (_player is null) return;
        var equalizer = new Equalizer();
        foreach (var band in bands)
        {
            var index = FindBandIndex(equalizer, band.FrequencyHz);
            if (index >= 0)
                equalizer.SetAmp((float)band.GainDb, (uint)index);
        }
        _player.SetEqualizer(equalizer);
    }

    public void ApplyVideoFilters()
    {
        if (_player is null) return;

        _player.SetAdjustFloat(VideoAdjustOption.Enable, 1f);
        _player.SetAdjustFloat(VideoAdjustOption.Brightness, (float)Math.Clamp(VideoFilters.Brightness, 0, 2));
        _player.SetAdjustFloat(VideoAdjustOption.Contrast, (float)Math.Clamp(VideoFilters.Contrast, 0, 2));
        _player.SetAdjustFloat(VideoAdjustOption.Saturation, (float)Math.Clamp(VideoFilters.Saturation, 0, 2));
        _player.SetAdjustFloat(VideoAdjustOption.Gamma, (float)Math.Clamp(VideoFilters.Gamma, 0, 2));
        _player.SetAdjustFloat(VideoAdjustOption.Hue, (float)VideoFilters.Hue);
        SetPlayerProperty("AspectRatio", GetAspectRatioValue(VideoFilters.AspectRatio));
    }

    public void SetVolume()
    {
        if (_player is null) return;
        _player.Volume = IsMuted ? 0 : (int)Math.Clamp(Volume * 3, 0, 300);
    }

    public void SetPlaybackRate()
    {
        if (_player is null) return;
        _player.SetRate((float)PlaybackSpeed);
    }

    public void DisposePlayer()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _player?.Dispose();
        _currentMedia?.Dispose();
        _libVlc.Dispose();
    }

    private void ConfigureMedia(VlcMedia media)
    {
        media.AddOption(":network-caching=3000");
        if (_hardwareAcceleration)
            media.AddOption(":avcodec-hw=any");
    }

    private void RefreshTracks()
    {
        _subtitleTracks.Clear();
        _audioTracks.Clear();
        if (_player?.Media is null) return;

        foreach (var track in _player.Media.Tracks)
        {
            if (track.TrackType == TrackType.Audio)
            {
                _audioTracks.Add(new AudioTrackInfo
                {
                    Index = (int)track.Id,
                    Language = track.Language ?? "und",
                    Codec = track.Codec.ToString(),
                    Label = $"Audio {track.Id} ({track.Language})"
                });
            }
            else if (track.TrackType == TrackType.Text)
            {
                _subtitleTracks.Add(new SubtitleTrackInfo
                {
                    Index = (int)track.Id,
                    Language = track.Language ?? "und",
                    Label = $"Subtitle {track.Id} ({track.Language})"
                });
            }
        }
    }

    private void InvokePlayerMember(string methodName, int value)
    {
        try
        {
            var method = _player?.GetType().GetMethod(methodName, [typeof(int)]);
            method?.Invoke(_player, [value]);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LibVLC player method {MethodName} was not applied", methodName);
        }
    }

    private void SetPlayerProperty(string propertyName, object value)
    {
        try
        {
            var property = _player?.GetType().GetProperty(propertyName);
            if (property is not { CanWrite: true }) return;

            var converted = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(_player, converted);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LibVLC player property {PropertyName} was not applied", propertyName);
        }
    }

    private static string? GetAspectRatioValue(AspectRatioMode aspectRatio) => aspectRatio switch
    {
        AspectRatioMode.Ratio16x9 => "16:9",
        AspectRatioMode.Ratio4x3 => "4:3",
        AspectRatioMode.Ratio21x9 => "21:9",
        AspectRatioMode.Stretch => null,
        AspectRatioMode.Fill => null,
        _ => null
    };

    private void UpdatePosition()
    {
        if (_player is null || !_player.IsPlaying) return;
        Position = TimeSpan.FromMilliseconds(_player.Time);
        var duration = TimeSpan.FromMilliseconds(_player.Length);
        if (duration != Duration)
        {
            Duration = duration;
            DurationChanged?.Invoke(this, duration);
        }
        PositionChanged?.Invoke(this, Position);

        if (_abRepeat.IsActive && Position >= _abRepeat.PointB)
            Seek(_abRepeat.PointA!.Value);
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        if (LoopMode == LoopMode.Single && _player is not null)
        {
            _player.Time = 0;
            _player.Play();
            return;
        }
        SetState(PlaybackState.Stopped);
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnError(object? sender, EventArgs e)
    {
        SetState(PlaybackState.Error);
        ErrorOccurred?.Invoke(this, "Playback error occurred");
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e) =>
        SetState(e.Cache >= 100 ? PlaybackState.Playing : PlaybackState.Buffering);

    private void OnPlaying(object? sender, EventArgs e) => SetState(PlaybackState.Playing);
    private void OnPaused(object? sender, EventArgs e) => SetState(PlaybackState.Paused);
    private void OnStopped(object? sender, EventArgs e) => SetState(PlaybackState.Stopped);

    private void SetState(PlaybackState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static bool IsAudioFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".aac" or ".flac" or ".wav" or ".ogg" or ".m4a";
    }

    private static int FindBandIndex(Equalizer equalizer, int frequencyHz)
    {
        for (uint i = 0; i < equalizer.BandCount; i++)
        {
            if (Math.Abs(equalizer.BandFrequency(i) - frequencyHz) < 100)
                return (int)i;
        }
        return -1;
    }

    public void Dispose() => DisposePlayer();
}
