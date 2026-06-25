namespace DPlayer.Plugins;

/// <summary>
/// Base interface for DPlayer plugins. Implement in external assemblies
/// and place DLLs in the Plugins folder.
/// </summary>
public interface IDPlayerPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    void Initialize(IPluginContext context);
    void Shutdown();
}

public interface IPluginContext
{
    string DataDirectory { get; }
    T GetService<T>() where T : notnull;
    void RegisterMenuItem(string path, Action action);
    void Log(string message);
}

public interface ISubtitleProviderPlugin : IDPlayerPlugin
{
    string ProviderId { get; }
    Task<IReadOnlyList<PluginSubtitleResult>> SearchAsync(PluginSubtitleQuery query, CancellationToken ct);
    Task<string> DownloadAsync(string subtitleId, string savePath, CancellationToken ct);
}

public sealed class PluginSubtitleQuery
{
    public string? Title { get; set; }
    public string? SeriesTitle { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string LanguageCode { get; set; } = "eng";
    public string? FileHash { get; set; }
}

public sealed class PluginSubtitleResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string? Preview { get; set; }
}
