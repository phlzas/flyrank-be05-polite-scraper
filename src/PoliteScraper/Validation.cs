using System.Text.Json;
using Json.Schema;

namespace PoliteScraper;

public sealed record ValidationResult(bool IsValid, string Reason);

public sealed class Validation
{
    private readonly JsonSchema _schema;

    public Validation(string schemaPath)
    {
        _schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
    }

    public ValidationResult Validate(Book book)
    {
        var element = JsonSerializer.SerializeToElement(book);
        var results = _schema.Evaluate(element);
        if (results.IsValid)
        {
            return new ValidationResult(true, string.Empty);
        }

        var locations = results.Details?
            .Where(detail => !detail.IsValid)
            .Select(detail => detail.InstanceLocation.ToString())
            .Where(location => location.Length > 0)
            .Distinct()
            .Take(3)
            .ToList() ?? new List<string>();

        var reason = locations.Count > 0
            ? $"schema validation failed at {string.Join(", ", locations)}"
            : "schema validation failed";
        return new ValidationResult(false, reason);
    }
}
