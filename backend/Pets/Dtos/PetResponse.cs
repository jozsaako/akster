namespace backend.Pets.Dtos
{
    public class PetResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PetDto? Pet { get; set; }
        public List<PetDto>? Pets { get; set; }
    }
}
