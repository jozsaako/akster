using Microsoft.EntityFrameworkCore;
using PetSitting.Application.Availability;
using PetSitting.Domain.Availability;

namespace PetSitting.Infrastructure.Persistence.Repositories.Availability;

public class SitterProfileRepository : ISitterProfileRepository
{
    private readonly AppDbContext _context;

    public SitterProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SitterProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.SitterProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(SitterProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.SitterProfiles.AddAsync(profile, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SitterProfile profile, CancellationToken cancellationToken = default)
    {
        _context.SitterProfiles.Update(profile);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
