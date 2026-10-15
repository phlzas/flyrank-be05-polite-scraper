using System.Text.Encodings.Web;
using System.Text.Json;

namespace PoliteScraper;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task<int> Main(string[] args)
    {
        var stage = -1;
        var includeBrokenUrl = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--stage" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed))
            {
                stage = parsed;
                i++;
            }
            else if (args[i] == "--include-broken-url")
            {
                includeBrokenUrl = true;
            }
        }

        if (stage < 0)
        {
            return await RunFullAsync(includeBrokenUrl);
        }

        return stage switch
        {
            0 => await RunStage0Async(),
            1 => await RunStage1Async(),
            2 => await RunStage2Async(),
            3 => await RunStage3Async(),
            _ => Unknown(stage)
        };
    }

    private static int Unknown(int stage)
    {
        Console.Error.WriteLine($"unknown stage {stage}");
        return 1;
    }

    private static RunReport NewReport() => new()
    {
        StartTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };

    private static void Finish(RunReport report) => Report.WriteRunReport(report);

    // Stage 0: classify the scraping target (robots.txt + summary).
    private static async Task<int> RunStage0Async()
    {
        var report = NewReport();
        var fetcher = new Fetcher();
        var robotsStatus = await fetcher.CheckRobotsAsync(report);

        Console.WriteLine(robotsStatus == 404 ? "no robots file found" : $"robots.txt status {robotsStatus}");
        Console.WriteLine();
        Console.WriteLine("Target classification");
        Console.WriteLine("  site:           Books to Scrape (https://books.toscrape.com/)");
        Console.WriteLine("  kind:           a sandbox built for practising scraping");
        Console.WriteLine("  how much:       the first 3 catalogue pages only (20 books each)");
        Console.WriteLine("  data collected: title, product_url, price_text, availability_text,");
        Console.WriteLine("                  rating_text, description, source_page, fetched_at, price_gbp");
        Console.WriteLine("  why ok here:    the site exists to be scraped, so polite crawling stays inside its purpose");
        Console.WriteLine($"  robots.txt:     {robotsStatus} — {(robotsStatus == 404 ? "no robots file found" : "robots file found")}");

        Finish(report);
        return 0;
    }

    // Stage 1: fetch and cache catalogue page 1 only.
    private static async Task<int> RunStage1Async()
    {
        var report = NewReport();
        var fetcher = new Fetcher();
        await fetcher.FetchCataloguePageAsync(1, report);
        Finish(report);
        return 0;
    }

    // Stage 2: discover three catalogue pages and every unique book url.
    private static async Task<int> RunStage2Async()
    {
        var report = NewReport();
        var fetcher = new Fetcher();
        var catalog = new Catalog(fetcher);
        var discovery = await catalog.DiscoverAsync(report);

        Console.WriteLine($"catalogue_pages={discovery.CataloguePages}, discovered={discovery.Discovered}, unique_urls={discovery.UniqueUrls.Count}");

        Finish(report);
        return 0;
    }

    // Stage 3: extract the raw record (8 keys) for every discovered book.
    private static async Task<int> RunStage3Async()
    {
        var report = NewReport();
        var fetcher = new Fetcher();
        var catalog = new Catalog(fetcher);
        var extractor = new Extractor();
        var discovery = await catalog.DiscoverAsync(report);

        var shown = false;
        foreach (var link in discovery.UniqueUrls)
        {
            var fetch = await fetcher.FetchDetailAsync(link.Url, report);
            if (!fetch.IsSuccess || fetch.Content is null)
            {
                report.AddFailedPage(link.Url, fetch.Error ?? $"status {fetch.StatusCode}");
                continue;
            }

            var raw = extractor.Extract(fetch.Content, link.Url, link.SourcePage, DateTime.UtcNow);
            if (!shown)
            {
                Console.WriteLine("sample raw record:");
                Console.WriteLine(JsonSerializer.Serialize(raw, JsonOptions));
                shown = true;
            }
        }

        Console.WriteLine($"detail_pages={discovery.UniqueUrls.Count}");

        Finish(report);
        return 0;
    }

    private static async Task<int> RunFullAsync(bool includeBrokenUrl)
    {
        await RunStage0Async();
        return 0;
    }
}
