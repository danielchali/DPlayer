using LibVLCSharp.Shared;
using System;

namespace DPlayer.App.Services;

public class MediaPlayerService : IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;

    public event EventHandler? TimeChanged;
    public event EventHandler? LengthChanged;
    public event EventHandler? PlayingChanged;
    public event EventHandler? EndReached;

    public bool IsInitialized => _libVLC != null;

    public void Initialize()
    {
        if (_libVLC == null)
        {
            Core.Initialize();
            _libVLC = new LibVLC(enableDebugLogs: false);
            _mediaPlayer = new MediaPlayer(_libVLC);
            
            _mediaPlayer.TimeChanged += (s, e) => TimeChanged?.Invoke(s, e);
            _mediaPlayer.LengthChanged += (s, e) => LengthChanged?.Invoke(s, e);
            _mediaPlayer.Playing += (s, e) => PlayingChanged?.Invoke(s, e);
            _mediaPlayer.EndReached += (s, e) => EndReached?.Invoke(s, e);
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
