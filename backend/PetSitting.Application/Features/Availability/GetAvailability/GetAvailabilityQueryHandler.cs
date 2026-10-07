using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Availability.GetAvailability;

public class GetAvailabilityQueryHandler : IRequestHandler<GetAvailabilityQuery, AvailabilityResponse>
{
    private readonly ISitterAvailabilityRepository _repository;

    public GetAvailabilityQueryHandler(ISitterAvailabilityRepository repository)
    {
        _repository = repository;
    }

    public async Task<AvailabilityResponse> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        // A sitter who hasn't saved anything yet is a normal state, not a failure.
        var availability = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        return new AvailabilityResponse(true, string.Empty, availability?.ToDto());
    }
}
