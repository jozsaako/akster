namespace backend.Availability.Dtos
{
    public class UpdateAvailabilityRequest
    {
        public Dictionary<string, List<string>> Schedule { get; set; } = new();
        public List<string> Services { get; set; } = new();
        public List<string> AcceptedPetTypes { get; set; } = new();
        public int MaxPets { get; set; } = 1;
        public string? Bio { get; set; }
    }
}
