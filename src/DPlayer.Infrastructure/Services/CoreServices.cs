using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DPlayer.Core.Enums;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using DPlayer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DPlayer.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly DPlayerDbContext _db;
    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;
    private const string SettingsKey = "app_settings";

    public AppSettings Settings { get; private set; } = new();

    public SettingsService(DPlayerDbContext db, ILogger<SettingsService> logger)
    {
        _db = db;
        _logger = logger;
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DPlayer");
        Directory.CreateDirectory(appData);
        _settingsFilePath = Path.Combine(appData, "settings.json");
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                await SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            Settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json);

        var entity = await _db.Settings.FindAsync(SettingsKey);
        if (entity is null)
        {
            _db.Settings.Add(new SettingEntity { Key = SettingsKey, Value = json });
        }
        else
        {
            entity.Value = json;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<T?> GetValueAsync<T>(string key)
    {
        var entity = await _db.Settings.FindAsync(key);
        if (entity is null) return default;
        return JsonSerializer.Deserialize<T>(entity.Value);
    }

    public async Task SetValueAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        var entity = await _db.Settings.FindAsync(key);
        if (entity is null)
            _db.Settings.Add(new SettingEntity { Key = key, Value = json });
        else
            entity.Value = json;
        await _db.SaveChangesAsync();
    }
}

public sealed class PlaylistService : IPlaylistService
{
    private readonly DPlayerDbContext _db;
    private readonly ILogger<PlaylistService> _logger;
    private readonly List<Playlist> _playlists = [];
    private readonly Random _random = new();
    private List<int> _shuffleOrder = [];

    public IReadOnlyList<Playlist> Playlists => _playlists;
    public Playlist? ActivePlaylist { get; private set; }
    public int CurrentIndex { get; private set; }
    public bool Shuffle { get; set; }
    public LoopMode LoopMode { get; set; }

    public event EventHandler? PlaylistChanged;
    public event EventHandler<int>? CurrentIndexChanged;

    public PlaylistService(DPlayerDbContext db, ILogger<PlaylistService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LoadPlaylistsAsync()
    {
        var entities = await _db.Playlists
            .Include(p => p.Items)
            .OrderBy(p => p.Name)
            .ToListAsync();

        _playlists.Clear();
        foreach (var e in entities)
        {
            _playlists.Add(MapToPlaylist(e));
        }
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<Playlist> CreatePlaylistAsync(string name)
    {
        var playlist = new Playlist { Name = name };
        _db.Playlists.Add(MapToEntity(playlist));
        await _db.SaveChangesAsync();
        _playlists.Add(playlist);
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        return playlist;
    }

    public async Task DeletePlaylistAsync(Guid id)
    {
        var entity = await _db.Playlists.FindAsync(id);
        if (entity is null) return;
        _db.Playlists.Remove(entity);
        await _db.SaveChangesAsync();
        _playlists.RemoveAll(p => p.Id == id);
        if (ActivePlaylist?.Id == id) ActivePlaylist = null;
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddToPlaylistAsync(Guid playlistId, string filePath, string title)
    {
        var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is null) return;

        var entry = new PlaylistEntry
        {
            PlaylistId = playlistId,
            OrderIndex = playlist.Items.Count,
            FilePath = filePath,
            Title = title
        };
        _db.PlaylistItems.Add(new PlaylistItemEntity
        {
            Id = entry.Id,
            PlaylistId = playlistId,
            OrderIndex = entry.OrderIndex,
            FilePath = filePath,
            Title = title
        });
        playlist.Items.Add(entry);
        playlist.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveFromPlaylistAsync(Guid playlistId, Guid entryId)
    {
        var entity = await _db.PlaylistItems.FindAsync(entryId);
        if (entity is null) return;
        _db.PlaylistItems.Remove(entity);
        await _db.SaveChangesAsync();

        var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        playlist?.Items.RemoveAll(i => i.Id == entryId);
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearPlaylistAsync(Guid playlistId)
    {
        var entities = await _db.PlaylistItems
            .Where(i => i.PlaylistId == playlistId)
            .ToListAsync();

        _db.PlaylistItems.RemoveRange(entities);
        await _db.SaveChangesAsync();

        var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is not null)
        {
            playlist.Items.Clear();
            playlist.ModifiedAt = DateTime.UtcNow;
        }

        if (ActivePlaylist?.Id == playlistId)
            CurrentIndex = 0;

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActivePlaylist(Guid playlistId)
    {
        ActivePlaylist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        CurrentIndex = 0;
        Reshuffle();
        CurrentIndexChanged?.Invoke(this, CurrentIndex);
    }

    public async Task PlayNextAsync(IMediaPlayerService player)
    {
        if (ActivePlaylist is null || ActivePlaylist.Items.Count == 0) return;

        if (Shuffle)
        {
            var shuffleIdx = Array.IndexOf(_shuffleOrder.ToArray(), CurrentIndex);
            if (shuffleIdx < _shuffleOrder.Count - 1)
                CurrentIndex = _shuffleOrder[shuffleIdx + 1];
            else if (LoopMode == LoopMode.Playlist)
            {
                Reshuffle();
                CurrentIndex = _shuffleOrder[0];
            }
            else return;
        }
        else
        {
            if (CurrentIndex < ActivePlaylist.Items.Count - 1)
                CurrentIndex++;
            else if (LoopMode == LoopMode.Playlist)
                CurrentIndex = 0;
            else return;
        }

        var item = ActivePlaylist.Items[CurrentIndex];
        await player.LoadAsync(item.FilePath);
        player.Play();
        CurrentIndexChanged?.Invoke(this, CurrentIndex);
    }

    public async Task PlayPreviousAsync(IMediaPlayerService player)
    {
        if (ActivePlaylist is null || ActivePlaylist.Items.Count == 0) return;

        if (CurrentIndex > 0)
            CurrentIndex--;
        else if (LoopMode == LoopMode.Playlist)
            CurrentIndex = ActivePlaylist.Items.Count - 1;
        else return;

        var item = ActivePlaylist.Items[CurrentIndex];
        await player.LoadAsync(item.FilePath);
        player.Play();
        CurrentIndexChanged?.Invoke(this, CurrentIndex);
    }

    public async Task ImportPlaylistAsync(string path)
    {
        var lines = await File.ReadAllLinesAsync(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var playlist = await CreatePlaylistAsync(name);
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#')))
        {
            var filePath = line.Trim();
            await AddToPlaylistAsync(playlist.Id, filePath, Path.GetFileNameWithoutExtension(filePath));
        }
    }

    public async Task ExportPlaylistAsync(Guid playlistId, string path)
    {
        var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is null) return;
        var lines = playlist.Items.OrderBy(i => i.OrderIndex).Select(i => i.FilePath);
        await File.WriteAllLinesAsync(path, lines);
    }

    public async Task ReorderAsync(Guid playlistId, int fromIndex, int toIndex)
    {
        var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is null) return;

        var items = playlist.Items.OrderBy(i => i.OrderIndex).ToList();
        var item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].OrderIndex = i;
            var entity = await _db.PlaylistItems.FindAsync(items[i].Id);
            if (entity is not null) entity.OrderIndex = i;
        }
        playlist.Items = items;
        playlist.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Reshuffle()
    {
        if (ActivePlaylist is null) return;
        _shuffleOrder = Enumerable.Range(0, ActivePlaylist.Items.Count).OrderBy(_ => _random.Next()).ToList();
    }

    private static Playlist MapToPlaylist(PlaylistEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        IsSmartPlaylist = e.IsSmartPlaylist,
        SmartFilter = e.SmartFilter,
        CreatedAt = e.CreatedAt,
        ModifiedAt = e.ModifiedAt,
        Items = e.Items.OrderBy(i => i.OrderIndex).Select(i => new PlaylistEntry
        {
            Id = i.Id,
            PlaylistId = i.PlaylistId,
            OrderIndex = i.OrderIndex,
            FilePath = i.FilePath,
            Title = i.Title,
            Duration = TimeSpan.FromTicks(i.DurationTicks)
        }).ToList()
    };

    private static PlaylistEntity MapToEntity(Playlist p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        IsSmartPlaylist = p.IsSmartPlaylist,
        SmartFilter = p.SmartFilter,
        CreatedAt = p.CreatedAt,
        ModifiedAt = p.ModifiedAt
    };
}

public sealed class LibraryService : ILibraryService
{
    private static readonly string[] MediaExtensions =
    [
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mpeg", ".mpg",
        ".mp3", ".aac", ".flac", ".wav", ".ogg", ".m4a"
    ];

    private readonly DPlayerDbContext _db;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(DPlayerDbContext db, ILogger<LibraryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ScanFolderAsync(string folderPath, IProgress<int>? progress = null)
    {
        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => MediaExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        for (var i = 0; i < files.Count; i++)
        {
            await IndexFileAsync(files[i]);
            progress?.Report((i + 1) * 100 / files.Count);
        }
    }

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(string query, LibraryItemType? type = null)
    {
        var q = _db.MediaItems.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(m => m.Title.Contains(query) || m.FilePath.Contains(query));
        if (type.HasValue)
            q = q.Where(m => m.LibraryType == (int)type.Value);

        var entities = await q.OrderBy(m => m.Title).Take(200).ToListAsync();
        return entities.Select(MapToMediaItem).ToList();
    }

    public async Task<IReadOnlyList<MediaItem>> GetRecentAsync(int count = 20)
    {
        var recent = await _db.RecentFiles
            .OrderByDescending(r => r.LastOpened)
            .Take(count)
            .ToListAsync();

        return recent.Select(r => new MediaItem
        {
            FilePath = r.FilePath,
            Title = r.Title,
            LastPlayed = r.LastOpened
        }).ToList();
    }

    public async Task RecordRecentAsync(string filePath, string title)
    {
        var existing = await _db.RecentFiles.FirstOrDefaultAsync(r => r.FilePath == filePath);
        if (existing is null)
        {
            _db.RecentFiles.Add(new RecentFileEntity
            {
                Id = Guid.NewGuid(),
                FilePath = filePath,
                Title = title,
                LastOpened = DateTime.UtcNow
            });
        }
        else
        {
            existing.Title = title;
            existing.LastOpened = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task AddToFavoritesAsync(string filePath)
    {
        if (await _db.Favorites.AnyAsync(f => f.FilePath == filePath)) return;
        _db.Favorites.Add(new FavoriteEntity { Id = Guid.NewGuid(), FilePath = filePath, AddedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveFromFavoritesAsync(string filePath)
    {
        var fav = await _db.Favorites.FirstOrDefaultAsync(f => f.FilePath == filePath);
        if (fav is not null)
        {
            _db.Favorites.Remove(fav);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<MediaItem>> GetFavoritesAsync()
    {
        var favs = await _db.Favorites.OrderByDescending(f => f.AddedAt).ToListAsync();
        return favs.Select(f => new MediaItem { FilePath = f.FilePath, Title = Path.GetFileNameWithoutExtension(f.FilePath) }).ToList();
    }

    public async Task AddToWatchLaterAsync(string filePath)
    {
        if (await _db.WatchLater.AnyAsync(w => w.FilePath == filePath)) return;
        _db.WatchLater.Add(new WatchLaterEntity { Id = Guid.NewGuid(), FilePath = filePath, AddedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<MediaItem>> GetWatchLaterAsync()
    {
        var items = await _db.WatchLater.OrderByDescending(w => w.AddedAt).ToListAsync();
        return items.Select(w => new MediaItem { FilePath = w.FilePath, Title = Path.GetFileNameWithoutExtension(w.FilePath) }).ToList();
    }

    public async Task SavePlaybackPositionAsync(string filePath, TimeSpan position)
    {
        var entity = await _db.PlaybackPositions.FirstOrDefaultAsync(p => p.FilePath == filePath);
        if (entity is null)
        {
            _db.PlaybackPositions.Add(new PlaybackPositionEntity
            {
                Id = Guid.NewGuid(),
                FilePath = filePath,
                PositionTicks = position.Ticks,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            entity.PositionTicks = position.Ticks;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<TimeSpan?> GetPlaybackPositionAsync(string filePath)
    {
        var entity = await _db.PlaybackPositions.FirstOrDefaultAsync(p => p.FilePath == filePath);
        return entity is null ? null : TimeSpan.FromTicks(entity.PositionTicks);
    }

    public async Task<IReadOnlyList<SceneBookmark>> GetBookmarksAsync(string filePath)
    {
        var entities = await _db.SceneBookmarks
            .Where(b => b.MediaPath == filePath)
            .OrderBy(b => b.PositionTicks)
            .ToListAsync();
        return entities.Select(e => new SceneBookmark
        {
            Id = e.Id,
            MediaPath = e.MediaPath,
            Position = TimeSpan.FromTicks(e.PositionTicks),
            Label = e.Label,
            ThumbnailPath = e.ThumbnailPath,
            CreatedAt = e.CreatedAt
        }).ToList();
    }

    public async Task AddBookmarkAsync(SceneBookmark bookmark)
    {
        _db.SceneBookmarks.Add(new SceneBookmarkEntity
        {
            Id = bookmark.Id,
            MediaPath = bookmark.MediaPath,
            PositionTicks = bookmark.Position.Ticks,
            Label = bookmark.Label,
            ThumbnailPath = bookmark.ThumbnailPath,
            CreatedAt = bookmark.CreatedAt
        });
        await _db.SaveChangesAsync();
    }

    private async Task IndexFileAsync(string filePath)
    {
        if (await _db.MediaItems.AnyAsync(m => m.FilePath == filePath)) return;

        var info = new FileInfo(filePath);
        var item = new MediaItemEntity
        {
            Id = Guid.NewGuid(),
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath),
            FileSizeBytes = info.Length,
            DateAdded = DateTime.UtcNow,
            MediaType = IsAudio(filePath) ? (int)MediaType.Audio : (int)MediaType.Video,
            LibraryType = GuessLibraryType(filePath)
        };
        _db.MediaItems.Add(item);
        await _db.SaveChangesAsync();
    }

    private static bool IsAudio(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".aac" or ".flac" or ".wav" or ".ogg" or ".m4a";
    }

    private static int GuessLibraryType(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.Contains("s0") || name.Contains("season") || name.Contains("e0"))
            return (int)LibraryItemType.Episode;
        return (int)LibraryItemType.Movie;
    }

    private static MediaItem MapToMediaItem(MediaItemEntity e) => new()
    {
        Id = e.Id,
        FilePath = e.FilePath,
        Title = e.Title,
        Artist = e.Artist,
        Album = e.Album,
        Duration = TimeSpan.FromTicks(e.DurationTicks),
        Type = (MediaType)e.MediaType,
        FileSizeBytes = e.FileSizeBytes,
        DateAdded = e.DateAdded,
        LastPlayed = e.LastPlayed,
        LastPosition = TimeSpan.FromTicks(e.LastPositionTicks),
        ThumbnailPath = e.ThumbnailPath,
        PosterPath = e.PosterPath,
        LibraryType = (LibraryItemType)e.LibraryType,
        SeriesTitle = e.SeriesTitle,
        Season = e.Season,
        Episode = e.Episode
    };
}

public sealed class UpdateService : IUpdateService
{
    private const string VersionUrl = "https://api.github.com/repos/DPlayer/DPlayer/releases/latest";

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "DPlayer");
            var response = await client.GetStringAsync(VersionUrl, cancellationToken);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            return new UpdateInfo
            {
                Version = root.GetProperty("tag_name").GetString() ?? "",
                DownloadUrl = root.GetProperty("html_url").GetString() ?? "",
                ReleaseNotes = root.GetProperty("body").GetString() ?? ""
            };
        }
        catch
        {
            return null;
        }
    }
}

public static class FileHashHelper
{
    public static async Task<string> ComputeMovieHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var fileSize = stream.Length;
        if (fileSize < 65536) return string.Empty;

        var hash = MD5.Create();
        var buffer = new byte[65536];

        stream.Seek(0, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer.AsMemory(0, 65536));
        var first = hash.ComputeHash(buffer);

        stream.Seek(fileSize - 65536, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(buffer.AsMemory(0, 65536));
        var last = hash.ComputeHash(buffer);

        var sizeBytes = BitConverter.GetBytes(fileSize);
        var result = new byte[8];
        for (var i = 0; i < 8; i++)
            result[i] = (byte)(first[i] ^ last[i] ^ sizeBytes[i]);

        return Convert.ToHexString(result).ToLowerInvariant();
    }
}
