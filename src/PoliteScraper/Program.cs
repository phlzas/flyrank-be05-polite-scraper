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
            4 => await RunStage4Async(),
            5 => await RunStage5Async(includeBrokenUrl),
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

    // Stage 4: normalize, validate against the JSON Schema, and write output/books.json.
    private static async Task<int> RunStage4Async()
    {
        var report = NewReport();
        var (books, errors) = await ExtractValidateAsync(report, includeBrokenUrl: false, retry: false);
        WriteOutputs(books, errors);

        Console.WriteLine($"books.json={books.Count} records, errors.json={(errors.Count > 0 ? $"{errors.Count} entries" : "absent")}");

        Finish(report);
        return 0;
    }

    private static async Task<(List<Book> Books, List<ErrorEntry> Errors)> ExtractValidateAsync(
        RunReport report, bool includeBrokenUrl, bool retry)
    {
        var fetcher = new Fetcher();
        var catalog = new Catalog(fetcher);
        var extractor = new Extractor();
        var normalizer = new Normalizer();
        var validation = new Validation(Config.SchemaPath);

        var discovery = await catalog.DiscoverAsync(report);
        var links = discovery.UniqueUrls.ToList();

        if (includeBrokenUrl)
        {
            var brokenUrl = new Uri(new Uri(Config.BaseUrl), "catalogue/this-book-does-not-exist_99999/index.html").AbsoluteUri;
            links.Add(new BookLink(brokenUrl, links.Count > 0 ? links[0].SourcePage : Config.BaseUrl));
            Console.WriteLine($"injected 1 broken url for the Stage 5 proof: {brokenUrl}");
        }

        var books = new List<Book>();
        var errors = new List<ErrorEntry>();

        foreach (var link in links)
        {
            try
            {
                var fetch = await fetcher.FetchDetailAsync(link.Url, report, retry);
                if (!fetch.IsSuccess || fetch.Content is null)
                {
                    report.AddFailedPage(link.Url, fetch.Error ?? $"status {fetch.StatusCode}");
                    errors.Add(new ErrorEntry(link.Url, $"page fetch failed: {fetch.Error ?? $"status {fetch.StatusCode}"}"));
                    continue;
                }

                var raw = extractor.Extract(fetch.Content, link.Url, link.SourcePage, DateTime.UtcNow);
                var book = normalizer.Normalize(raw);
                var result = validation.Validate(book);
                if (result.IsValid)
                {
                    books.Add(book);
                }
                else
                {
                    errors.Add(new ErrorEntry(link.Url, result.Reason));
                }
            }
            catch (Exception ex)
            {
                report.AddFailedPage(link.Url, ex.Message);
                errors.Add(new ErrorEntry(link.Url, ex.Message));
            }
        }

        report.ValidRecords = books.Count;
        report.InvalidRecords = errors.Count;
        return (books, errors);
    }

    private static void WriteOutputs(List<Book> books, List<ErrorEntry> errors)
    {
        Directory.CreateDirectory(Config.OutputDir);
        File.WriteAllText(Config.BooksPath, JsonSerializer.Serialize(books, JsonOptions));

        if (errors.Count > 0)
        {
            Report.WriteErrors(errors);
        }
        else if (File.Exists(Config.ErrorsPath))
        {
            File.Delete(Config.ErrorsPath);
        }
    }

    // Stage 5: survive a broken page, retry transient failures, and report the run.
    private static async Task<int> RunStage5Async(bool includeBrokenUrl)
    {
        var report = NewReport();
        var (books, errors) = await ExtractValidateAsync(report, includeBrokenUrl, retry: true);
        WriteOutputs(books, errors);

        Console.WriteLine($"books.json={books.Count} records, errors.json={(errors.Count > 0 ? $"{errors.Count} entries" : "absent")}");
        Console.WriteLine($"failed_pages={report.FailedPages}");

        Finish(report);
        return 0;
    }

    private static async Task<int> RunFullAsync(bool includeBrokenUrl)
    {
        await RunStage0Async();
        return 0;
    }
}
