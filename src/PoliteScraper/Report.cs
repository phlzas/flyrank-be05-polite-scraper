using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoliteScraper;

public sealed class RunReport
{
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("pages_fetched")]
    public int PagesFetched { get; set; }

    [JsonPropertyName("cache_hits")]
    public int CacheHits { get; set; }

    [JsonPropertyName("valid_records")]
    public int ValidRecords { get; set; }

    [JsonPropertyName("invalid_records")]
    public int InvalidRecords { get; set; }

    [JsonPropertyName("failed_pages")]
    public int FailedPages { get; set; }

    [JsonIgnore]
    public Stopwatch Stopwatch { get; set; } = Stopwatch.StartNew();

    [JsonIgnore]
    public List<ErrorEntry> Failures { get; } = new();

    public void AddFailedPage(string url, string reason)
    {
        FailedPages++;
        Failures.Add(new ErrorEntry(url, reason));
    }
}

public sealed record ErrorEntry(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("reason")] string Reason);

public static class Report
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void WriteRunReport(RunReport report)
    {
        report.Stopwatch.Stop();
        report.Duration = Math.Round(report.Stopwatch.Elapsed.TotalSeconds, 2);
        Directory.CreateDirectory(Config.OutputDir);
        File.WriteAllText(Config.RunReportPath, JsonSerializer.Serialize(report, Options));
    }

    public static void WriteErrors(List<ErrorEntry> errors)
    {
        File.WriteAllText(Config.ErrorsPath, JsonSerializer.Serialize(errors, Options));
    }
}
