using FluentValidation;

namespace PetSitting.Application.Features.Availability.UpsertAvailability;

public class UpsertAvailabilityCommandValidator : AbstractValidator<UpsertAvailabilityCommand>
{
    public UpsertAvailabilityCommandValidator()
    {
        RuleFor(x => x.MaxPets).InclusiveBetween(1, 10).WithMessage("MaxPets must be between 1 and 10.");
    }
}
