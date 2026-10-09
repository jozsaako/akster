using FluentValidation;

namespace PetSitting.Application.Common;

public static class ValidatorExtensions
{
    /// <summary>The first validation error message, or null when the input is valid.</summary>
    public static string? FirstError<T>(this IValidator<T> validator, T input)
    {
        var result = validator.Validate(input);
        return result.IsValid ? null : result.Errors[0].ErrorMessage;
    }
}
