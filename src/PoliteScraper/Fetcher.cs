using System.Security.Cryptography;
using System.Text;

namespace PoliteScraper;

public sealed class FetchResult
{
    public bool IsCacheHit { get; init; }
    public string? Content { get; init; }
    public long Bytes { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => StatusCode == 200 && Content is not null;
}

public sealed class Fetcher
{
    private readonly HttpClient _http;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public Fetcher()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
        _http.Timeout = TimeSpan.FromSeconds(Config.TimeoutSeconds);
        Directory.CreateDirectory(Config.CacheDir);
    }

    // robots.txt is fetched live on every run and never cached: a missing file is not permission.
    public async Task<int> CheckRobotsAsync(RunReport? report = null)
    {
        var (status, _) = await SendAsync(Config.RobotsUrl, report);
        Console.WriteLine($"FETCH {Config.RobotsUrl} -> status {status}");
        return status;
    }

    public Task<FetchResult> FetchCataloguePageAsync(int page, RunReport? report = null)
    {
        var url = new Uri(new Uri(Config.BaseUrl), $"catalogue/page-{page}.html").AbsoluteUri;
        return FetchAsync(url, $"catalogue-page-{page}.html", report, retry: false);
    }

    public Task<FetchResult> FetchDetailAsync(string url, RunReport? report = null, bool retry = false)
        => FetchAsync(url, GetDetailCacheFileName(url), report, retry);

    public async Task<FetchResult> FetchAsync(string url, string cacheFileName, RunReport? report, bool retry)
    {
        var cachePath = Path.Combine(Config.CacheDir, cacheFileName);
        if (File.Exists(cachePath))
        {
            var size = new FileInfo(cachePath).Length;
            Console.WriteLine($"CACHE HIT {cachePath} -> {size} bytes");
            if (report is not null)
            {
                report.CacheHits++;
            }
            return new FetchResult
            {
                IsCacheHit = true,
                Content = await File.ReadAllTextAsync(cachePath),
                Bytes = size,
                StatusCode = 200
            };
        }

        var (status, body) = await SendWithRetryAsync(url, report, retry);
        if (status != 200 || body is null)
        {
            // A non-200 is a failed fetch: never cached, never parsed as HTML.
            Console.WriteLine($"FETCH {url} -> status {status} (failed, not cached)");
            return new FetchResult
            {
                StatusCode = status,
                Error = status == 0 ? "network error or timeout" : $"HTTP {status}"
            };
        }

        await File.WriteAllTextAsync(cachePath, body);
        var bytes = new FileInfo(cachePath).Length;
        Console.WriteLine($"FETCH {url} -> {bytes} bytes (cached as {cachePath})");
        return new FetchResult { Content = body, Bytes = bytes, StatusCode = 200 };
    }

    private async Task<(int Status, string? Body)> SendWithRetryAsync(string url, RunReport? report, bool retry)
    {
        var result = await SendAsync(url, report);
        var transient = result.Status == 0 || result.Status >= 500;
        if (retry && transient)
        {
            Console.WriteLine($"transient failure (status {result.Status}) -> retrying once after {Config.RetryDelayMs} ms");
            await Task.Delay(Config.RetryDelayMs);
            result = await SendAsync(url, report);
        }
        return result;
    }

    private async Task<(int Status, string? Body)> SendAsync(string url, RunReport? report)
    {
        await WaitTurnAsync();
        try
        {
            using var response = await _http.GetAsync(url);
            var status = (int)response.StatusCode;
            if (report is not null)
            {
                report.PagesFetched++;
            }
            if (status != 200)
            {
                return (status, null);
            }
            return (status, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (report is not null)
            {
                report.PagesFetched++;
            }
            return (0, null);
        }
    }

    // Politeness: at least DelayMs between REAL network requests. Cached reads never reach here.
    private async Task WaitTurnAsync()
    {
        var elapsed = (DateTime.UtcNow - _lastRequestUtc).TotalMilliseconds;
        if (elapsed < Config.DelayMs)
        {
            await Task.Delay(Config.DelayMs - (int)elapsed);
        }
        _lastRequestUtc = DateTime.UtcNow;
    }

    public static string GetDetailCacheFileName(string productUrl)
    {
        if (Uri.TryCreate(productUrl, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[^1].Equals("index.html", StringComparison.OrdinalIgnoreCase))
            {
                var slugAndId = segments[^2];
                if (slugAndId.Length > 0 && slugAndId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    return slugAndId + ".html";
                }
            }
        }

        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(productUrl));
        return Convert.ToHexString(hash).ToLowerInvariant() + ".html";
    }
}
