using PetSitting.Domain;

namespace PetSitting.Application.Availability;

public interface IAvailabilityManager
{
    /// <summary>Success with null when the sitter has not saved anything yet (a normal state, not a failure).</summary>
    Task<Result<AvailabilityDto?>> GetAsync(int userId, CancellationToken cancellationToken = default);
    Task<Result<AvailabilityDto>> UpsertAsync(int userId, UpsertAvailabilityInput input, CancellationToken cancellationToken = default);
}
