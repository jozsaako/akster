using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace PetSitting.Infrastructure.Persistence.Localities;

/// <summary>Loads the embedded GeoNames extract into <c>Localities</c> once, when the table is empty.</summary>
public static class LocalitySeeder
{
    private const string Resource = "PetSitting.Infrastructure.Data.ro-localities.tsv";

    public static void Seed(AppDbContext db)
    {
        if (db.Localities.Any()) return;

        using var stream = typeof(LocalitySeeder).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"Embedded resource {Resource} not found.");
        using var reader = new StreamReader(stream);

        var batch = new List<Locality>(5000);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var f = line.Split('\t');
            if (f.Length < 5) continue;
            batch.Add(new Locality
            {
                County = f[0], City = f[1], CityKey = Locality.Key(f[1]), PostalCode = f[2],
                Latitude = double.Parse(f[3], CultureInfo.InvariantCulture),
                Longitude = double.Parse(f[4], CultureInfo.InvariantCulture)
            });
            if (batch.Count == 5000) Flush(db, batch);
        }
        Flush(db, batch);
    }

    private static void Flush(AppDbContext db, List<Locality> batch)
    {
        db.Localities.AddRange(batch);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        batch.Clear();
    }
}
