using System.Text.RegularExpressions;
using AngleSharp;

namespace PoliteScraper;

public sealed class Extractor
{
    public RawBook Extract(string html, string productUrl, string sourcePage, DateTime fetchedAtUtc)
    {
        var document = BrowsingContext.New(Configuration.Default)
            .OpenAsync(request => request.Content(html))
            .GetAwaiter()
            .GetResult();

        var product = document.QuerySelector(".product_page")
            ?? throw new InvalidOperationException("product area (.product_page) not found; refusing to scrape the whole document");

        var title = product.QuerySelector("h1")?.TextContent.Trim() ?? string.Empty;
        var priceText = product.QuerySelector("p.price_color")?.TextContent.Trim() ?? string.Empty;
        var availabilityText = Collapse(product.QuerySelector("p.availability")?.TextContent);

        var ratingText = "Unknown";
        if (product.QuerySelector("p.star-rating") is { } stars)
        {
            ratingText = stars.ClassList.FirstOrDefault(c => !c.Equals("star-rating", StringComparison.Ordinal)) ?? "Unknown";
        }

        string? description = null;
        var descriptionHeader = product.QuerySelector("#product_description");
        var descriptionParagraph = descriptionHeader?.NextElementSibling;
        if (descriptionParagraph is not null && descriptionParagraph.TagName.Equals("P", StringComparison.OrdinalIgnoreCase))
        {
            var text = Collapse(descriptionParagraph.TextContent);
            if (text.Length > 0)
            {
                description = text;
            }
        }

        return new RawBook(
            Title: title,
            ProductUrl: productUrl,
            PriceText: priceText,
            AvailabilityText: availabilityText,
            RatingText: ratingText,
            Description: description,
            SourcePage: sourcePage,
            FetchedAt: fetchedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    private static string Collapse(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : Regex.Replace(value.Trim(), @"\s+", " ");
}
