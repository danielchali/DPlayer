using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DPlayer.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DPlayer.App.Services;

public interface IDialogService
{
    Task<string?> OpenFileAsync(string filter);
    Task<string?> OpenFolderAsync();
    Task<string?> SaveFileAsync(string filter, string defaultName);
    Task<string?> PromptTextAsync(string title, string message, string defaultValue = "");
    void ShowMessage(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    void ShowSettingsWindow();
    void ShowSubtitleSearchWindow();
}

public sealed class DialogService : IDialogService
{
    public Task<string?> OpenFileAsync(string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Multiselect = false
        };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public Task<string?> OpenFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog();
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FolderName : null);
    }

    public Task<string?> SaveFileAsync(string filter, string defaultName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            FileName = defaultName
        };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public Task<string?> PromptTextAsync(string title, string message, string defaultValue = "")
    {
        var input = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            MinWidth = 420,
            Margin = new Thickness(0, 8, 0, 12)
        };

        var window = new Window
        {
            Title = title,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Background = Application.Current.TryFindResource("BackgroundBrush") as Brush,
            Foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush,
            Content = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    new System.Windows.Controls.TextBlock { Text = message },
                    input,
                    new System.Windows.Controls.StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            new System.Windows.Controls.Button
                            {
                                Content = "Open",
                                IsDefault = true,
                                MinWidth = 82,
                                Margin = new Thickness(0, 0, 8, 0)
                            },
                            new System.Windows.Controls.Button
                            {
                                Content = "Cancel",
                                IsCancel = true,
                                MinWidth = 82
                            }
                        }
                    }
                }
            }
        };

        var buttons = ((System.Windows.Controls.StackPanel)((System.Windows.Controls.StackPanel)window.Content).Children[2]).Children;
        ((System.Windows.Controls.Button)buttons[0]).Click += (_, _) => window.DialogResult = true;
        ((System.Windows.Controls.Button)buttons[1]).Click += (_, _) => window.DialogResult = false;

        input.SelectAll();
        input.Focus();
        var result = window.ShowDialog() == true ? input.Text.Trim() : null;
        return Task.FromResult(string.IsNullOrWhiteSpace(result) ? null : result);
    }

    public void ShowMessage(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public Task<bool> ConfirmAsync(string title, string message) =>
        Task.FromResult(MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);

    public void ShowSettingsWindow()
    {
        var viewModel = App.Services.GetRequiredService<ViewModels.SettingsViewModel>();
        viewModel.Reload();
        var window = new Views.SettingsWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = viewModel
        };
        window.ShowDialog();
    }

    public void ShowSubtitleSearchWindow()
    {
        var window = new Views.SubtitleSearchWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = App.Services.GetRequiredService<ViewModels.SubtitleSearchViewModel>()
        };
        window.ShowDialog();
    }
}

public interface IFileService
{
    bool IsMediaFile(string path);
    bool IsSubtitleFile(string path);
    string GetMediaFilter();
    string GetSubtitleFilter();
}

public sealed class FileService : IFileService
{
    private static readonly string[] MediaExtensions =
    [
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mpeg", ".mpg",
        ".mp3", ".aac", ".flac", ".wav", ".ogg", ".m4a"
    ];

    private static readonly string[] SubtitleExtensions =
        [".srt", ".ass", ".ssa", ".sub", ".vtt"];

    public bool IsMediaFile(string path) =>
        MediaExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public bool IsSubtitleFile(string path) =>
        SubtitleExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public string GetMediaFilter() =>
        "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.mpeg;*.mpg;*.mp3;*.aac;*.flac;*.wav;*.ogg;*.m4a|All Files|*.*";

    public string GetSubtitleFilter() =>
        "Subtitle Files|*.srt;*.ass;*.ssa;*.sub;*.vtt|All Files|*.*";
}

public sealed class ThemeService
{
    private const string ThemePathPrefix = "Themes/Colors.";

    public void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        var themeFile = theme switch
        {
            AppTheme.DarkGreen => "Themes/Colors.DarkGreen.xaml",
            AppTheme.EmeraldGreen => "Themes/Colors.EmeraldGreen.xaml",
            AppTheme.NeonGreen => "Themes/Colors.NeonGreen.xaml",
            AppTheme.Light => "Themes/Colors.Light.xaml",
            _ => "Themes/Colors.DarkGreen.xaml"
        };

        var nextTheme = new ResourceDictionary
        {
            Source = new Uri(themeFile, UriKind.Relative)
        };

        var dictionaries = app.Resources.MergedDictionaries;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is not null &&
                source.Contains(ThemePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[i] = nextTheme;
                return;
            }
        }

        dictionaries.Insert(0, nextTheme);
    }
}

public static class WindowHelper
{
    public static void EnableMica(Window window, bool darkMode = true)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            int backdropType = 2; // Mica
            DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
            int dark = darkMode ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        }
        catch
        {
            // Mica not supported on older Windows versions
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}

public static class KeyboardShortcutHandler
{
    public static bool TryHandleShortcut(
        System.Windows.Input.KeyEventArgs e,
        ViewModels.MainViewModel vm,
        Dictionary<string, string> shortcuts)
    {
        var key = GetKeyString(e);
        if (shortcuts.TryGetValue("PlayPause", out var playPause) && key == playPause)
        {
            vm.TogglePlayPauseCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("Fullscreen", out var fs) && key == fs)
        {
            vm.ToggleFullscreenCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("SeekBackward", out var sb) && key == sb)
        {
            vm.SeekBackwardCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("SeekForward", out var sf) && key == sf)
        {
            vm.SeekForwardCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("VolumeUp", out var vu) && key == vu)
        {
            vm.VolumeUpCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("VolumeDown", out var vd) && key == vd)
        {
            vm.VolumeDownCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("Stop", out var stop) && key == stop)
        {
            vm.StopCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("Mute", out var mute) && key == mute)
        {
            vm.ToggleMuteCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("OpenUrl", out var openUrl) && key == openUrl)
        {
            vm.OpenUrlCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("FrameBack", out var frameBack) && key == frameBack)
        {
            vm.FrameBackCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("FrameForward", out var frameForward) && key == frameForward)
        {
            vm.FrameForwardCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("SpeedDown", out var speedDown) && key == speedDown)
        {
            vm.SpeedDownCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("SpeedUp", out var speedUp) && key == speedUp)
        {
            vm.SpeedUpCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("Screenshot", out var ss) && key == ss)
        {
            vm.TakeScreenshotCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("CycleAudioTrack", out var audioTrack) && key == audioTrack)
        {
            vm.CycleAudioTrackCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("CycleSubtitleTrack", out var subtitleTrack) && key == subtitleTrack)
        {
            vm.CycleSubtitleTrackCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("AudioDelayDown", out var audioDelayDown) && key == audioDelayDown)
        {
            vm.AdjustAudioDelayCommand.Execute("-50");
            return true;
        }
        if (shortcuts.TryGetValue("AudioDelayUp", out var audioDelayUp) && key == audioDelayUp)
        {
            vm.AdjustAudioDelayCommand.Execute("50");
            return true;
        }
        if (shortcuts.TryGetValue("SubtitleDelayDown", out var subtitleDelayDown) && key == subtitleDelayDown)
        {
            vm.AdjustSubtitleDelayCommand.Execute("-50");
            return true;
        }
        if (shortcuts.TryGetValue("SubtitleDelayUp", out var subtitleDelayUp) && key == subtitleDelayUp)
        {
            vm.AdjustSubtitleDelayCommand.Execute("50");
            return true;
        }
        if (shortcuts.TryGetValue("TogglePlaylist", out var pl) && key == pl)
        {
            vm.TogglePlaylistPanelCommand.Execute(null);
            return true;
        }
        if (shortcuts.TryGetValue("AbRepeat", out var ab) && key == ab)
        {
            vm.SetAbPointACommand.Execute(null);
            return true;
        }
        return false;
    }

    private static string GetKeyString(System.Windows.Input.KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            parts.Add("Ctrl");
        if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            parts.Add("Shift");
        if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
            parts.Add("Alt");

        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
