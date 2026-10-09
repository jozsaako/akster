namespace PetSitting.Application.Users;

public record LocalityMatch(double Latitude, double Longitude);

/// <summary>Read-only Romanian locality reference data (GeoNames), used to place a user on the map.</summary>
public interface ILocalityLookup
{
    /// <summary>
    /// Centroid for a postal code, which must belong to the given county; without a postal code, or
    /// with one that is not in the dataset, falls back to the county + city. Null when nothing matches.
    /// </summary>
    Task<LocalityMatch?> FindAsync(string county, string city, string? postalCode, CancellationToken cancellationToken = default);
}
