using DPlayer.Core.Interfaces;
using DPlayer.Infrastructure.Data;
using DPlayer.Infrastructure.Playback;
using DPlayer.Infrastructure.Subtitles;
using DPlayer.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DPlayer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDPlayerInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        services.AddDbContext<DPlayerDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddHttpClient();
        services.AddHttpClient("OpenSubtitles");

        services.AddSingleton<SubtitleServiceOptions>();
        services.AddSingleton<ISubtitleProvider, OpenSubtitlesProvider>();
        services.AddSingleton<ISubtitleProvider, SubDlProvider>();
        services.AddSingleton<ISubtitleProvider, PodnapisiProvider>();

        services.AddSingleton<IMediaPlayerService, LibVlcMediaPlayerService>();
        services.AddSingleton<ISettingsService, Services.SettingsService>();
        services.AddSingleton<IPlaylistService, Services.PlaylistService>();
        services.AddSingleton<ILibraryService, Services.LibraryService>();
        services.AddSingleton<ISubtitleService, SubtitleService>();
        services.AddSingleton<IUpdateService, Services.UpdateService>();
        services.AddSingleton<PluginManager>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DPlayerDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public static ILoggerProvider CreateLoggerProvider(string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        return new Serilog.Extensions.Logging.SerilogLoggerProvider(Log.Logger, dispose: true);
    }
}
