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
                WindowStyle = _viewModel.IsFullscreen ? WindowStyle.None : WindowStyle.None;
                WindowState = _viewModel.IsFullscreen ? WindowState.Maximized : WindowState.Normal;
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.EnableMica(this);

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
        if (!IsLoaded) return;
        _viewModel.SeekToPositionCommand.Execute(e.NewValue);
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
}
