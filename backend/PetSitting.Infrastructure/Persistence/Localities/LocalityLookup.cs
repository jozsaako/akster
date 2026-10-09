using Microsoft.EntityFrameworkCore;
using PetSitting.Application.Users;

namespace PetSitting.Infrastructure.Persistence.Localities;

public class LocalityLookup : ILocalityLookup
{
    private readonly AppDbContext _context;

    public LocalityLookup(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LocalityMatch?> FindAsync(string county, string city, string? postalCode, CancellationToken cancellationToken = default)
    {
        if (postalCode != null)
        {
            var byCode = await _context.Localities.AsNoTracking()
                .FirstOrDefaultAsync(l => l.PostalCode == postalCode, cancellationToken);
            if (byCode != null)
                return byCode.County == county ? new LocalityMatch(byCode.Latitude, byCode.Longitude) : null;
        }

        var cityKey = Locality.Key(city);
        var byCity = await _context.Localities.AsNoTracking()
            .Where(l => l.County == county && l.CityKey == cityKey)
            .OrderBy(l => l.PostalCode)
            .FirstOrDefaultAsync(cancellationToken);
        return byCity == null ? null : new LocalityMatch(byCity.Latitude, byCity.Longitude);
    }
}
