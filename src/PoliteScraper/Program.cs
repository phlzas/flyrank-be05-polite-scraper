namespace PoliteScraper;

public static class Program
{
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

    private static async Task<int> RunFullAsync(bool includeBrokenUrl)
    {
        await RunStage0Async();
        return 0;
    }
}
