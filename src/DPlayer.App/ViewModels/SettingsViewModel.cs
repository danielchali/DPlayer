using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DPlayer.Core.Enums;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using DPlayer.App.Services;

namespace DPlayer.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ThemeService _theme;
    private readonly IUpdateService _updateService;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private AppTheme _selectedTheme;
    [ObservableProperty] private bool _hardwareAcceleration;
    [ObservableProperty] private bool _autoDownloadSubtitles;
    [ObservableProperty] private string _defaultSubtitleLanguage = "eng";
    [ObservableProperty] private bool _resumePlayback;
    [ObservableProperty] private bool _checkForUpdates;
    [ObservableProperty] private bool _shufflePlaylist;
    [ObservableProperty] private bool _loopPlaylist;
    [ObservableProperty] private string? _openSubtitlesApiKey;
    [ObservableProperty] private string? _openSubtitlesUsername;
    [ObservableProperty] private string? _openSubtitlesPassword;
    [ObservableProperty] private bool _audioNormalization;
    [ObservableProperty] private bool _surroundSimulation;
    [ObservableProperty] private double _bassBoost;
    [ObservableProperty] private string _updateStatus = "";

    public ObservableCollection<AppTheme> AvailableThemes { get; } =
    [
        AppTheme.DarkGreen,
        AppTheme.EmeraldGreen,
        AppTheme.NeonGreen,
        AppTheme.Light
    ];

    public ObservableCollection<string> LanguageCodes { get; } =
    [
        "eng", "spa", "fre", "ger", "ita", "por", "rus", "jpn", "kor", "chi"
    ];

    public SettingsViewModel(
        ISettingsService settings,
        ThemeService theme,
        IUpdateService updateService,
        IDialogService dialogs)
    {
        _settings = settings;
        _theme = theme;
        _updateService = updateService;
        _dialogs = dialogs;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settings.Settings;
        SelectedTheme = s.Theme;
        HardwareAcceleration = s.HardwareAcceleration;
        AutoDownloadSubtitles = s.AutoDownloadSubtitles;
        DefaultSubtitleLanguage = s.DefaultSubtitleLanguage;
        ResumePlayback = s.ResumePlayback;
        CheckForUpdates = s.CheckForUpdates;
        ShufflePlaylist = s.ShufflePlaylist;
        LoopPlaylist = s.LoopPlaylist;
        OpenSubtitlesApiKey = s.OpenSubtitlesApiKey;
        OpenSubtitlesUsername = s.OpenSubtitlesUsername;
        OpenSubtitlesPassword = s.OpenSubtitlesPassword;
        AudioNormalization = s.AudioNormalization;
        SurroundSimulation = s.EnableSurroundSimulation;
        BassBoost = s.BassBoost;
    }

    partial void OnSelectedThemeChanged(AppTheme value) => _theme.ApplyTheme(value);

    [RelayCommand]
    private async Task Save()
    {
        var s = _settings.Settings;
        s.Theme = SelectedTheme;
        s.HardwareAcceleration = HardwareAcceleration;
        s.AutoDownloadSubtitles = AutoDownloadSubtitles;
        s.DefaultSubtitleLanguage = DefaultSubtitleLanguage;
        s.ResumePlayback = ResumePlayback;
        s.CheckForUpdates = CheckForUpdates;
        s.ShufflePlaylist = ShufflePlaylist;
        s.LoopPlaylist = LoopPlaylist;
        s.OpenSubtitlesApiKey = OpenSubtitlesApiKey;
        s.OpenSubtitlesUsername = OpenSubtitlesUsername;
        s.OpenSubtitlesPassword = OpenSubtitlesPassword;
        s.AudioNormalization = AudioNormalization;
        s.EnableSurroundSimulation = SurroundSimulation;
        s.BassBoost = BassBoost;
        await _settings.SaveAsync();
        _dialogs.ShowMessage("Settings", "Settings saved successfully.");
    }

    [RelayCommand]
    private async Task CheckUpdates()
    {
        UpdateStatus = "Checking...";
        var info = await _updateService.CheckForUpdatesAsync();
        UpdateStatus = info is not null
            ? $"Update available: {info.Version}"
            : "You are running the latest version.";
    }
}

public partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryService _library;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private ObservableCollection<MediaItem> _searchResults = [];
    [ObservableProperty] private ObservableCollection<MediaItem> _recentFiles = [];
    [ObservableProperty] private ObservableCollection<MediaItem> _favorites = [];
    [ObservableProperty] private ObservableCollection<MediaItem> _watchLater = [];

    public LibraryViewModel(ILibraryService library, IDialogService dialogs)
    {
        _library = library;
        _dialogs = dialogs;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        RecentFiles = new ObservableCollection<MediaItem>(await _library.GetRecentAsync());
        Favorites = new ObservableCollection<MediaItem>(await _library.GetFavoritesAsync());
        WatchLater = new ObservableCollection<MediaItem>(await _library.GetWatchLaterAsync());
    }

    [RelayCommand]
    private async Task Search()
    {
        var results = await _library.SearchAsync(SearchQuery);
        SearchResults = new ObservableCollection<MediaItem>(results);
    }

    [RelayCommand]
    private async Task ScanFolder()
    {
        var folder = await _dialogs.OpenFolderAsync();
        if (folder is not null)
        {
            await _library.ScanFolderAsync(folder);
            await Search();
        }
    }

    [RelayCommand]
    private async Task AddToFavorites(MediaItem item)
    {
        await _library.AddToFavoritesAsync(item.FilePath);
        Favorites = new ObservableCollection<MediaItem>(await _library.GetFavoritesAsync());
    }

    [RelayCommand]
    private async Task AddToWatchLater(MediaItem item)
    {
        await _library.AddToWatchLaterAsync(item.FilePath);
        WatchLater = new ObservableCollection<MediaItem>(await _library.GetWatchLaterAsync());
    }
}
