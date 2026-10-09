using PetSitting.Application.Availability;
using PetSitting.Application.Pets;
using PetSitting.Application.Users;

namespace PetSitting.Api.Contracts;

// Wire format the frontend expects: { success, message, ...payload }.
public record AuthResponse(bool Success, string Message, UserDto? User = null, string? Token = null, string? RefreshToken = null);
public record PetResponse(bool Success, string Message, PetDto? Pet = null, List<PetDto>? Pets = null);
public record AvailabilityResponse(bool Success, string Message, AvailabilityDto? Availability = null);
public record CountiesResponse(bool Success, string Message, IReadOnlyList<string> Counties);
