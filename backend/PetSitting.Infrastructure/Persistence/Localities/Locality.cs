namespace PetSitting.Infrastructure.Persistence.Localities;

/// <summary>
/// Read-only reference row (GeoNames postal-code data, CC BY 4.0): one per Romanian postal code.
/// Not an aggregate, so it has no repository; <see cref="LocalityLookup"/> is its only reader.
/// </summary>
public class Locality
{
    public int Id { get; set; }
    public string County { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    /// <summary>City lowercased without diacritics, for typo-tolerant matching.</summary>
    public string CityKey { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public static string Key(string text) =>
        new string(text.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).Trim().ToLowerInvariant();
}
