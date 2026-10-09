using PetSitting.Domain.Availability;

namespace PetSitting.Application.Availability;

public interface ISitterProfileRepository
{
    Task<SitterProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(SitterProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(SitterProfile profile, CancellationToken cancellationToken = default);
}
