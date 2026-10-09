using FluentValidation;

namespace PetSitting.Application.Users.Validators;

public class UpdateProfileInputValidator : AbstractValidator<UpdateProfileInput>
{
    public UpdateProfileInputValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("All fields are required.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("All fields are required.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("All fields are required.")
            .EmailAddress().WithMessage("Invalid email format.");
    }
}
