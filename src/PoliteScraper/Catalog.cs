using AngleSharp;

namespace PoliteScraper;

public sealed record BookLink(string Url, string SourcePage);

public sealed record Discovery(int CataloguePages, int Discovered, List<BookLink> UniqueUrls);

public sealed class Catalog
{
    private readonly Fetcher _fetcher;

    public Catalog(Fetcher fetcher) => _fetcher = fetcher;

    public async Task<Discovery> DiscoverAsync(RunReport? report = null)
    {
        var found = new List<BookLink>();
        var pages = 0;
        var current = new Uri(new Uri(Config.BaseUrl), "catalogue/page-1.html");

        while (pages < Config.MaxCataloguePages)
        {
            var fetch = await _fetcher.FetchAsync(current.AbsoluteUri, $"catalogue-page-{pages + 1}.html", report, retry: false);
            if (!fetch.IsSuccess || fetch.Content is null)
            {
                break;
            }
            pages++;

            var document = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request.Content(fetch.Content));
            var sourcePage = current.AbsoluteUri;

            foreach (var anchor in document.QuerySelectorAll("article.product_pod h3 a"))
            {
                var href = anchor.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }
                found.Add(new BookLink(new Uri(current, href).AbsoluteUri, sourcePage));
            }

            var next = document.QuerySelector("li.next a")?.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(next))
            {
                break;
            }
            current = new Uri(current, next);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<BookLink>();
        foreach (var link in found)
        {
            if (seen.Add(link.Url))
            {
                unique.Add(link);
            }
        }

        return new Discovery(pages, found.Count, unique);
    }
}
