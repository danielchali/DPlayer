using System.Windows;
using DPlayer.App.Services;
using DPlayer.App.ViewModels;
using DPlayer.Infrastructure;
using DPlayer.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DPlayer.App;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DPlayer");
        Directory.CreateDirectory(appData);

        var dbPath = Path.Combine(appData, "dplayer.db");
        var logPath = Path.Combine(appData, "logs", "dplayer.log");
        var pluginsPath = Path.Combine(appData, "Plugins");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDPlayerInfrastructure(dbPath);
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<PlaylistViewModel>();
                services.AddSingleton<SubtitleViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<LibraryViewModel>();
                services.AddTransient<SubtitleSearchViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(DependencyInjection.CreateLoggerProvider(logPath));
            })
            .Build();

        Services = _host.Services;
        await DependencyInjection.InitializeDatabaseAsync(Services);

        var settings = Services.GetRequiredService<Core.Interfaces.ISettingsService>();
        await settings.LoadAsync();

        var themeService = Services.GetRequiredService<ThemeService>();
        themeService.ApplyTheme(settings.Settings.Theme);

        var pluginManager = Services.GetRequiredService<PluginManager>();
        var logger = Services.GetRequiredService<ILogger<PluginContext>>();
        var pluginContext = new PluginContext(Services, appData, logger);
        pluginManager.LoadPlugins(pluginContext);

        var playlist = Services.GetRequiredService<Core.Interfaces.IPlaylistService>();
        await playlist.LoadPlaylistsAsync();

        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            var vm = Services.GetRequiredService<MainViewModel>();
            await vm.OpenFileAsync(e.Args[0]);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var settings = Services.GetRequiredService<Core.Interfaces.ISettingsService>();
        await settings.SaveAsync();

        var player = Services.GetRequiredService<Core.Interfaces.IMediaPlayerService>();
        player.DisposePlayer();

        var pluginManager = Services.GetRequiredService<PluginManager>();
        pluginManager.ShutdownAll();

        if (_host is not null)
            await _host.StopAsync();

        base.OnExit(e);
    }
}
