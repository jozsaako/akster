using FluentValidation;

namespace PetSitting.Application.Availability.Validators;

public class UpsertAvailabilityInputValidator : AbstractValidator<UpsertAvailabilityInput>
{
    public UpsertAvailabilityInputValidator()
    {
        RuleFor(x => x.MaxPets).InclusiveBetween(1, 10).WithMessage("MaxPets must be between 1 and 10.");
    }
}
