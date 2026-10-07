using Microsoft.EntityFrameworkCore;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Pets;

namespace PetSitting.Infrastructure.Persistence.Repositories;

public class PetRepository : IPetRepository
{
    private readonly AppDbContext _context;

    public PetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Pet?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Pets
            .Include(p => p.Pictures)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Pet>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Pets
            .Where(p => p.UserId == userId)
            .Include(p => p.Pictures)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        await _context.Pets.AddAsync(pet, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        _context.Pets.Update(pet);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var pet = await GetByIdAsync(id, cancellationToken);
        if (pet != null)
        {
            _context.Pets.Remove(pet);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
