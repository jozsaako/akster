using FluentValidation;
using PetSitting.Domain.Pets;

namespace PetSitting.Application.Features.Pets.CreatePet;

public class CreatePetCommandValidator : AbstractValidator<CreatePetCommand>
{
    public CreatePetCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");

        RuleFor(x => x.Age).GreaterThanOrEqualTo(0).WithMessage("Age must be a non-negative number.");

        RuleFor(x => x.Gender)
            .Must(g => Enum.TryParse<PetGender>(g, true, out _)).WithMessage("Invalid gender. Valid values: Male, Female.");

        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<PetType>(t, true, out _)).WithMessage("Invalid type. Valid values: Cat, Dog.");
    }
}
