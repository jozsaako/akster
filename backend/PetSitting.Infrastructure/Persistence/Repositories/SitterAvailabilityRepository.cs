using Microsoft.EntityFrameworkCore;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Availability;

namespace PetSitting.Infrastructure.Persistence.Repositories;

public class SitterAvailabilityRepository : ISitterAvailabilityRepository
{
    private readonly AppDbContext _context;

    public SitterAvailabilityRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SitterAvailability?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.SitterAvailabilities
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(SitterAvailability availability, CancellationToken cancellationToken = default)
    {
        await _context.SitterAvailabilities.AddAsync(availability, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SitterAvailability availability, CancellationToken cancellationToken = default)
    {
        _context.SitterAvailabilities.Update(availability);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
