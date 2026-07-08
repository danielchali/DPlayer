using LibVLCSharp.Shared;
using System;
using System.IO;

namespace DPlayer.App.Services;

public class MediaPlayerService : IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private readonly object _initLock = new object();
    private readonly List<string> _playlist = new List<string>();
    private int _currentIndex = -1;

    public event EventHandler? TimeChanged;
    public event EventHandler? LengthChanged;
    public event EventHandler? PlayingChanged;
    public event EventHandler? EndReached;

    public bool IsInitialized => _libVLC != null;
    public bool HasNext => _currentIndex >= 0 && _currentIndex < _playlist.Count - 1;
    public bool HasPrevious => _currentIndex > 0 && _playlist.Count > 0;
    public string? CurrentTrackPath => _currentIndex >= 0 && _currentIndex < _playlist.Count ? _playlist[_currentIndex] : null;

    public void SetPlaylist(List<string> playlist, int startIndex)
    {
        _playlist.Clear();
        _playlist.AddRange(playlist);
        _currentIndex = (startIndex >= 0 && startIndex < _playlist.Count) ? startIndex : 0;
    }

    public void MoveNext()
    {
        if (HasNext)
        {
            _currentIndex++;
        }
    }

    public void MovePrevious()
    {
        if (HasPrevious)
        {
            _currentIndex--;
        }
    }

    public void Initialize()
    {
        lock (_initLock)
        {
            if (_libVLC == null)
            {
                string libVlcDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "libvlc",
                    Environment.Is64BitProcess ? "win-x64" : "win-x86"
                );

                if (Directory.Exists(libVlcDirectory))
                {
                    Core.Initialize(libVlcDirectory);
                }
                else
                {
                    Core.Initialize();
                }

                _libVLC = new LibVLC(enableDebugLogs: false);
                _mediaPlayer = new MediaPlayer(_libVLC);
                
                _mediaPlayer.TimeChanged += (s, e) => TimeChanged?.Invoke(s, e);
                _mediaPlayer.LengthChanged += (s, e) => LengthChanged?.Invoke(s, e);
                _mediaPlayer.Playing += (s, e) => PlayingChanged?.Invoke(s, e);
                _mediaPlayer.EndReached += (s, e) => EndReached?.Invoke(s, e);
            }
        }
    }

    public void LoadMedia(string path)
    {
        Initialize();
        _media?.Dispose();

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || 
             uri.Scheme == "rtsp" || uri.Scheme == "rtmp" || uri.Scheme == "ftp"))
        {
            _media = new Media(_libVLC!, uri);
        }
        else
        {
            _media = new Media(_libVLC!, path, FromType.FromPath);
        }

        _mediaPlayer!.Media = _media;
    }

    public void Play()
    {
        _mediaPlayer?.Play();
    }

    public void Pause()
    {
        _mediaPlayer?.Pause();
    }

    public void Stop()
    {
        _mediaPlayer?.Stop();
    }

    public void SetPosition(float position)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Position = position;
        }
    }

    public float GetPosition()
    {
        return _mediaPlayer?.Position ?? 0f;
    }

    public void SetVolume(int volume)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Volume = volume;
        }
    }

    public int GetVolume()
    {
        return _mediaPlayer?.Volume ?? 100;
    }

    public void SetRate(float rate)
    {
        _mediaPlayer?.SetRate(rate);
    }

    public float GetRate()
    {
        return _mediaPlayer?.Rate ?? 1f;
    }

    public long GetTime()
    {
        return _mediaPlayer?.Time ?? 0;
    }

    public long GetLength()
    {
        return _mediaPlayer?.Length ?? 0;
    }

    public bool IsPlaying()
    {
        return _mediaPlayer?.IsPlaying ?? false;
    }

    public MediaPlayer? GetMediaPlayer()
    {
        return _mediaPlayer;
    }

    public void Dispose()
    {
        _media?.Dispose();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
    }
}
