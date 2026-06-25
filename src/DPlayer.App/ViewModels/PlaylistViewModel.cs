using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using DPlayer.App.Services;

namespace DPlayer.App.ViewModels;

public partial class PlaylistViewModel : ObservableObject
{
    private readonly IPlaylistService _playlist;
    private readonly IDialogService _dialogs;
    private readonly IFileService _files;

    [ObservableProperty] private ObservableCollection<Playlist> _playlists = [];
    [ObservableProperty] private Playlist? _selectedPlaylist;
    [ObservableProperty] private PlaylistEntry? _selectedEntry;

    public PlaylistViewModel(IPlaylistService playlist, IDialogService dialogs, IFileService files)
    {
        _playlist = playlist;
        _dialogs = dialogs;
        _files = files;
        _playlist.PlaylistChanged += (_, _) => RefreshPlaylists();
        RefreshPlaylists();
    }

    private void RefreshPlaylists()
    {
        Playlists = new ObservableCollection<Playlist>(_playlist.Playlists);
    }

    [RelayCommand]
    private async Task CreatePlaylist()
    {
        var playlist = await _playlist.CreatePlaylistAsync($"Playlist {Playlists.Count + 1}");
        SelectedPlaylist = playlist;
    }

    [RelayCommand]
    private async Task DeletePlaylist()
    {
        if (SelectedPlaylist is null) return;
        if (await _dialogs.ConfirmAsync("Delete Playlist", $"Delete '{SelectedPlaylist.Name}'?"))
            await _playlist.DeletePlaylistAsync(SelectedPlaylist.Id);
    }

    [RelayCommand]
    private async Task AddFile()
    {
        if (SelectedPlaylist is null) return;
        var path = await _dialogs.OpenFileAsync(_files.GetMediaFilter());
        if (path is not null)
            await _playlist.AddToPlaylistAsync(SelectedPlaylist.Id, path, Path.GetFileNameWithoutExtension(path));
    }

    [RelayCommand]
    private async Task RemoveEntry()
    {
        if (SelectedPlaylist is null || SelectedEntry is null) return;
        await _playlist.RemoveFromPlaylistAsync(SelectedPlaylist.Id, SelectedEntry.Id);
    }

    [RelayCommand]
    private async Task ImportPlaylist()
    {
        var path = await _dialogs.OpenFileAsync("Playlist Files|*.m3u;*.m3u8;*.pls|All Files|*.*");
        if (path is not null)
            await _playlist.ImportPlaylistAsync(path);
    }

    [RelayCommand]
    private async Task ExportPlaylist()
    {
        if (SelectedPlaylist is null) return;
        var path = await _dialogs.SaveFileAsync("M3U Playlist|*.m3u", $"{SelectedPlaylist.Name}.m3u");
        if (path is not null)
            await _playlist.ExportPlaylistAsync(SelectedPlaylist.Id, path);
    }

    [RelayCommand]
    private void ActivatePlaylist()
    {
        if (SelectedPlaylist is not null)
            _playlist.SetActivePlaylist(SelectedPlaylist.Id);
    }
}
