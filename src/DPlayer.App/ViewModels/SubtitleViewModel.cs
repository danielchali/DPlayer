using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using DPlayer.App.Services;

namespace DPlayer.App.ViewModels;

public partial class SubtitleViewModel : ObservableObject
{
    private readonly IMediaPlayerService _player;

    [ObservableProperty] private ObservableCollection<SubtitleTrack> _tracks = [];
    [ObservableProperty] private SubtitleTrack? _selectedTrack;
    [ObservableProperty] private double _subtitleDelay;
    [ObservableProperty] private double _fontSize = 24;
    [ObservableProperty] private string _fontFamily = "Segoe UI";
    [ObservableProperty] private string _foregroundColor = "#FFFFFF";
    [ObservableProperty] private string _outlineColor = "#000000";
    [ObservableProperty] private double _outlineWidth = 2;
    [ObservableProperty] private bool _shadowEnabled = true;

    public SubtitleViewModel(IMediaPlayerService player)
    {
        _player = player;
    }

    public void RefreshTracks()
    {
        Tracks = new ObservableCollection<SubtitleTrack>(_player.SubtitleTracks);
    }

    partial void OnSelectedTrackChanged(SubtitleTrack? value)
    {
        if (value is not null)
            _player.CurrentSubtitleTrack = value.Index;
    }

    partial void OnSubtitleDelayChanged(double value) => ApplyStyle();
    partial void OnFontSizeChanged(double value) => ApplyStyle();
    partial void OnFontFamilyChanged(string value) => ApplyStyle();
    partial void OnForegroundColorChanged(string value) => ApplyStyle();
    partial void OnOutlineColorChanged(string value) => ApplyStyle();
    partial void OnOutlineWidthChanged(double value) => ApplyStyle();
    partial void OnShadowEnabledChanged(bool value) => ApplyStyle();

    private void ApplyStyle()
    {
        _player.SubtitleStyle = new SubtitleStyle
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            ForegroundColor = ForegroundColor,
            OutlineColor = OutlineColor,
            OutlineWidth = OutlineWidth,
            ShadowEnabled = ShadowEnabled,
            DelayMs = SubtitleDelay
        };
    }

    [RelayCommand]
    private void IncreaseDelay() => SubtitleDelay += 500;

    [RelayCommand]
    private void DecreaseDelay() => SubtitleDelay -= 500;
}

public partial class SubtitleSearchViewModel : ObservableObject
{
    private readonly ISubtitleService _subtitleService;
    private readonly IMediaPlayerService _player;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string _searchTitle = "";
    [ObservableProperty] private string _seriesTitle = "";
    [ObservableProperty] private int? _season;
    [ObservableProperty] private int? _episode;
    [ObservableProperty] private string _languageCode = "eng";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private SubtitleSearchResult? _selectedResult;
    [ObservableProperty] private ObservableCollection<SubtitleSearchResult> _results = [];

    public SubtitleSearchViewModel(
        ISubtitleService subtitleService,
        IMediaPlayerService player,
        IDialogService dialogs)
    {
        _subtitleService = subtitleService;
        _player = player;
        _dialogs = dialogs;

        if (_player.CurrentMedia is not null)
            SearchTitle = Path.GetFileNameWithoutExtension(_player.CurrentMedia.FilePath);
    }

    [RelayCommand]
    private async Task Search()
    {
        IsSearching = true;
        try
        {
            var query = new SubtitleSearchQuery
            {
                Title = SearchTitle,
                SeriesTitle = SeriesTitle,
                Season = Season,
                Episode = Episode,
                LanguageCode = LanguageCode
            };

            if (_player.CurrentMedia is not null)
            {
                query.FileHash = await _subtitleService.ComputeFileHashAsync(_player.CurrentMedia.FilePath);
                query.FileSizeBytes = _player.CurrentMedia.FileSizeBytes;
            }

            var results = await _subtitleService.SearchAsync(query);
            Results = new ObservableCollection<SubtitleSearchResult>(results);
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task DownloadSelected()
    {
        if (SelectedResult is null) return;
        var saveDir = _player.CurrentMedia is not null
            ? Path.GetDirectoryName(_player.CurrentMedia.FilePath) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;

        var path = await _subtitleService.DownloadAsync(SelectedResult, saveDir);
        await _player.LoadSubtitleAsync(path);
        _dialogs.ShowMessage("Subtitles", $"Downloaded: {Path.GetFileName(path)}");
    }
}
