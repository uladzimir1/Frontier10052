using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Turnaround;

public interface ITurnaroundService
{
    ValueTask<CommandResult<TurnaroundSnapshot>> ResumeMarsOperationsAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> ServiceLienAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> DeferLienAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> RefuelAsync(string playerKey, int percentagePoints, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> RepairAsync(string playerKey, RepairService service, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> RestCrewAsync(string playerKey, CrewRestService service, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> SelectContractAsync(string playerKey, ContractId contractId, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<TurnaroundSnapshot>> AuthorizeDepartureAsync(string playerKey, CancellationToken cancellationToken = default);
}

public sealed record TurnaroundSnapshot(
    string GameId,
    int Seed,
    long CommandSequence,
    int SchemaVersion,
    string ContentPackVersion,
    string LocationName,
    string LocationDetail,
    string GameTimeLabel,
    string Callsign,
    string Initials,
    TurnaroundShipPresentation Ship,
    IReadOnlyList<TurnaroundCrewPresentation> Crew,
    TurnaroundLienPresentation Lien,
    TurnaroundMaintenancePresentation Maintenance,
    IReadOnlyList<TurnaroundFactionPresentation> Factions,
    int LegalExposure,
    long AvailableCredits,
    int OfferHoursRemaining,
    IReadOnlyList<TurnaroundOfferPresentation> Offers,
    TurnaroundAction ServiceLienAction,
    TurnaroundAction DeferLienAction,
    TurnaroundAction CertifiedRepairAction,
    TurnaroundAction FieldRepairAction,
    TurnaroundAction DeferRepairAction,
    TurnaroundAction FullLayoverAction,
    TurnaroundAction WatchesAction,
    IReadOnlyList<TurnaroundRequirement> DepartureRequirements,
    TurnaroundAction DepartureAction,
    bool DepartureAuthorized,
    string? AuthorizedDestination,
    string LastOutcome);

public sealed record TurnaroundAction(string Id, string Label, string Consequence, bool IsAvailable, string Explanation);
public sealed record TurnaroundShipPresentation(string Name, string Hull, int CargoLoaded, int CargoCapacity, int CargoAvailable, int FuelPercent, int DriveWearPercent, int HullConditionPercent);
public sealed record TurnaroundCrewMemoryPresentation(string Kind, string Summary, int LoyaltyDelta, string RecordedAt);
public sealed record TurnaroundCrewPresentation(string Id, string Name, string Role, int Loyalty, int Fatigue, string Assignment, IReadOnlyList<TurnaroundCrewMemoryPresentation> Memories);
public sealed record TurnaroundLienHistoryPresentation(string Disposition, long Amount, long PrincipalAfter, string Note, string RecordedAt);
public sealed record TurnaroundLienPresentation(long Principal, string? Disposition, IReadOnlyList<TurnaroundLienHistoryPresentation> History);
public sealed record TurnaroundRepairHistoryPresentation(string Service, long Cost, int Hours, int WearRemoved, string Provenance, string CompletedAt);
public sealed record TurnaroundMaintenancePresentation(int HullConditionPercent, int DriveConditionPercent, string RepairDecision, bool RepairDeferred, IReadOnlyList<TurnaroundRepairHistoryPresentation> History);
public sealed record TurnaroundFactionPresentation(string Id, string Name, int Standing);
public sealed record TurnaroundOfferPresentation(
    string Id,
    string Title,
    string Issuer,
    string Destination,
    string Commodity,
    int Quantity,
    int DeadlineHours,
    string DeadlineLabel,
    long Reward,
    long FailurePenalty,
    string Legality,
    string Consequence,
    string Status,
    int RouteHours,
    int FuelCost,
    int DriveWear,
    string Encounter,
    string CrewReaction,
    TurnaroundAction SelectAction);
public sealed record TurnaroundRequirement(string Label, bool Complete, string Detail);
