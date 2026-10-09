using PetSitting.Domain.Availability;

namespace PetSitting.Application.Availability;

public interface ISitterAvailabilityRepository
{
    Task<SitterAvailability?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(SitterAvailability availability, CancellationToken cancellationToken = default);
    Task UpdateAsync(SitterAvailability availability, CancellationToken cancellationToken = default);
}
