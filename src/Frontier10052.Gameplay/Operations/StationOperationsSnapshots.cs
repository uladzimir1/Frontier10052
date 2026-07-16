namespace Frontier10052.Gameplay.Operations;

public sealed record ActionAvailability(bool IsAvailable, string Explanation);

public sealed record StationOperationsSnapshot(
    string GameId,
    int Seed,
    long CommandSequence,
    string ContentPackVersion,
    string LocationName,
    string LocationDetail,
    string GameTimeLabel,
    CommanderPresentation Commander,
    ShipPresentation Ship,
    IReadOnlyList<CrewPresentation> Crew,
    ContractPresentation Contract,
    IReadOnlyList<MarketItemPresentation> Market,
    IReadOnlyList<MarketReportPresentation> Reports,
    string? ReportDecision,
    IReadOnlyList<CargoPresentation> Cargo,
    FinancePresentation Finances,
    BriefingPresentation Briefing,
    EngineerPresentation Engineer,
    DeparturePresentation Departure,
    string JourneyPhase,
    string ContinuePath,
    bool IsDestinationOperations,
    int SchemaVersion = 3,
    bool IsTurnaround = false,
    int VoyageNumber = 1,
    long LienBalance = 72_000,
    string RepairCondition = "No service record",
    int LegalExposure = 0,
    IReadOnlyList<FactionPresentation>? Factions = null,
    string ImportantConsequences = "No persistent consequences recorded yet.");

public sealed record FactionPresentation(string Id, string Name, int Standing);

public sealed record CommanderPresentation(string DisplayName, string Initials, string Career);

public sealed record ShipPresentation(
    string Name,
    string Hull,
    int CargoLoaded,
    int CargoCapacity,
    int CargoAvailable,
    int FuelPercent,
    int DriveWearPercent);

public sealed record CrewPresentation(
    string Id,
    string Name,
    string Role,
    string Briefing,
    int Loyalty,
    int Fatigue,
    string Status,
    string Assignment);

public sealed record ContractPresentation(
    string Id,
    string Title,
    string Issuer,
    string Origin,
    string Destination,
    string Commodity,
    int Quantity,
    int DeadlineHours,
    string DeadlineLabel,
    long Reward,
    long FailurePenalty,
    string Consequence,
    bool Accepted,
    ActionAvailability AcceptAction,
    int RouteDurationHours = 0);

public sealed record MarketItemPresentation(
    string Id,
    string Name,
    string Description,
    string Legality,
    long UnitPrice,
    int Stock,
    long EstimatedProfitLow,
    long EstimatedProfitHigh,
    bool Purchasable);

public sealed record MarketReportPresentation(
    string Id,
    string Headline,
    string Detail,
    string Source,
    int AgeHours,
    int ConfidencePercent,
    string Legality,
    bool Verified,
    bool EngineerAnalyzed);

public sealed record CargoPresentation(
    string CommodityId,
    string Name,
    int Quantity,
    bool IsContractCargo,
    bool Sealed);

public sealed record FinancePresentation(long AvailableCredits, long ContractReward, long FailureExposure);

public sealed record BriefingPresentation(bool Acknowledged, ActionAvailability AcknowledgeAction);

public sealed record EngineerPresentation(
    string? Assignment,
    string Outcome,
    string ReliabilityRisk,
    ActionAvailability AnalyzeAction,
    ActionAvailability InspectAction);

public sealed record ReadinessRequirement(string Label, bool Complete, string Detail);

public sealed record AuthorizedManifestPresentation(
    string Origin,
    string Destination,
    string AuthorizedAt,
    long CommandSequence,
    string EngineerAssignment,
    string ReportDecision,
    long RemainingCredits,
    IReadOnlyList<CargoPresentation> Cargo);

public sealed record DeparturePresentation(
    bool Authorized,
    ActionAvailability AuthorizeAction,
    IReadOnlyList<ReadinessRequirement> Requirements,
    AuthorizedManifestPresentation? Manifest);
