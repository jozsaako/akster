using FluentValidation;

namespace PetSitting.Application.Features.Identity.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("All fields are required.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("All fields are required.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("All fields are required.")
            .EmailAddress().WithMessage("Invalid email format.");
    }
}
