using System.Text.Json;
using backend.Availability.Dtos;
using backend.Availability.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Availability.Services
{
    public interface IAvailabilityService
    {
        Task<AvailabilityResponse> GetAvailabilityAsync(int userId);
        Task<AvailabilityResponse> UpsertAvailabilityAsync(int userId, UpdateAvailabilityRequest request);
    }

    public class AvailabilityService : IAvailabilityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AvailabilityService> _logger;

        private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        private static readonly string[] ValidSlots = ["Morning", "Afternoon", "Evening"];
        private static readonly string[] ValidServices = ["DogWalking", "DropInVisits", "HomeBoarding", "HouseSitting", "Daycare"];
        private static readonly string[] ValidPetTypes = ["Dog", "Cat"];

        public AvailabilityService(AppDbContext context, ILogger<AvailabilityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AvailabilityResponse> GetAvailabilityAsync(int userId)
        {
            var record = await _context.SitterAvailabilities
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (record == null)
                return new AvailabilityResponse { Success = true };

            return new AvailabilityResponse { Success = true, Availability = MapToDto(record) };
        }

        public async Task<AvailabilityResponse> UpsertAvailabilityAsync(int userId, UpdateAvailabilityRequest request)
        {
            try
            {
                if (request.MaxPets < 1 || request.MaxPets > 10)
                    return new AvailabilityResponse { Success = false, Message = "MaxPets must be between 1 and 10." };

                var sanitizedSchedule = new Dictionary<string, List<string>>();
                foreach (var day in ValidDays)
                {
                    if (request.Schedule.TryGetValue(day, out var slots))
                        sanitizedSchedule[day] = slots.Where(s => ValidSlots.Contains(s)).ToList();
                }

                var sanitizedServices = request.Services
                    .Where(s => ValidServices.Contains(s))
                    .Distinct()
                    .ToList();

                var sanitizedPetTypes = request.AcceptedPetTypes
                    .Where(t => ValidPetTypes.Contains(t))
                    .Distinct()
                    .ToList();

                var record = await _context.SitterAvailabilities
                    .FirstOrDefaultAsync(a => a.UserId == userId);

                var now = DateTime.UtcNow;

                if (record == null)
                {
                    record = new SitterAvailability
                    {
                        UserId = userId,
                        CreatedAt = now,
                    };
                    _context.SitterAvailabilities.Add(record);
                }

                record.ScheduleJson = JsonSerializer.Serialize(sanitizedSchedule);
                record.ServicesJson = JsonSerializer.Serialize(sanitizedServices);
                record.AcceptedPetTypesJson = JsonSerializer.Serialize(sanitizedPetTypes);
                record.MaxPets = request.MaxPets;
                record.Bio = request.Bio?.Trim();
                record.UpdatedAt = now;

                await _context.SaveChangesAsync();

                return new AvailabilityResponse { Success = true, Message = "Availability saved.", Availability = MapToDto(record) };
            }
            catch (Exception ex)
            {
                _logger.LogError("Error upserting availability: {Message}", ex.Message);
                return new AvailabilityResponse { Success = false, Message = "An error occurred while saving availability." };
            }
        }

        private static AvailabilityDto MapToDto(SitterAvailability a) => new()
        {
            Id = a.Id,
            UserId = a.UserId,
            Schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(a.ScheduleJson) ?? new(),
            Services = JsonSerializer.Deserialize<List<string>>(a.ServicesJson) ?? new(),
            AcceptedPetTypes = JsonSerializer.Deserialize<List<string>>(a.AcceptedPetTypesJson) ?? new(),
            MaxPets = a.MaxPets,
            Bio = a.Bio,
            UpdatedAt = a.UpdatedAt,
        };
    }
}
