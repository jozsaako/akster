using MediatR;

namespace PetSitting.Application.Features.Availability.GetAvailability;

public record GetAvailabilityQuery(int UserId) : IRequest<AvailabilityResponse>;
