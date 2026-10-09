using System.Text.Json;
using FluentValidation;
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

    private readonly ISitterAvailabilityRepository _availabilityRepository;
    private readonly IValidator<UpsertAvailabilityInput> _upsertValidator;

    public AvailabilityManager(ISitterAvailabilityRepository availabilityRepository, IValidator<UpsertAvailabilityInput> upsertValidator)
    {
        _availabilityRepository = availabilityRepository;
        _upsertValidator = upsertValidator;
    }

    public async Task<Result<AvailabilityDto?>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var availability = await _availabilityRepository.GetByUserIdAsync(userId, cancellationToken);
        return Result<AvailabilityDto?>.Ok(availability?.ToDto());
    }

    public async Task<Result<AvailabilityDto>> UpsertAsync(int userId, UpsertAvailabilityInput input, CancellationToken cancellationToken = default)
    {
        if (_upsertValidator.FirstError(input) is { } error)
            return Result<AvailabilityDto>.Fail(ErrorKind.Validation, error);

        var schedule = new Dictionary<string, List<string>>();
        foreach (var day in ValidDays)
        {
            if (input.Schedule.TryGetValue(day, out var slots))
                schedule[day] = slots.Where(ValidSlots.Contains).ToList();
        }

        var now = DateTime.UtcNow;
        var record = await _availabilityRepository.GetByUserIdAsync(userId, cancellationToken);
        var isNew = record == null;
        record ??= new SitterAvailability { UserId = userId, CreatedAt = now };

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
}
