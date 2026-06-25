using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DPlayer.Plugins;

public sealed class PluginManager
{
    private readonly List<IDPlayerPlugin> _plugins = [];
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginDirectory;

    public PluginManager(ILogger<PluginManager> logger, string pluginDirectory)
    {
        _logger = logger;
        _pluginDirectory = pluginDirectory;
    }

    public IReadOnlyList<IDPlayerPlugin> LoadedPlugins => _plugins;

    public void LoadPlugins(IPluginContext context)
    {
        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
            return;
        }

        foreach (var dll in Directory.GetFiles(_pluginDirectory, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IDPlayerPlugin).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IDPlayerPlugin plugin)
                    {
                        plugin.Initialize(context);
                        _plugins.Add(plugin);
                        _logger.LogInformation("Loaded plugin: {Name} v{Version}", plugin.Name, plugin.Version);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Dll}", dll);
            }
        }
    }

    public void ShutdownAll()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down plugin {Name}", plugin.Name);
            }
        }
        _plugins.Clear();
    }
}

public sealed class PluginContext : IPluginContext
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public PluginContext(IServiceProvider services, string dataDirectory, ILogger logger)
    {
        _services = services;
        DataDirectory = dataDirectory;
        _logger = logger;
    }

    public string DataDirectory { get; }
    public List<(string Path, Action Action)> MenuItems { get; } = [];

    public T GetService<T>() where T : notnull =>
        _services.GetService(typeof(T)) is T service
            ? service
            : throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");

    public void RegisterMenuItem(string path, Action action) => MenuItems.Add((path, action));

    public void Log(string message) => _logger.LogInformation("[Plugin] {Message}", message);
}
