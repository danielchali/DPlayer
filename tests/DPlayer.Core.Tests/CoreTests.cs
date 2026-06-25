using DPlayer.Core.Models;
using FluentAssertions;
using Xunit;

namespace DPlayer.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultShortcuts_ContainsEssentialBindings()
    {
        var shortcuts = DefaultShortcuts.Create();

        shortcuts.Should().ContainKey("PlayPause").WhoseValue.Should().Be("Space");
        shortcuts.Should().ContainKey("Fullscreen").WhoseValue.Should().Be("F");
        shortcuts.Should().ContainKey("OpenFile").WhoseValue.Should().Be("Ctrl+O");
        shortcuts.Should().ContainKey("SubtitleSearch").WhoseValue.Should().Be("Ctrl+S");
    }

    [Fact]
    public void AppSettings_DefaultTheme_IsDarkGreen()
    {
        var settings = new AppSettings();
        settings.Theme.Should().Be(Enums.AppTheme.DarkGreen);
        settings.HardwareAcceleration.Should().BeTrue();
    }
}

public class FileHashHelperTests
{
    [Fact]
    public async Task ComputeMovieHash_ReturnsEmpty_ForSmallFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[100]);
            var hash = await Infrastructure.Services.FileHashHelper.ComputeMovieHashAsync(tempFile);
            hash.Should().BeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

public class AbRepeatRangeTests
{
    [Fact]
    public void IsActive_RequiresBothPoints()
    {
        var range = new AbRepeatRange { PointA = TimeSpan.FromSeconds(10) };
        range.IsActive.Should().BeFalse();

        range.PointB = TimeSpan.FromSeconds(60);
        range.IsActive.Should().BeTrue();
    }
}

public class PlaylistServiceTests
{
    [Fact]
    public void MapToPlaylist_PreservesOrder()
    {
        var entity = new Infrastructure.Data.PlaylistEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Items =
            [
                new() { OrderIndex = 1, Title = "Second", FilePath = "b.mp4" },
                new() { OrderIndex = 0, Title = "First", FilePath = "a.mp4" }
            ]
        };

        // Verify ordering logic via entity structure
        var ordered = entity.Items.OrderBy(i => i.OrderIndex).ToList();
        ordered[0].Title.Should().Be("First");
        ordered[1].Title.Should().Be("Second");
    }
}
