namespace backend.Pets.Dtos
{
    public class PetDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? SpecialNeeds { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<PetPictureDto> Pictures { get; set; } = new();
    }

    public class PetPictureDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}
