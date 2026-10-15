using System.Globalization;
using System.Text.RegularExpressions;

namespace PoliteScraper;

public sealed class Normalizer
{
    public Book Normalize(RawBook raw)
    {
        return new Book(
            Title: raw.Title,
            ProductUrl: CanonicalUrl(raw.ProductUrl),
            PriceText: raw.PriceText,
            AvailabilityText: raw.AvailabilityText,
            RatingText: raw.RatingText,
            Description: raw.Description,
            SourcePage: raw.SourcePage,
            FetchedAt: raw.FetchedAt,
            PriceGbp: ParsePriceGbp(raw.PriceText));
    }

    private static string CanonicalUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 };
            return builder.Uri.AbsoluteUri;
        }
        return url;
    }

    private static decimal ParsePriceGbp(string priceText)
    {
        var match = Regex.Match(priceText, @"\d+(?:\.\d+)?");
        return match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }
}
