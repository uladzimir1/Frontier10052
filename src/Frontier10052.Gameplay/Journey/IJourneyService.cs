using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Journey;

public interface IJourneyService
{
    ValueTask<CommandResult<JourneySnapshot>> ResumeAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> BeginVoyageAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> AdvanceVoyageAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> ResolveCurrentEncounterAsync(string playerKey, EncounterResponse response, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> ResolveCheckpointAsync(string playerKey, CheckpointResponse? response = null, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> ArriveAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> SellCargoAsync(string playerKey, CommodityId commodityId, Tonnes quantity, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> DeliverCargoAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> ClearSiriusCustomsAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<JourneySnapshot>> SettleInformationContractAsync(string playerKey, CancellationToken cancellationToken = default);
}

public sealed record JourneySnapshot(
    string GameId,
    int Seed,
    long CommandSequence,
    int SchemaVersion,
    string ContentPackVersion,
    JourneyPhase Phase,
    string ContinuePath,
    string GameTimeLabel,
    string LocationLabel,
    CommanderJourneyPresentation Commander,
    ShipJourneyPresentation Ship,
    RoutePresentation Route,
    EncounterPresentation? Encounter,
    IReadOnlyList<JourneyCargoPresentation> Cargo,
    IReadOnlyList<DestinationBidPresentation> DestinationMarket,
    IReadOnlyList<SalePresentation> Sales,
    ContractJourneyPresentation Contract,
    FinanceJourneyPresentation Finances,
    string LastOutcome,
    JourneyAction BeginAction,
    JourneyAction AdvanceAction,
    JourneyAction DockAction,
    JourneyAction DeliverAction,
    int VoyageNumber = 1,
    IReadOnlyList<JourneyCrewPresentation>? Crew = null,
    IReadOnlyList<JourneyFactionPresentation>? Factions = null,
    long LienPrincipal = 0,
    int LegalExposure = 0,
    DestinationManifestPresentation? DestinationManifest = null,
    IReadOnlyList<CheckpointPresentation>? Checkpoints = null,
    InformationJourneyPresentation? Information = null,
    SiriusCustomsPresentation? SiriusCustoms = null,
    JourneyAction? CustomsAction = null,
    JourneyAction? InformationSettlementAction = null,
    InformationSettlementPresentation? InformationSettlement = null,
    CinematicPresentation? Cinematic = null);

public sealed record CommanderJourneyPresentation(string DisplayName, string Initials);
public sealed record ShipJourneyPresentation(string Name, string Hull, int CargoLoaded, int CargoCapacity, int FuelPercent, int DriveWearPercent, int PinchReserve = 0);
public sealed record RoutePresentation(string Id, string Name, string Origin, string Destination, int DurationHours, int FuelCost, int BaseWear, int EncounterHour, int ElapsedHours, int DelayHours, string EstimatedArrival, string ActualArrival, int ProgressPercent, string Profile = "", int PinchCost = 0, bool UsesCheckpoints = false);
public sealed record JourneyAction(string Id, string Label, string Consequence, bool IsAvailable, string Explanation, EncounterResponse? Response = null, CheckpointResponse? CheckpointResponse = null);
public sealed record CheckpointPresentation(string Id, string Kind, string Title, string Detail, int ScheduledHour, string Status, string Outcome, IReadOnlyList<JourneyAction> Responses, bool IsNext, CinematicPresentation Cinematic);
public sealed record InformationJourneyPresentation(string Id, string Title, string Disposition, int ConfidencePercent, IReadOnlyList<string> Provenance);
public sealed record SiriusCustomsPresentation(bool Cleared, int DelayHours, string Origin, string Outcome);
public sealed record InformationSettlementPresentation(string Disposition, bool OnTime, long Payment, long Claim, long CapitalizedClaim, string Outcome);
public sealed record EncounterPresentation(string Id, string Title, string Detail, string Source, string Status, string Outcome, IReadOnlyList<JourneyAction> Responses, CinematicPresentation Cinematic);
public sealed record JourneyCargoPresentation(string CommodityId, string Name, int Quantity, bool IsContractCargo, bool Sealed);
public sealed record DestinationBidPresentation(string CommodityId, string Name, long UnitPrice, long Margin, int OwnedQuantity, JourneyAction SellAction, bool IsAtCurrentStation);
public sealed record SalePresentation(string Commodity, int Quantity, long UnitPrice, long Proceeds);
public sealed record ContractJourneyPresentation(string Title, string Status, int Quantity, string Commodity, long Reward, long FailurePenalty, int HoursRemaining, string DeadlineLabel);
public sealed record FinanceJourneyPresentation(long Credits, int CommercialTrust, long RealizedSales, long ContractUpside);
public sealed record JourneyCrewPresentation(string Id, string Name, string Role, int Loyalty, int Fatigue, string LastMemory);
public sealed record JourneyFactionPresentation(string Id, string Name, int Standing);
public sealed record DestinationManifestPresentation(string Station, string Contract, string Status, string PresentedAt, long CreditsAfterSettlement, int LegalExposure, string Outcome);

/// <summary>
/// A derived, presentation-only scene. It is never serialized into the authoritative save.
/// </summary>
public sealed record CinematicPresentation(
    string CueId,
    string Stage,
    string Environment,
    string RouteId,
    string? EventId,
    long CommandSequence,
    int DurationSeconds,
    int ScrollLengthVh,
    string Caption,
    string FallbackPlate,
    string ContinuationPath);
