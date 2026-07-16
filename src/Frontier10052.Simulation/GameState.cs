using System.Text.Json.Serialization;
using Frontier10052.Domain;

namespace Frontier10052.Simulation;

public enum ContractStatus { Offered, Accepted, Completed, Failed, Rejected, Transformed }
public enum ContractObjectiveKind { Cargo, Information }
public enum InformationDisposition { Secured, Sealed, Corroborated, Disclosed, Delivered }
public enum RouteCheckpointKind { Undock, GravityBoundary, DelayedMessage, LatticeDrift, Approach }
public enum CheckpointResolutionStatus { Pending, Resolved }
public enum StationOperationsMode { MarsTurnaround, SiriusPreparation }
public enum ReportDecision { ActuatorShortage, ConvoyArrival, CoolantAlternative, SkipSpeculativeTrade }
public enum EngineerAssignment { AnalyzeCourierManifest, InspectDrive }

public enum JourneyPhase
{
    DockedAtOrigin = 0,
    DepartureAuthorized = 1,
    InTransit = 2,
    EncounterPending = 3,
    Approach = 4,
    Docked = 5,
    Turnaround = 6,
    Delivered = 7,
    CheckpointPending = 8,
    CustomsPending = 9,
}

public enum EncounterStatus { Pending, Resolved }
public enum LienDisposition { Serviced, Deferred }
public enum RepairService { Certified, IlyaFieldService, Deferred }
public enum CrewRestService { FullLayover, TurnaroundWatches }

public enum EncounterResponse
{
    InspectionStandardCompliance,
    InspectionMedicalPriority,
    MechanicalFieldRepair,
    MechanicalReducedBurn,
    PiratePayDemand,
    PirateDumpSpeculativeCargo,
    PirateHardBurn,
    MigrationMedicalAssist,
    MedicalEmergencyAssist = MigrationMedicalAssist,
    MigrationConserveSupplies,
    MedicalEmergencyConserveSupplies = MigrationConserveSupplies,
    DebrisIlyaRepair,
    DebrisFieldRepair = DebrisIlyaRepair,
    DebrisMaraEvasion,
    DebrisEvasiveBurn = DebrisMaraEvasion,
}

public enum CheckpointResponse
{
    PreserveSeal,
    CorroborateWarning,
    LeakWarning,
    IlyaRecalibration,
    MaraPinchCorrection,
}

public static class FactionIds
{
    public const string TerranContinuityAuthority = "tca";
    public const string KuiperSyndicates = "kuiper-syndicates";
    public const string SiriusCorporateCompact = "sirius-corporate-compact";
    public const string SiriusLabor = "sirius-labor";
}

public sealed record CommanderState(string Callsign);

public sealed record ShipState(
    ShipId Id,
    StationId StationId,
    Tonnes CargoCapacity,
    int FuelPercent,
    int DriveWearPercent,
    int HullWearPercent = 8,
    int PinchReserve = 0);

public sealed record CrewMemoryState(GameTime RecordedAt, string Kind, string Summary, int LoyaltyDelta);

public sealed record CrewMemberState(
    CrewId Id,
    int Loyalty,
    int Fatigue,
    bool Available,
    string CurrentAssignment,
    IReadOnlyList<CrewMemoryState>? Memories = null);

public sealed record MarketListingState(CommodityId CommodityId, Credits AskPrice, Tonnes Stock, bool Purchasable);
public sealed record StationMarketState(StationId StationId, IReadOnlyList<MarketListingState> Listings);
public sealed record MarketReportState(string Id, GameTime ObservedAt, int ConfidencePercent, bool Verified, bool EngineerAnalyzed);

public sealed record ContractObjectiveState(
    ContractObjectiveKind Kind,
    CommodityId? CommodityId,
    Tonnes Quantity,
    InformationId? InformationId);

public sealed record ContractState(
    ContractId Id,
    StationId OriginStationId,
    StationId DestinationStationId,
    CommodityId CommodityId,
    Tonnes Quantity,
    GameTime Deadline,
    Credits Reward,
    Credits FailurePenalty,
    ContractStatus Status,
    string IssuerFactionId = "",
    bool IsTurnaroundOffer = false,
    GameTime? OfferedAt = null,
    GameTime? AcceptedAt = null,
    GameTime? SettledAt = null,
    string Outcome = "",
    ContractObjectiveState? Objective = null,
    GameTime? AcceptanceExpiresAt = null,
    string Transformation = "");

public sealed record InformationProvenanceState(string Source, GameTime ObservedAt, int ConfidencePercent, string Note);

public sealed record InformationItemState(
    InformationId Id,
    string Title,
    InformationDisposition Disposition,
    int ConfidencePercent,
    IReadOnlyList<InformationProvenanceState> Provenance);

public sealed record ContractTransformationState(
    ContractId ContractId,
    GameTime TransformedAt,
    string FromCase,
    string ToCase,
    string Reason);

public sealed record CargoLineState(CommodityId CommodityId, Tonnes Quantity, bool IsContractCargo, bool Sealed);

public sealed record DepartureManifestState(
    GameTime AuthorizedAt,
    long AuthorizedAtCommandSequence,
    StationId OriginStationId,
    StationId DestinationStationId,
    IReadOnlyList<CargoLineState> Cargo,
    Credits RemainingCredits,
    EngineerAssignment EngineerAssignment,
    ReportDecision? ReportDecision,
    ContractId? ContractId = null,
    RouteId? RouteId = null,
    int ManifestConfidencePercent = 100,
    int PinchReserve = 0,
    InformationId? InformationId = null);

public sealed record RouteCheckpointState(
    RouteCheckpointId Id,
    RouteCheckpointKind Kind,
    int ScheduledHour,
    CheckpointResolutionStatus Status,
    GameTime? ResolvedAt,
    CheckpointResponse? Response,
    string Outcome);

public sealed record RouteTravelState(
    RouteId Id,
    StationId OriginStationId,
    StationId DestinationStationId,
    int BaselineDurationHours,
    int FuelCostPercent,
    int BaseDriveWearPercent,
    int EncounterAtHour,
    GameTime DepartedAt,
    GameTime EstimatedArrival,
    int ElapsedBaselineHours,
    int DelayHours,
    string EncounterPoolId = "",
    int PinchCost = 0,
    IReadOnlyList<RouteCheckpointState>? Checkpoints = null)
{
    [JsonIgnore]
    public IReadOnlyList<RouteCheckpointState> AllCheckpoints => Checkpoints ?? [];

    [JsonIgnore]
    public bool UsesCheckpoints => Checkpoints is { Count: > 0 };
}

public sealed record EncounterState(
    EncounterId Id,
    EncounterStatus Status,
    GameTime TriggeredAt,
    EncounterResponse? Response,
    string Outcome);

// Retained as a compatibility presentation of the first destination market.
// Canonical schema 3 also persists all station markets on GameState.
public sealed record DestinationMarketListingState(CommodityId CommodityId, Credits BidPrice, long RealizedMargin);
public sealed record DestinationMarketState(StationId StationId, IReadOnlyList<DestinationMarketListingState> Listings);
public sealed record CargoSaleState(CommodityId CommodityId, Tonnes Quantity, Credits UnitPrice, Credits Proceeds, StationId? StationId = null);

public sealed record DestinationManifestState(
    StationId StationId,
    ContractId ContractId,
    GameTime PresentedAt,
    IReadOnlyList<CargoLineState> PresentedCargo,
    ContractStatus SettlementStatus,
    Credits CreditsAfterSettlement,
    int LegalExposure,
    string Outcome);

public sealed record JourneyHistoryState(
    int VoyageNumber,
    RouteId RouteId,
    StationId OriginStationId,
    StationId DestinationStationId,
    ContractId ContractId,
    GameTime DepartedAt,
    GameTime? ArrivedAt,
    GameTime? DeliveredAt,
    DepartureManifestState DepartureManifest,
    EncounterState? Encounter,
    DestinationManifestState? DestinationManifest,
    string Outcome,
    InformationSettlementState? InformationSettlement = null);

public sealed record LienPaymentState(GameTime RecordedAt, LienDisposition Disposition, Credits Amount, Credits PrincipalAfter, string Note);
public sealed record LienState(Credits Principal, LienDisposition? TurnaroundDisposition, IReadOnlyList<LienPaymentState> PaymentHistory);

public sealed record DebtLedgerEntryState(
    GameTime RecordedAt,
    string Kind,
    Credits Claim,
    Credits PaidFromCash,
    Credits Capitalized,
    Credits PrincipalAfter,
    string Note);

public sealed record RepairRecordState(
    GameTime CompletedAt,
    RepairService Service,
    Credits Cost,
    int Hours,
    int WearRemoved,
    string Provenance);

public sealed record MaintenanceState(
    int HullConditionPercent,
    int DriveConditionPercent,
    bool RepairDeferred,
    IReadOnlyList<RepairRecordState> RepairHistory);

public sealed record FactionStandingState(string FactionId, int Standing);
public sealed record StationVisitState(StationId StationId, GameTime ArrivedAt, GameTime? DepartedAt);

public sealed record TurnaroundState(
    StationId StationId,
    GameTime StartedAt,
    GameTime OffersExpireAt,
    LienDisposition? LienDisposition,
    RepairService? RepairService,
    CrewRestService? CrewRestService,
    ContractId? SelectedContractId,
    bool DepartureAuthorized,
    string LastOutcome,
    StationOperationsMode Mode = StationOperationsMode.MarsTurnaround);

public sealed record SiriusCustomsState(
    StationId OriginStationId,
    bool Cleared,
    GameTime? ClearedAt,
    int DelayHours,
    string Outcome);

public sealed record InformationSettlementState(
    ContractId ContractId,
    InformationId InformationId,
    InformationDisposition Disposition,
    bool OnTime,
    GameTime SettledAt,
    Credits Payment,
    Credits Claim,
    Credits CapitalizedClaim,
    string Outcome);

public sealed record JourneyState(
    JourneyPhase Phase,
    StationId? DockedStationId,
    RouteTravelState? Route,
    EncounterState? Encounter,
    [property: JsonPropertyName("marsMarket")] DestinationMarketState DestinationMarket,
    IReadOnlyList<CargoSaleState> Sales,
    string LastOutcome,
    int VoyageNumber = 1,
    ContractId? ActiveContractId = null,
    DestinationManifestState? DestinationManifest = null);

public sealed record GameState(
    int SchemaVersion,
    int Seed,
    long CommandSequence,
    GameId GameId,
    GameTime Time,
    CommanderState Commander,
    ShipState Ship,
    IReadOnlyList<CrewMemberState> Crew,
    StationMarketState Market,
    IReadOnlyList<MarketReportState> Reports,
    ContractState Contract,
    IReadOnlyList<CargoLineState> Cargo,
    Credits Money,
    bool BriefingAcknowledged,
    ReportDecision? ReportDecision,
    EngineerAssignment? EngineerAssignment,
    string EngineerOutcome,
    string DepartureReliabilityRisk,
    bool DepartureAuthorized,
    DepartureManifestState? DepartureManifest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JourneyState? Journey = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CommercialTrust = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ContractState>? Contracts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StationMarketState>? StationMarkets = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LienState? Lien = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MaintenanceState? Maintenance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FactionStandingState>? FactionStandings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int LegalExposure = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TurnaroundState? Turnaround = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<JourneyHistoryState>? JourneyHistory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StationVisitState>? StationVisits = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ManifestConfidencePercent = 100,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<InformationItemState>? InformationCargo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ContractTransformationState>? ContractTransformations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DebtLedgerEntryState>? DebtLedger = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SiriusCustomsState? SiriusCustoms = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InformationSettlementState? InformationSettlement = null)
{
    public const int CurrentSchemaVersion = 4;
    public Tonnes CargoLoaded => new(Cargo.Sum(item => item.Quantity.Value));
    public Tonnes CargoAvailable => Ship.CargoCapacity - CargoLoaded;

    [JsonIgnore]
    public IReadOnlyList<ContractState> AllContracts => Contracts ?? [Contract];

    [JsonIgnore]
    public IReadOnlyList<StationMarketState> AllStationMarkets => StationMarkets ?? [Market];

    [JsonIgnore]
    public IReadOnlyList<FactionStandingState> AllFactionStandings => FactionStandings ?? [];

    [JsonIgnore]
    public IReadOnlyList<JourneyHistoryState> AllJourneyHistory => JourneyHistory ?? [];

    [JsonIgnore]
    public IReadOnlyList<StationVisitState> AllStationVisits => StationVisits ?? [];

    [JsonIgnore]
    public IReadOnlyList<InformationItemState> AllInformationCargo => InformationCargo ?? [];

    [JsonIgnore]
    public IReadOnlyList<ContractTransformationState> AllContractTransformations => ContractTransformations ?? [];

    [JsonIgnore]
    public IReadOnlyList<DebtLedgerEntryState> AllDebtLedger => DebtLedger ?? [];
}
