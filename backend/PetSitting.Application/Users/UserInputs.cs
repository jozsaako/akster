namespace PetSitting.Application.Users;

public record LoginInput(string Email, string Password);

public record RegisterInput(string Email, string Password, string FirstName, string LastName);

public record UpdateProfileInput(string FirstName, string LastName, string Email, string? Address);
