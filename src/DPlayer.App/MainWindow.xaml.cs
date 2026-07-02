using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DPlayer.App.Services;
using DPlayer.App.ViewModels;
using DPlayer.Core.Enums;
using DPlayer.Core.Interfaces;
using DPlayer.Infrastructure.Playback;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace DPlayer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly IMediaPlayerService _playerService;
    private readonly DispatcherTimer _hideControlsTimer;
    private MediaPlayer? _mediaPlayer;
    private bool _isSeekingWithSlider;
    private WindowState _windowStateBeforeFullscreen = WindowState.Normal;
    private WindowStyle _windowStyleBeforeFullscreen = WindowStyle.None;
    private ResizeMode _resizeModeBeforeFullscreen = ResizeMode.CanResize;
    private bool _topmostBeforeFullscreen;

    public MainWindow(MainViewModel viewModel, ISettingsService settings, IMediaPlayerService playerService)
    {
        _viewModel = viewModel;
        _settings = settings;
        _playerService = playerService;
        DataContext = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;

        _hideControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideControlsTimer.Tick += (_, _) =>
        {
            if (_viewModel.IsFullscreen)
                _viewModel.IsControlsVisible = false;
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
                ApplyFullscreenState();
        };

        _playerService.StateChanged += OnPlayerStateChanged;
        VideoView.SizeChanged += (_, _) => ScheduleVideoBackgroundFix();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.EnableMica(this);
        UpdateWindowChromeState();

        if (_playerService is LibVlcMediaPlayerService vlcService)
        {
            _mediaPlayer = vlcService.CreateAndAttachPlayer();
            VideoView.MediaPlayer = _mediaPlayer;
            ScheduleVideoBackgroundFix();
        }
    }

    private void OnPlayerStateChanged(object? sender, PlaybackState state)
    {
        if (state is PlaybackState.Playing or PlaybackState.Buffering)
            ScheduleVideoBackgroundFix();
    }

    private void ScheduleVideoBackgroundFix()
    {
        if (!IsLoaded)
            return;

        SetNativeVideoHostBackgroundToBlack();
        Dispatcher.BeginInvoke(SetNativeVideoHostBackgroundToBlack, DispatcherPriority.Loaded);
        QueueDelayedVideoBackgroundFix(100);
        QueueDelayedVideoBackgroundFix(400);
    }

    private void QueueDelayedVideoBackgroundFix(int delayMs)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            SetNativeVideoHostBackgroundToBlack();
        };
        timer.Start();
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

    private void PlayerMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.ContextMenu is null)
            return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.IsFullscreen)
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

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
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
            return;
        }

        _viewModel.TogglePlayPauseCommand.Execute(null);
        e.Handled = true;
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && IsMouseOverVideoArea(e))
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
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

    private async void PlaylistItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.PlaylistVm.SelectedEntry is not null)
            await _viewModel.OpenFileAsync(_viewModel.PlaylistVm.SelectedEntry.FilePath);
    }

    private async void LibraryItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: Core.Models.MediaItem item })
            await _viewModel.OpenFileAsync(item.FilePath);
    }

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
            _windowStyleBeforeFullscreen = WindowStyle;
            _resizeModeBeforeFullscreen = ResizeMode;
            _topmostBeforeFullscreen = Topmost;
            _viewModel.IsPlaylistPanelOpen = false;
            _viewModel.IsLibraryPanelOpen = false;
            _viewModel.IsControlsVisible = false;
            _hideControlsTimer.Stop();
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowState = WindowState.Maximized;
        }
        else
        {
            Topmost = _topmostBeforeFullscreen;
            WindowStyle = _windowStyleBeforeFullscreen;
            ResizeMode = _resizeModeBeforeFullscreen;
            _viewModel.IsControlsVisible = true;
            _hideControlsTimer.Stop();
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

    private bool IsMouseOverVideoArea(MouseButtonEventArgs e)
    {
        var position = e.GetPosition(VideoOverlay);
        return position.X >= 0 &&
               position.Y >= 0 &&
               position.X <= VideoOverlay.ActualWidth &&
               position.Y <= VideoOverlay.ActualHeight;
    }

    private void SetNativeVideoHostBackgroundToBlack()
    {
        try
        {
            var blackBrush = NativeMethods.GetStockObject(NativeMethods.BlackBrush);

            if (_mediaPlayer is { Hwnd: var playerHwnd } && playerHwnd != IntPtr.Zero)
                PaintHwndBackgroundBlack(playerHwnd, blackBrush);

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            NativeMethods.EnumChildWindows(hwnd, (child, lParam) =>
            {
                PaintHwndBackgroundBlack(child, lParam);
                return true;
            }, blackBrush);
        }
        catch
        {
            // WPF parent is already black; this fixes native VLC host letterbox repainting.
        }
    }

    private static void PaintHwndBackgroundBlack(IntPtr hwnd, IntPtr blackBrush)
    {
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.SetClassLongPtr(hwnd, NativeMethods.GclpHbrBackground, blackBrush);
        NativeMethods.InvalidateRect(hwnd, IntPtr.Zero, true);
    }

    private static class NativeMethods
    {
        public const int GclpHbrBackground = -10;
        public const int BlackBrush = 4;

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLongPtr", SetLastError = true)]
        public static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLong", SetLastError = true)]
        public static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr GetStockObject(int fnObject);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        public static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
            IntPtr.Size == 8
                ? SetClassLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }
}
