namespace Frontier10052.Domain;

public sealed record CommandError(string Code, string Message);

public readonly record struct CommandResult<T>
{
    private CommandResult(bool isSuccess, T? value, CommandError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public CommandError? Error { get; }

    public static CommandResult<T> Success(T value) => new(true, value, null);
    public static CommandResult<T> Failure(string code, string message) => new(false, default, new CommandError(code, message));
}

public static class CommandErrorCodes
{
    public const string InvalidPlayerKey = "player.invalid-key";
    public const string InvalidCallsign = "commander.invalid-callsign";
    public const string GameNotFound = "game.not-found";
    public const string ActiveGameExists = "game.overwrite-confirmation-required";
    public const string SaveCorrupt = "save.corrupt";
    public const string SaveIncompatible = "save.incompatible";
    public const string PersistenceFailed = "save.write-failed";
    public const string InvalidQuantity = "cargo.invalid-quantity";
    public const string InsufficientCredits = "cargo.insufficient-credits";
    public const string InsufficientStock = "cargo.insufficient-stock";
    public const string InsufficientCapacity = "cargo.insufficient-capacity";
    public const string ContractAlreadyAccepted = "contract.already-accepted";
    public const string ContractUnavailable = "contract.unavailable";
    public const string BriefingAlreadyAcknowledged = "briefing.already-acknowledged";
    public const string ReportDecisionAlreadyRecorded = "reports.decision-already-recorded";
    public const string EngineerAlreadyAssigned = "crew.engineer-already-assigned";
    public const string DepartureRequirementsNotMet = "departure.requirements-not-met";
    public const string DepartureAlreadyAuthorized = "departure.already-authorized";
    public const string DepartureLocked = "departure.manifest-locked";
    public const string DepartureNotAuthorized = "journey.departure-not-authorized";
    public const string JourneyAlreadyStarted = "journey.already-started";
    public const string InvalidJourneyPhase = "journey.invalid-phase";
    public const string EncounterResolutionRequired = "journey.encounter-resolution-required";
    public const string EncounterOptionUnavailable = "encounter.option-unavailable";
    public const string DockingUnavailable = "journey.docking-unavailable";
    public const string CargoNotSellable = "cargo.not-sellable";
    public const string InsufficientCargo = "cargo.insufficient-owned-quantity";
    public const string ContractNotDeliverable = "contract.not-deliverable";
    public const string ContractAlreadySettled = "contract.already-settled";
    public const string ContentInvalid = "content.invalid";
    public const string TurnaroundUnavailable = "turnaround.unavailable";
    public const string LienChoiceRequired = "turnaround.lien-choice-required";
    public const string LienAlreadyResolved = "turnaround.lien-already-resolved";
    public const string RepairAlreadyResolved = "turnaround.repair-already-resolved";
    public const string RestAlreadyResolved = "turnaround.rest-already-resolved";
    public const string InvalidFuelQuantity = "turnaround.invalid-fuel-quantity";
    public const string FuelTankCapacityExceeded = "turnaround.fuel-capacity-exceeded";
    public const string InvalidPinchQuantity = "turnaround.invalid-pinch-quantity";
    public const string PinchCapacityExceeded = "turnaround.pinch-capacity-exceeded";
    public const string InsufficientPinchReserve = "journey.insufficient-pinch-reserve";
    public const string OfferExpired = "contract.offer-expired";
    public const string OfferAlreadyClosed = "contract.offer-already-closed";
    public const string OfferExpirationRisk = "contract.offer-expiration-risk";
    public const string ContractSelectionRequired = "contract.selection-required";
    public const string ServiceRequirementsNotMet = "turnaround.service-requirements-not-met";
    public const string RouteUnavailable = "route.unavailable";
    public const string CheckpointUnavailable = "checkpoint.unavailable";
    public const string CheckpointResponseUnavailable = "checkpoint.response-unavailable";
    public const string InformationMissing = "information.missing";
    public const string CustomsUnavailable = "sirius.customs-unavailable";
    public const string CustomsAlreadyCleared = "sirius.customs-already-cleared";
    public const string SettlementUnavailable = "sirius.settlement-unavailable";
    public const string SettlementAlreadyComplete = "sirius.settlement-already-complete";
}
