using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DPlayer.App.Services;
using DPlayer.App.ViewModels;
using DPlayer.Core.Interfaces;
using DPlayer.Infrastructure.Playback;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace DPlayer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _hideControlsTimer;
    private MediaPlayer? _mediaPlayer;
    private bool _isSeekingWithSlider;
    private WindowState _windowStateBeforeFullscreen = WindowState.Normal;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;

        _hideControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideControlsTimer.Tick += (_, _) =>
        {
            if (_viewModel.IsFullscreen && _viewModel.IsPlaying)
                _viewModel.IsControlsVisible = false;
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
            {
                ApplyFullscreenState();
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.EnableMica(this);
        UpdateWindowChromeState();

        if (App.Services.GetRequiredService<IMediaPlayerService>() is LibVlcMediaPlayerService playerService)
        {
            _mediaPlayer = playerService.CreateAndAttachPlayer();
            VideoView.MediaPlayer = _mediaPlayer;
        }
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await _viewModel.SavePositionAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsFullscreen)
            return;

        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var shortcuts = _settings.Settings.KeyboardShortcuts;

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                _viewModel.OpenFolderCommand.Execute(null);
            else
                _viewModel.OpenFileCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            _viewModel.OpenSubtitleSearchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (KeyboardShortcutHandler.TryHandleShortcut(e, _viewModel, shortcuts))
            e.Handled = true;
    }

    private void VideoArea_MouseMove(object sender, MouseEventArgs e)
    {
        _viewModel.IsControlsVisible = true;
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void VideoArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            _viewModel.ToggleFullscreenCommand.Execute(null);
        else
            _viewModel.TogglePlayPauseCommand.Execute(null);
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || !_isSeekingWithSlider) return;
        _viewModel.SeekToPositionCommand.Execute(e.NewValue);
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _isSeekingWithSlider = true;

    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isSeekingWithSlider = false;
        _viewModel.SeekToPositionCommand.Execute(((System.Windows.Controls.Slider)sender).Value);
    }

    private void SeekSlider_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageDown or Key.PageUp)
            _viewModel.SeekToPositionCommand.Execute(((System.Windows.Controls.Slider)sender).Value);
    }

    private void SpeedText_Click(object sender, MouseButtonEventArgs e) =>
        _viewModel.CyclePlaybackSpeedCommand.Execute(null);

    private void MuteButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsMuted = !_viewModel.IsMuted;

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            await _viewModel.HandleFileDropCommand.ExecuteAsync(files);
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateWindowChromeState();

    private void ApplyFullscreenState()
    {
        if (_viewModel.IsFullscreen)
        {
            _windowStateBeforeFullscreen = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowState = WindowState.Maximized;
        }
        else
        {
            Topmost = false;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _windowStateBeforeFullscreen;
        }

        UpdateWindowChromeState();
    }

    private void UpdateWindowChromeState()
    {
        if (!IsLoaded)
            return;

        var isFullscreen = _viewModel.IsFullscreen;
        var isMaximized = WindowState == WindowState.Maximized;
        TitleBarRow.Height = isFullscreen
            ? new GridLength(0)
            : (GridLength)FindResource("TitleBarHeight");
        TitleBar.Visibility = isFullscreen ? Visibility.Collapsed : Visibility.Visible;
        RootBorder.CornerRadius = isFullscreen || isMaximized
            ? new CornerRadius(0)
            : (CornerRadius)FindResource("WindowCornerRadius");
        RootBorder.BorderThickness = isFullscreen ? new Thickness(0) : new Thickness(1);
        MaximizeButton.Content = isMaximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = isMaximized ? "Restore" : "Maximize";
        FullscreenButton.Content = isFullscreen ? "\uE73F" : "\uE740";
        FullscreenButton.ToolTip = isFullscreen ? "Exit Fullscreen (F)" : "Fullscreen (F)";
    }
}
