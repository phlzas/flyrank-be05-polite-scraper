using System.Text.Json.Serialization;

namespace PoliteScraper;

public sealed record RawBook(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("product_url")] string ProductUrl,
    [property: JsonPropertyName("price_text")] string PriceText,
    [property: JsonPropertyName("availability_text")] string AvailabilityText,
    [property: JsonPropertyName("rating_text")] string RatingText,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("source_page")] string SourcePage,
    [property: JsonPropertyName("fetched_at")] string FetchedAt
);

public sealed record Book(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("product_url")] string ProductUrl,
    [property: JsonPropertyName("price_text")] string PriceText,
    [property: JsonPropertyName("availability_text")] string AvailabilityText,
    [property: JsonPropertyName("rating_text")] string RatingText,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("source_page")] string SourcePage,
    [property: JsonPropertyName("fetched_at")] string FetchedAt,
    [property: JsonPropertyName("price_gbp")] decimal PriceGbp
);
