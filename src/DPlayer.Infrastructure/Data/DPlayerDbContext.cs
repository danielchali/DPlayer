using DPlayer.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DPlayer.Infrastructure.Data;

public sealed class DPlayerDbContext : DbContext
{
    public DPlayerDbContext(DbContextOptions<DPlayerDbContext> options) : base(options) { }

    public DbSet<MediaItemEntity> MediaItems => Set<MediaItemEntity>();
    public DbSet<PlaylistEntity> Playlists => Set<PlaylistEntity>();
    public DbSet<PlaylistItemEntity> PlaylistItems => Set<PlaylistItemEntity>();
    public DbSet<FavoriteEntity> Favorites => Set<FavoriteEntity>();
    public DbSet<WatchLaterEntity> WatchLater => Set<WatchLaterEntity>();
    public DbSet<PlaybackPositionEntity> PlaybackPositions => Set<PlaybackPositionEntity>();
    public DbSet<SceneBookmarkEntity> SceneBookmarks => Set<SceneBookmarkEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();
    public DbSet<RecentFileEntity> RecentFiles => Set<RecentFileEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaItemEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FilePath).IsUnique();
            e.HasIndex(x => x.Title);
            e.HasIndex(x => x.LibraryType);
        });

        modelBuilder.Entity<PlaylistEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Items)
                .WithOne(x => x.Playlist)
                .HasForeignKey(x => x.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlaylistItemEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PlaylistId, x.OrderIndex });
        });

        modelBuilder.Entity<FavoriteEntity>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<WatchLaterEntity>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<PlaybackPositionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FilePath).IsUnique();
        });
        modelBuilder.Entity<SceneBookmarkEntity>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<SettingEntity>(e => e.HasKey(x => x.Key));
        modelBuilder.Entity<RecentFileEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FilePath).IsUnique();
        });
    }
}

public sealed class MediaItemEntity
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public long DurationTicks { get; set; }
    public int MediaType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime? LastPlayed { get; set; }
    public long LastPositionTicks { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? PosterPath { get; set; }
    public int LibraryType { get; set; }
    public string? SeriesTitle { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class PlaylistEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSmartPlaylist { get; set; }
    public string? SmartFilter { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public List<PlaylistItemEntity> Items { get; set; } = [];
}

public sealed class PlaylistItemEntity
{
    public Guid Id { get; set; }
    public Guid PlaylistId { get; set; }
    public int OrderIndex { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long DurationTicks { get; set; }
    public PlaylistEntity? Playlist { get; set; }
}

public sealed class FavoriteEntity
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}

public sealed class WatchLaterEntity
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}

public sealed class PlaybackPositionEntity
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long PositionTicks { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SceneBookmarkEntity
{
    public Guid Id { get; set; }
    public string MediaPath { get; set; } = string.Empty;
    public long PositionTicks { get; set; }
    public string? Label { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SettingEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class RecentFileEntity
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
}
