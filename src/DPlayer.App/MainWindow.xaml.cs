using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DPlayer.App.Services;
using LibVLCSharp.Shared;

namespace DPlayer.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MediaPlayerService _mediaPlayerService;
    private bool _isMuted;
    private bool _isUpdatingProgress;
    private string? _lastPlayedFile;
    private bool _isFullScreen;
    private readonly DispatcherTimer _fullScreenHideTimer;
    private bool _controlsVisible;
    private string? _savedTitle;
    private static readonly string LastPlayedFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DPlayer",
        "last_played.txt"
    );

    // Threshold in pixels from edge to trigger reveal
    private const double EdgeThreshold = 60;

    public MainWindow()
    {
        _mediaPlayerService = new MediaPlayerService();
        InitializeComponent();
        Title = "DPlayer - No media loaded";

        _fullScreenHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _fullScreenHideTimer.Tick += FullScreenHideTimer_Tick;
        
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        KeyDown += MainWindow_KeyDown;
        MouseMove += MainWindow_MouseMove; // catches moves outside the VLC area
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => _mediaPlayerService.Initialize());
        VideoView.MediaPlayer = _mediaPlayerService.GetMediaPlayer();
        
        _mediaPlayerService.TimeChanged += MediaPlayerService_TimeChanged;
        _mediaPlayerService.LengthChanged += MediaPlayerService_LengthChanged;
        _mediaPlayerService.PlayingChanged += MediaPlayerService_PlayingChanged;
        _mediaPlayerService.EndReached += MediaPlayerService_EndReached;

        _lastPlayedFile = LoadLastPlayedFile();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _mediaPlayerService.Dispose();
    }

    private void MediaPlayerService_TimeChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_isUpdatingProgress)
            {
                var time = _mediaPlayerService.GetTime();
                var length = _mediaPlayerService.GetLength();
                
                CurrentTimeText.Text = FormatTime(time);
                
                if (length > 0)
                {
                    ProgressSlider.Value = (double)time / length * 100;
                }
            }
        });
    }

    private void MediaPlayerService_LengthChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var length = _mediaPlayerService.GetLength();
            TotalTimeText.Text = FormatTime(length);
        });
    }

    private void MediaPlayerService_PlayingChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var isPlaying = _mediaPlayerService.IsPlaying();
            PlayPauseButton.Content = isPlaying ? "⏸" : "▶";
        });
    }

    private void MediaPlayerService_EndReached(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PlayPauseButton.Content = "▶";
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
        });
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "All Media Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.mpg;*.mpeg;*.m4v;*.3gp;*.ts;*.mts;*.vob;*.asf;*.mp3;*.wav;*.ogg;*.flac;*.aac;*.m4a;*.wma;*.ape;*.opus;*.mka;*.m3u;*.m3u8;*.pls|Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.mpg;*.mpeg;*.m4v;*.3gp;*.ts;*.mts;*.vob;*.asf|Audio Files|*.mp3;*.wav;*.ogg;*.flac;*.aac;*.m4a;*.wma;*.ape;*.opus;*.mka|Playlists|*.m3u;*.m3u8;*.pls|All Files|*.*",
            Title = "Open Media File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadMedia(openFileDialog.FileName);
        }
    }

    private void OpenUrlButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Open URL",
            Width = 500,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = System.Windows.Media.Brushes.LightGray
        };

        var stackPanel = new StackPanel { Margin = new Thickness(20) };
        
        var label = new TextBlock { Text = "Enter media URL:", Margin = new Thickness(0, 0, 0, 10) };
        var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        var okButton = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right };

        okButton.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                LoadMedia(textBox.Text);
                dialog.Close();
            }
        };

        stackPanel.Children.Add(label);
        stackPanel.Children.Add(textBox);
        stackPanel.Children.Add(okButton);
        dialog.Content = stackPanel;
        dialog.ShowDialog();
    }

    private void LibraryButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Library feature coming soon!", "DPlayer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadMedia(string path)
    {
        try
        {
            _mediaPlayerService.LoadMedia(path);
            NoMediaPanel.Visibility = Visibility.Collapsed;
            VideoView.Visibility = Visibility.Visible;
            ProgressTimePanel.Visibility = Visibility.Visible;
            Title = $"{GetMediaDisplayName(path)} - DPlayer";
            _mediaPlayerService.Play();

            _lastPlayedFile = path;
            SaveLastPlayedFile(path);

            // If we are currently in fullscreen, move controls to the VideoOverlayGrid to bypass airspace
            if (_isFullScreen && RootGrid.Children.Contains(PlaybackControls))
            {
                RootGrid.Children.Remove(PlaybackControls);
                VideoOverlayGrid.Children.Add(PlaybackControls);
                Grid.SetRow(PlaybackControls, 0);
                PlaybackControls.VerticalAlignment = VerticalAlignment.Bottom;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading media: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetMediaDisplayName(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return Uri.UnescapeDataString(uri.Segments.LastOrDefault() ?? path);
        }
        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return path;
        }
    }

    private void SaveLastPlayedFile(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(LastPlayedFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(LastPlayedFilePath, path);
        }
        catch { }
    }

    private string? LoadLastPlayedFile()
    {
        try
        {
            if (File.Exists(LastPlayedFilePath))
            {
                return File.ReadAllText(LastPlayedFilePath);
            }
        }
        catch { }
        return null;
    }

    private bool IsValidMediaSource(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || 
             uri.Scheme == "rtsp" || uri.Scheme == "rtmp" || uri.Scheme == "ftp"))
        {
            return true;
        }
        return File.Exists(path);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (NoMediaPanel.Visibility == Visibility.Visible)
        {
            if (IsValidMediaSource(_lastPlayedFile))
            {
                LoadMedia(_lastPlayedFile!);
            }
            else
            {
                OpenFileButton_Click(sender, e);
            }
            return;
        }

        if (_mediaPlayerService.IsPlaying())
        {
            _mediaPlayerService.Pause();
        }
        else
        {
            _mediaPlayerService.Play();
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _mediaPlayerService.Stop();
        PlayPauseButton.Content = "▶";
        ProgressSlider.Value = 0;
        CurrentTimeText.Text = "00:00";

        // Restore controls parenting if it was in the video overlay
        if (VideoOverlayGrid.Children.Contains(PlaybackControls))
        {
            VideoOverlayGrid.Children.Remove(PlaybackControls);
            RootGrid.Children.Add(PlaybackControls);
            // If in fullscreen, it should remain overlaid at the bottom of the RootGrid
            if (_isFullScreen)
            {
                Grid.SetRow(PlaybackControls, 0);
                PlaybackControls.VerticalAlignment = VerticalAlignment.Bottom;
            }
            else
            {
                Grid.SetRow(PlaybackControls, 1);
                PlaybackControls.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        VideoView.Visibility = Visibility.Collapsed;
        NoMediaPanel.Visibility = Visibility.Visible;
        ProgressTimePanel.Visibility = Visibility.Collapsed;
        Title = "DPlayer - No media loaded";
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Previous track feature coming soon!", "DPlayer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Next track feature coming soon!", "DPlayer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isUpdatingProgress = true;
        var position = (float)ProgressSlider.Value / 100;
        _mediaPlayerService.SetPosition(position);
        _isUpdatingProgress = false;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayerService == null) return;
        _mediaPlayerService.SetVolume((int)e.NewValue);
        if (MuteButton != null)
        {
            if (e.NewValue == 0)
            {
                MuteButton.Content = "🔇";
            }
            else
            {
                MuteButton.Content = "🔊";
                _isMuted = false;
            }
        }
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isMuted)
        {
            _mediaPlayerService.SetVolume(100);
            VolumeSlider.Value = 100;
            MuteButton.Content = "🔊";
            _isMuted = false;
        }
        else
        {
            _mediaPlayerService.SetVolume(0);
            VolumeSlider.Value = 0;
            MuteButton.Content = "🔇";
            _isMuted = true;
        }
    }

    private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_mediaPlayerService == null) return;
        if (SpeedComboBox?.SelectedItem is ComboBoxItem item)
        {
            float rate = 1.0f;
            if (item.Tag is string rateStr && float.TryParse(rateStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r1))
            {
                rate = r1;
            }
            else if (item.Tag is float r2)
            {
                rate = r2;
            }
            else if (item.Tag is double r3)
            {
                rate = (float)r3;
            }

            _mediaPlayerService.SetRate(rate);
            if (SpeedText != null)
            {
                SpeedText.Text = $"{rate:F1}x";
            }
        }
    }

    private void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isFullScreen)
        {
            EnterFullScreen();
        }
        else
        {
            ExitFullScreen();
        }
    }

    private void ShowOverlayTitleBar()
    {
        OverlayTitleText.Text = Title;
        TitleBarOverlay.Visibility = Visibility.Visible;
    }

    private void HideOverlayTitleBar()
    {
        TitleBarOverlay.Visibility = Visibility.Collapsed;
    }

    private void EnterFullScreen()
    {
        _isFullScreen = true;
        _savedTitle = Title;

        // Transition to true borderless fullscreen (done once, no further WindowStyle changes)
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;

        if (VideoView.Visibility == Visibility.Visible)
        {
            RootGrid.Children.Remove(PlaybackControls);
            VideoOverlayGrid.Children.Add(PlaybackControls);
        }

        Grid.SetRow(PlaybackControls, 0);
        PlaybackControls.VerticalAlignment = VerticalAlignment.Bottom;
        ControlsRow.Height = new System.Windows.GridLength(0);

        PlaybackControls.Visibility = Visibility.Collapsed;
        ProgressTimePanel.Visibility = Visibility.Collapsed;
        HideOverlayTitleBar();
        _controlsVisible = false;
    }

    private void ExitFullScreen()
    {
        _fullScreenHideTimer.Stop();
        _isFullScreen = false;
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.SingleBorderWindow;
        if (_savedTitle != null) Title = _savedTitle;

        HideOverlayTitleBar();

        if (VideoOverlayGrid.Children.Contains(PlaybackControls))
        {
            VideoOverlayGrid.Children.Remove(PlaybackControls);
            RootGrid.Children.Add(PlaybackControls);
        }
        Grid.SetRow(PlaybackControls, 1);
        PlaybackControls.VerticalAlignment = VerticalAlignment.Stretch;
        ControlsRow.Height = System.Windows.GridLength.Auto;

        PlaybackControls.Visibility = Visibility.Visible;
        if (NoMediaPanel.Visibility != Visibility.Visible)
        {
            ProgressTimePanel.Visibility = Visibility.Visible;
        }
        _controlsVisible = false;
    }

    private void MainWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isFullScreen) return;

        var pos = e.GetPosition(this);
        var windowHeight = ActualHeight;

        bool nearEdge = pos.Y >= windowHeight - EdgeThreshold || pos.Y <= EdgeThreshold;

        if (nearEdge)
        {
            // Show both the custom title bar overlay and the bottom controls
            ShowOverlayTitleBar();
            if (!_controlsVisible)
            {
                PlaybackControls.Visibility = Visibility.Visible;
                if (NoMediaPanel.Visibility != Visibility.Visible)
                {
                    ProgressTimePanel.Visibility = Visibility.Visible;
                }
                _controlsVisible = true;
            }
            _fullScreenHideTimer.Stop();
            _fullScreenHideTimer.Start();
        }
        else
        {
            // Middle area — hide everything
            HideOverlayTitleBar();
            if (_controlsVisible)
            {
                PlaybackControls.Visibility = Visibility.Collapsed;
                ProgressTimePanel.Visibility = Visibility.Collapsed;
                _controlsVisible = false;
            }
            _fullScreenHideTimer.Stop();
        }
    }

    private void FullScreenHideTimer_Tick(object? sender, EventArgs e)
    {
        _fullScreenHideTimer.Stop();
        if (!_isFullScreen) return;

        HideOverlayTitleBar();
        if (_controlsVisible)
        {
            PlaybackControls.Visibility = Visibility.Collapsed;
            ProgressTimePanel.Visibility = Visibility.Collapsed;
            _controlsVisible = false;
        }
    }

    private void OverlayMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OverlayRestore_Click(object sender, RoutedEventArgs e)
    {
        ExitFullScreen();
    }

    private void OverlayClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
        }
    }

    private string FormatTime(long milliseconds)
    {
        var seconds = milliseconds / 1000;
        var minutes = seconds / 60;
        var hours = minutes / 60;
        
        if (hours > 0)
        {
            return $"{hours:D2}:{(minutes % 60):D2}:{(seconds % 60):D2}";
        }
        return $"{minutes:D2}:{(seconds % 60):D2}";
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int useImmersiveDarkMode = 1;
            int attribute = 20; // DWMWA_USE_IMMERSIVE_DARK_MODE
            
            // Fallback for older Windows 10 versions (build < 18985)
            if (Environment.OSVersion.Version.Major == 10 && Environment.OSVersion.Version.Build < 18985)
            {
                attribute = 19;
            }
            
            DwmSetWindowAttribute(hwnd, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
        catch { }
    }

    private void ContextMenuSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayerService == null) return;
        if (sender is MenuItem item && item.Tag is string rateStr && float.TryParse(rateStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rate))
        {
            _mediaPlayerService.SetRate(rate);
            if (SpeedText != null)
            {
                SpeedText.Text = $"{rate:F1}x";
            }
            
            // Sync selection in ComboBox
            if (SpeedComboBox != null)
            {
                foreach (ComboBoxItem cbItem in SpeedComboBox.Items)
                {
                    if (cbItem.Tag is string tagStr && tagStr == rateStr)
                    {
                        SpeedComboBox.SelectedItem = cbItem;
                        break;
                    }
                }
            }
        }
    }

    private void ContextMenuVolume_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayerService == null) return;
        if (sender is MenuItem item && item.Tag is string volStr && int.TryParse(volStr, out int vol))
        {
            _mediaPlayerService.SetVolume(vol);
            if (VolumeSlider != null)
            {
                VolumeSlider.Value = vol;
            }
        }
    }

    private void PlayerContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        // Repopulate subtitles from the current media player state
        SubtitlesMenuItem.Items.Clear();

        var noneItem = new MenuItem { Header = "None (Disabled)", Tag = "-1" };
        noneItem.Click += ContextMenuSubtitle_Click;
        SubtitlesMenuItem.Items.Add(noneItem);

        var player = _mediaPlayerService.GetMediaPlayer();
        if (player != null && player.IsPlaying)
        {
            var tracks = player.SpuDescription;
            if (tracks != null)
            {
                foreach (var track in tracks)
                {
                    // Skip the built-in "Disable" entry (id == -1) since we already added "None"
                    if (track.Id == -1) continue;

                    var item = new MenuItem
                    {
                        Header = string.IsNullOrWhiteSpace(track.Name) ? $"Track {track.Id}" : track.Name,
                        Tag = track.Id.ToString()
                    };
                    item.Click += ContextMenuSubtitle_Click;
                    SubtitlesMenuItem.Items.Add(item);
                }
            }

            // Option to load an external subtitle file
            SubtitlesMenuItem.Items.Add(new Separator());
            var loadItem = new MenuItem { Header = "Load Subtitle File..." };
            loadItem.Click += LoadSubtitleFile_Click;
            SubtitlesMenuItem.Items.Add(loadItem);
        }

        // Disable the menu if no tracks available
        SubtitlesMenuItem.IsEnabled = player != null && player.IsPlaying;
    }

    private void ContextMenuSubtitle_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayerService == null) return;
        if (sender is MenuItem item && item.Tag is string idStr && int.TryParse(idStr, out int id))
        {
            var player = _mediaPlayerService.GetMediaPlayer();
            player?.SetSpu(id);
        }
    }

    private void LoadSubtitleFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Subtitle Files|*.srt;*.sub;*.ass;*.ssa;*.vtt;*.idx;*.smi|All Files|*.*",
            Title = "Open Subtitle File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var player = _mediaPlayerService.GetMediaPlayer();
            if (player != null)
            {
                player.AddSlave(MediaSlaveType.Subtitle, new Uri(openFileDialog.FileName).AbsoluteUri, true);
            }
        }
    }
}