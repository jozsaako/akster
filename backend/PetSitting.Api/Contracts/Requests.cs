using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PetSitting.Domain.Pets;

namespace PetSitting.Api.Contracts;

// Input shape rules live here as DataAnnotations; [ApiController] rejects a bad body with 400
// (formatted in Program.cs) before the action runs. Managers receive already-valid input.
public record LoginRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    string Email,
    [Required(ErrorMessage = "Password is required.")]
    string Password);

public record RegisterRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    string Email,
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    string Password,
    [Required(ErrorMessage = "First name is required.")]
    [MinLength(2, ErrorMessage = "First name must be at least 2 characters long.")]
    string FirstName,
    [Required(ErrorMessage = "Last name is required.")]
    [MinLength(2, ErrorMessage = "Last name must be at least 2 characters long.")]
    string LastName);

public record RefreshRequest(string RefreshToken);
public record SetOwnerRequest([Required(ErrorMessage = "IsOwner is required.")] bool? IsOwner);

public record UpdateProfileRequest(
    [Required(ErrorMessage = "All fields are required.")] string FirstName,
    [Required(ErrorMessage = "All fields are required.")] string LastName,
    [Required(ErrorMessage = "All fields are required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,
    DateOnly? DateOfBirth);

public record UpdateLocationRequest(
    [Required(ErrorMessage = "County is required.")] string County,
    [Required(ErrorMessage = "City is required.")] string City,
    [Required(ErrorMessage = "Street and number are required.")] string Street,
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Postal code must be 6 digits.")]
    string? PostalCode);

// Gender and Type are nullable so a missing value fails [Required] instead of defaulting to the first enum member.
public record PetRequest(
    [Required(ErrorMessage = "Name is required.")] string Name,
    [Range(0, int.MaxValue, ErrorMessage = "Age must be a non-negative number.")] int Age,
    [Required(ErrorMessage = "Gender is required.")]
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    [EnumDataType(typeof(PetGender), ErrorMessage = "Invalid gender. Valid values: Male, Female.")]
    PetGender? Gender,
    [Required(ErrorMessage = "Type is required.")]
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    [EnumDataType(typeof(PetType), ErrorMessage = "Invalid type. Valid values: Cat, Dog.")]
    PetType? Type,
    string? SpecialNeeds);

public record AvailabilityRequest(
    Dictionary<string, List<string>> Schedule,
    List<string> Services,
    List<string> AcceptedPetTypes,
    [Range(1, 10, ErrorMessage = "MaxPets must be between 1 and 10.")] int MaxPets,
    string? Bio);
