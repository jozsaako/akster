using System.Text.Json;
using MediatR;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Availability;

namespace PetSitting.Application.Features.Availability.UpsertAvailability;

public class UpsertAvailabilityCommandHandler : IRequestHandler<UpsertAvailabilityCommand, AvailabilityResponse>
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    private static readonly string[] ValidSlots = ["Morning", "Afternoon", "Evening"];
    private static readonly string[] ValidServices = ["DogWalking", "DropInVisits", "HomeBoarding", "HouseSitting", "Daycare"];
    private static readonly string[] ValidPetTypes = ["Dog", "Cat"];

    private readonly ISitterAvailabilityRepository _repository;

    public UpsertAvailabilityCommandHandler(ISitterAvailabilityRepository repository)
    {
        _repository = repository;
    }

    public async Task<AvailabilityResponse> Handle(UpsertAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var schedule = new Dictionary<string, List<string>>();
        foreach (var day in ValidDays)
        {
            if (request.Schedule.TryGetValue(day, out var slots))
                schedule[day] = slots.Where(ValidSlots.Contains).ToList();
        }

        var now = DateTime.UtcNow;
        var record = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        var isNew = record == null;
        record ??= new SitterAvailability { UserId = request.UserId, CreatedAt = now };

        record.ScheduleJson = JsonSerializer.Serialize(schedule);
        record.ServicesJson = JsonSerializer.Serialize(request.Services.Where(ValidServices.Contains).Distinct().ToList());
        record.AcceptedPetTypesJson = JsonSerializer.Serialize(request.AcceptedPetTypes.Where(ValidPetTypes.Contains).Distinct().ToList());
        record.MaxPets = request.MaxPets;
        record.Bio = request.Bio?.Trim();
        record.UpdatedAt = now;

        if (isNew) await _repository.AddAsync(record, cancellationToken);
        else await _repository.UpdateAsync(record, cancellationToken);

        return new AvailabilityResponse(true, "Availability saved.", record.ToDto());
    }
}
