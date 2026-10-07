namespace backend.Pets.Dtos
{
    public class UpdatePetRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? SpecialNeeds { get; set; }
    }
}
