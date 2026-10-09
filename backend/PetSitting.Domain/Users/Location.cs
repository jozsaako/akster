using System.Text.RegularExpressions;

namespace PetSitting.Domain.Users;

/// <summary>
/// Where a user lives (owned by <see cref="User"/>). Street is private; only county and city are public.
/// Rows migrated from the old free-text address have only <see cref="Street"/> set until the user
/// saves a structured location; <see cref="Create"/> never produces that shape.
/// </summary>
public class Location
{
    // Romania's bounding box.
    private const double MinLat = 43.6, MaxLat = 48.3, MinLng = 20.2, MaxLng = 29.8;
    private static readonly Regex PostalCodePattern = new(@"^\d{6}$");

    public string? County { get; private set; }
    public string? City { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Street { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    private Location() { }

    /// <summary>True once the location has coordinates (a structured location was saved).</summary>
    public bool IsGeocoded => Latitude.HasValue && Longitude.HasValue;

    public static Result<Location> Create(
        string? county, string? city, string? postalCode, string? street, double latitude, double longitude)
    {
        county = county?.Trim();
        city = city?.Trim();
        postalCode = postalCode?.Trim();
        street = street?.Trim();
        postalCode = string.IsNullOrEmpty(postalCode) ? null : postalCode;

        if (county is null || !Counties.All.Contains(county))
            return Result<Location>.Fail(ErrorKind.Validation, "Select a valid county.");
        if (string.IsNullOrEmpty(city))
            return Result<Location>.Fail(ErrorKind.Validation, "City is required.");
        if (string.IsNullOrEmpty(street))
            return Result<Location>.Fail(ErrorKind.Validation, "Street and number are required.");
        if (postalCode is not null && !PostalCodePattern.IsMatch(postalCode))
            return Result<Location>.Fail(ErrorKind.Validation, "Postal code must be 6 digits.");
        if (latitude is < MinLat or > MaxLat || longitude is < MinLng or > MaxLng)
            return Result<Location>.Fail(ErrorKind.Validation, "Location must be inside Romania.");

        return Result<Location>.Ok(new Location
        {
            County = county, City = city, PostalCode = postalCode, Street = street,
            Latitude = latitude, Longitude = longitude
        });
    }
}
