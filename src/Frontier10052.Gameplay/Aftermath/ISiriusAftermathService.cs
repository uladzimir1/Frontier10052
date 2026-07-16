using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Aftermath;

public interface ISiriusAftermathService
{
    ValueTask<CommandResult<SiriusAftermathSnapshot>> ResumeAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<SiriusAftermathSnapshot>> ResolveCrewConflictAsync(string playerKey, CrewConflictResponse response, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<SiriusAftermathSnapshot>> ResolveActuatorAllocationAsync(string playerKey, ActuatorAllocationResponse response, CancellationToken cancellationToken = default);
}

public sealed record SiriusAftermathSnapshot(
    string EventId,
    string Title,
    string Detail,
    SiriusAftermathPhase Phase,
    string ForecastDisposition,
    string GameTimeLabel,
    long CommandSequence,
    int SchemaVersion,
    string ContentPackVersion,
    int ConflictPressure,
    string ConflictOutcome,
    string? ConflictResponse,
    int ActuatorUnits,
    long UnitPrice,
    int CorporateUnits,
    int LaborUnits,
    int WayfarerUnits,
    string? Allocation,
    string Outcome,
    int DriveWearPercent,
    string RepairProvenance,
    long Credits,
    int CompactStanding,
    int LaborStanding,
    int CommercialTrust,
    IReadOnlyList<AftermathCrewPresentation> Crew,
    IReadOnlyList<CrewConflictAction> ConflictActions,
    IReadOnlyList<ActuatorAllocationAction> AllocationActions,
    IReadOnlyList<OutboundLeadPresentation> Leads);

public sealed record AftermathCrewPresentation(string Id, string Name, string Role, int Loyalty, int Fatigue, bool Available, string LastMemory);
public sealed record CrewConflictAction(string Id, string Label, string Consequence, bool IsAvailable, string Explanation, CrewConflictResponse Response);
public sealed record ActuatorAllocationAction(string Id, string Label, string Consequence, bool IsAvailable, string Explanation, ActuatorAllocationResponse Response);
public sealed record OutboundLeadPresentation(string Id, string Title, string Detail, bool Available, string LockedReason);
