namespace PetSitting.Domain.Availability;

/// <summary>
/// SitterProfile aggregate root. A user is a sitter while their profile is active.
/// Holds the sitter's schedule, services, and preferences.
/// </summary>
public class SitterProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; private set; }
    public string ScheduleJson { get; set; } = "{}";
    public string ServicesJson { get; set; } = "[]";
    public string AcceptedPetTypesJson { get; set; } = "[]";
    public int MaxPets { get; set; } = 1;
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
