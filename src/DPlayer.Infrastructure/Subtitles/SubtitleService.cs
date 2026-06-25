using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DPlayer.Core.Enums;
using DPlayer.Core.Interfaces;
using DPlayer.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DPlayer.Infrastructure.Subtitles;

public sealed class SubtitleServiceOptions
{
    public string? OpenSubtitlesApiKey { get; set; }
    public string? OpenSubtitlesUsername { get; set; }
    public string? OpenSubtitlesPassword { get; set; }
}

public sealed class SubtitleService : ISubtitleService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SubtitleService> _logger;
    private readonly SubtitleServiceOptions _options;
    private readonly IEnumerable<ISubtitleProvider> _providers;

    public SubtitleService(
        IHttpClientFactory httpClientFactory,
        ILogger<SubtitleService> logger,
        IOptions<SubtitleServiceOptions> options,
        IEnumerable<ISubtitleProvider> providers)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _providers = providers;
    }

    public async Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(
        SubtitleSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var tasks = _providers.Select(p => p.SearchAsync(query, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r)
            .OrderByDescending(r => r.Rating)
            .ThenByDescending(r => r.DownloadCount)
            .ToList();
    }

    public async Task<string> DownloadAsync(
        SubtitleSearchResult result,
        string saveDirectory,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.Provider == result.Provider)
            ?? throw new InvalidOperationException($"Provider {result.Provider} not found");

        Directory.CreateDirectory(saveDirectory);
        var savePath = Path.Combine(saveDirectory, $"{SanitizeFileName(result.Title)}.{result.LanguageCode}.srt");
        return await provider.DownloadAsync(result, savePath, cancellationToken);
    }

    public async Task<string?> AutoDetectAndDownloadAsync(
        string mediaPath,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var hash = await ComputeFileHashAsync(mediaPath);
        var fileInfo = new FileInfo(mediaPath);
        var title = Path.GetFileNameWithoutExtension(mediaPath);

        var query = new SubtitleSearchQuery
        {
            Title = title,
            LanguageCode = languageCode,
            FileHash = hash,
            FileSizeBytes = fileInfo.Length
        };

        var results = await SearchAsync(query, cancellationToken);
        if (results.Count == 0) return null;

        var best = results.First();
        var saveDir = Path.GetDirectoryName(mediaPath) ?? Environment.CurrentDirectory;
        return await DownloadAsync(best, saveDir, cancellationToken);
    }

    public Task<string> ComputeFileHashAsync(string filePath) =>
        Services.FileHashHelper.ComputeMovieHashAsync(filePath);

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}

public interface ISubtitleProvider
{
    SubtitleProvider Provider { get; }
    Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(SubtitleSearchQuery query, CancellationToken ct);
    Task<string> DownloadAsync(SubtitleSearchResult result, string savePath, CancellationToken ct);
}

public sealed class OpenSubtitlesProvider : ISubtitleProvider
{
    private const string BaseUrl = "https://api.opensubtitles.com/api/v1";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SubtitleServiceOptions _options;
    private readonly ILogger<OpenSubtitlesProvider> _logger;
    private string? _token;

    public SubtitleProvider Provider => SubtitleProvider.OpenSubtitles;

    public OpenSubtitlesProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<SubtitleServiceOptions> options,
        ILogger<OpenSubtitlesProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(
        SubtitleSearchQuery query,
        CancellationToken ct)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);
            var client = CreateClient();

            var url = $"{BaseUrl}/subtitles?languages={query.LanguageCode}";
            if (!string.IsNullOrEmpty(query.FileHash))
                url += $"&moviehash={query.FileHash}";
            if (!string.IsNullOrEmpty(query.Title))
                url += $"&query={Uri.EscapeDataString(query.Title)}";
            if (query.Season.HasValue)
                url += $"&season_number={query.Season}";
            if (query.Episode.HasValue)
                url += $"&episode_number={query.Episode}";

            var response = await client.GetFromJsonAsync<OpenSubtitlesSearchResponse>(url, ct);
            if (response?.Data is null) return [];

            return response.Data.Select(item => new SubtitleSearchResult
            {
                Id = item.Id,
                Title = item.Attributes?.Release ?? item.Attributes?.FeatureDetails?.Title ?? query.Title ?? "",
                Language = item.Attributes?.Language ?? query.LanguageCode,
                LanguageCode = query.LanguageCode,
                Release = item.Attributes?.Release,
                Rating = item.Attributes?.Ratings ?? 0,
                DownloadCount = item.Attributes?.DownloadCount ?? 0,
                Provider = SubtitleProvider.OpenSubtitles,
                PreviewText = item.Attributes?.Comments,
                Season = query.Season?.ToString(),
                Episode = query.Episode?.ToString()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenSubtitles search failed");
            return [];
        }
    }

    public async Task<string> DownloadAsync(SubtitleSearchResult result, string savePath, CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);
        var client = CreateClient();

        var downloadRequest = new { file_id = int.Parse(result.Id) };
        var response = await client.PostAsJsonAsync($"{BaseUrl}/download", downloadRequest, ct);
        response.EnsureSuccessStatusCode();

        var downloadInfo = await response.Content.ReadFromJsonAsync<OpenSubtitlesDownloadResponse>(ct);
        if (string.IsNullOrEmpty(downloadInfo?.Link))
            throw new InvalidOperationException("No download link returned");

        var subtitleClient = _httpClientFactory.CreateClient();
        var content = await subtitleClient.GetByteArrayAsync(downloadInfo.Link, ct);
        await File.WriteAllBytesAsync(savePath, content, ct);
        return savePath;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_token)) return;
        if (string.IsNullOrEmpty(_options.OpenSubtitlesApiKey)) return;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Api-Key", _options.OpenSubtitlesApiKey);
        client.DefaultRequestHeaders.Add("User-Agent", "DPlayer v1.0");

        if (!string.IsNullOrEmpty(_options.OpenSubtitlesUsername))
        {
            var loginBody = new { username = _options.OpenSubtitlesUsername, password = _options.OpenSubtitlesPassword };
            var response = await client.PostAsJsonAsync($"{BaseUrl}/login", loginBody, ct);
            if (response.IsSuccessStatusCode)
            {
                var loginResult = await response.Content.ReadFromJsonAsync<OpenSubtitlesLoginResponse>(ct);
                _token = loginResult?.Token;
            }
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("OpenSubtitles");
        client.DefaultRequestHeaders.Add("Api-Key", _options.OpenSubtitlesApiKey ?? "");
        client.DefaultRequestHeaders.Add("User-Agent", "DPlayer v1.0");
        if (!string.IsNullOrEmpty(_token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return client;
    }

    private sealed class OpenSubtitlesSearchResponse
    {
        public List<OpenSubtitlesItem>? Data { get; set; }
    }

    private sealed class OpenSubtitlesItem
    {
        public string Id { get; set; } = "";
        public OpenSubtitlesAttributes? Attributes { get; set; }
    }

    private sealed class OpenSubtitlesAttributes
    {
        public string? Release { get; set; }
        public string? Language { get; set; }
        public double? Ratings { get; set; }
        public int? DownloadCount { get; set; }
        public string? Comments { get; set; }
        public OpenSubtitlesFeatureDetails? FeatureDetails { get; set; }
    }

    private sealed class OpenSubtitlesFeatureDetails
    {
        public string? Title { get; set; }
    }

    private sealed class OpenSubtitlesDownloadResponse
    {
        public string? Link { get; set; }
    }

    private sealed class OpenSubtitlesLoginResponse
    {
        public string? Token { get; set; }
    }
}

public sealed class SubDlProvider : ISubtitleProvider
{
    private const string BaseUrl = "https://api.subdl.com/api/v1";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SubDlProvider> _logger;

    public SubtitleProvider Provider => SubtitleProvider.SubDL;

    public SubDlProvider(IHttpClientFactory httpClientFactory, ILogger<SubDlProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(
        SubtitleSearchQuery query,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{BaseUrl}/subtitles?film_name={Uri.EscapeDataString(query.Title ?? "")}&languages={query.LanguageCode}";
            if (query.Season.HasValue) url += $"&season={query.Season}";
            if (query.Episode.HasValue) url += $"&episode={query.Episode}";

            var json = await client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var results = new List<SubtitleSearchResult>();

            if (doc.RootElement.TryGetProperty("subtitles", out var subs))
            {
                foreach (var sub in subs.EnumerateArray())
                {
                    results.Add(new SubtitleSearchResult
                    {
                        Id = sub.GetProperty("sid").GetString() ?? "",
                        Title = sub.TryGetProperty("release_name", out var rn) ? rn.GetString() ?? "" : query.Title ?? "",
                        Language = query.LanguageCode,
                        LanguageCode = query.LanguageCode,
                        Rating = sub.TryGetProperty("rating", out var r) ? r.GetDouble() : 0,
                        Provider = SubtitleProvider.SubDL
                    });
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SubDL search failed");
            return [];
        }
    }

    public async Task<string> DownloadAsync(SubtitleSearchResult result, string savePath, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"{BaseUrl}/subtitles/{result.Id}/download";
        var content = await client.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(savePath, content, ct);
        return savePath;
    }
}

public sealed class PodnapisiProvider : ISubtitleProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PodnapisiProvider> _logger;

    public SubtitleProvider Provider => SubtitleProvider.Podnapisi;

    public PodnapisiProvider(IHttpClientFactory httpClientFactory, ILogger<PodnapisiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(
        SubtitleSearchQuery query,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://www.podnapisi.net/subtitles/search/?keywords={Uri.EscapeDataString(query.Title ?? "")}&language={query.LanguageCode}";
            var html = await client.GetStringAsync(url, ct);

            // Podnapisi returns HTML; parse basic results for integration scaffold
            var results = new List<SubtitleSearchResult>();
            var idMatches = System.Text.RegularExpressions.Regex.Matches(html, @"subtitles/(\d+)");
            var titleMatches = System.Text.RegularExpressions.Regex.Matches(html, @"class=""title"">([^<]+)");

            for (var i = 0; i < Math.Min(idMatches.Count, 10); i++)
            {
                results.Add(new SubtitleSearchResult
                {
                    Id = idMatches[i].Groups[1].Value,
                    Title = i < titleMatches.Count ? titleMatches[i].Groups[1].Value.Trim() : query.Title ?? "",
                    Language = query.LanguageCode,
                    LanguageCode = query.LanguageCode,
                    Provider = SubtitleProvider.Podnapisi
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Podnapisi search failed");
            return [];
        }
    }

    public async Task<string> DownloadAsync(SubtitleSearchResult result, string savePath, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"https://www.podnapisi.net/subtitles/{result.Id}/download";
        var content = await client.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(savePath, content, ct);
        return savePath;
    }
}
