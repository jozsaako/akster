using System.Text.Json;
using PetSitting.Application.Common;
using PetSitting.Domain;
using PetSitting.Domain.Availability;

namespace PetSitting.Application.Availability;

public class AvailabilityManager : IAvailabilityManager
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    private static readonly string[] ValidSlots = ["Morning", "Afternoon", "Evening"];
    private static readonly string[] ValidServices = ["DogWalking", "DropInVisits", "HomeBoarding", "HouseSitting", "Daycare"];
    private static readonly string[] ValidPetTypes = ["Dog", "Cat"];

    private readonly ISitterProfileRepository _availabilityRepository;

    public AvailabilityManager(ISitterProfileRepository availabilityRepository)
    {
        _availabilityRepository = availabilityRepository;
    }

    public async Task<Result<AvailabilityDto?>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var availability = await _availabilityRepository.GetByUserIdAsync(userId, cancellationToken);
        return Result<AvailabilityDto?>.Ok(availability?.ToDto());
    }

    public async Task<Result<AvailabilityDto>> UpsertAsync(int userId, UpsertAvailabilityInput input, CancellationToken cancellationToken = default)
    {
        var schedule = new Dictionary<string, List<string>>();
        foreach (var day in ValidDays)
        {
            if (input.Schedule.TryGetValue(day, out var slots))
                schedule[day] = slots.Where(ValidSlots.Contains).ToList();
        }

        var now = DateTime.UtcNow;
        var record = await _availabilityRepository.GetByUserIdAsync(userId, cancellationToken);
        var isNew = record == null;
        record ??= new SitterProfile { UserId = userId, CreatedAt = now };

        record.ScheduleJson = JsonSerializer.Serialize(schedule);
        record.ServicesJson = JsonSerializer.Serialize(input.Services.Where(ValidServices.Contains).Distinct().ToList());
        record.AcceptedPetTypesJson = JsonSerializer.Serialize(input.AcceptedPetTypes.Where(ValidPetTypes.Contains).Distinct().ToList());
        record.MaxPets = input.MaxPets;
        record.Bio = input.Bio?.Trim();
        record.UpdatedAt = now;

        if (isNew) await _availabilityRepository.AddAsync(record, cancellationToken);
        else await _availabilityRepository.UpdateAsync(record, cancellationToken);

        return Result<AvailabilityDto>.Ok(record.ToDto());
    }

    public async Task<Result<AvailabilityDto>> ActivateAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var profile = await _availabilityRepository.GetByUserIdAsync(userId, cancellationToken);
        var isNew = profile == null;
        profile ??= new SitterProfile { UserId = userId, CreatedAt = now };

        profile.Activate();
        profile.UpdatedAt = now;

        if (isNew) await _availabilityRepository.AddAsync(profile, cancellationToken);
        else await _availabilityRepository.UpdateAsync(profile, cancellationToken);

        return Result<AvailabilityDto>.Ok(profile.ToDto());
    }

    public async Task<Result<AvailabilityDto>> DeactivateAsync(int userId, CancellationToken cancellationToken = default)
    {
        var profile = await _availabilityRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile == null)
            return Result<AvailabilityDto>.Fail(ErrorKind.NotFound, "Sitter profile not found.");

        profile.Deactivate();
        profile.UpdatedAt = DateTime.UtcNow;
        await _availabilityRepository.UpdateAsync(profile, cancellationToken);

        return Result<AvailabilityDto>.Ok(profile.ToDto());
    }
}
