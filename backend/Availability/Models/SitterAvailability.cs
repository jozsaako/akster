using backend.Identity.Models;

namespace backend.Availability.Models
{
    public class SitterAvailability
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string ScheduleJson { get; set; } = "{}";
        public string ServicesJson { get; set; } = "[]";
        public string AcceptedPetTypesJson { get; set; } = "[]";
        public int MaxPets { get; set; } = 1;
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
