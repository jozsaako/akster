using PetSitting.Domain.Pets;

namespace PetSitting.Application.Pets;

public record PetInput(string Name, int Age, PetGender Gender, PetType Type, string? SpecialNeeds);
