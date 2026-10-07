namespace backend.Availability.Dtos
{
    public class AvailabilityResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AvailabilityDto? Availability { get; set; }
    }
}
