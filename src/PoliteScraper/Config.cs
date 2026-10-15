namespace PoliteScraper;

public static class Config
{
    public const string UserAgent = "FlyRankInternship-A9/1.0 (+https://github.com/phlzas/flyrank-be05-polite-scraper)";
    public const int TimeoutSeconds = 10;
    public const int DelayMs = 500;
    public const int RetryDelayMs = 500;
    public const int MaxCataloguePages = 3;

    public static readonly string BaseUrl = "https://books.toscrape.com/";
    public static readonly string RobotsUrl = "https://books.toscrape.com/robots.txt";

    // Resolve the repository root so the tool works no matter which directory it is launched from.
    public static readonly string RepoRoot = FindRepoRoot();

    public static string CacheDir => Path.Combine(RepoRoot, "cache");
    public static string OutputDir => Path.Combine(RepoRoot, "output");
    public static string SchemaPath => Path.Combine(RepoRoot, "schema", "book.schema.json");
    public static string BooksPath => Path.Combine(OutputDir, "books.json");
    public static string RunReportPath => Path.Combine(OutputDir, "run-report.json");
    public static string ErrorsPath => Path.Combine(RepoRoot, "errors.json");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PoliteScraper.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
