using Frontier10052.Domain;

namespace Frontier10052.Simulation;

public static class GameCommands
{
    public static CommandResult<GameState> AcknowledgeBriefing(GameState state)
    {
        if (state.DepartureAuthorized)
        {
            return Locked();
        }

        if (state.BriefingAcknowledged)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.BriefingAlreadyAcknowledged, "The crew briefing is already acknowledged.");
        }

        return Success(state with { BriefingAcknowledged = true });
    }

    public static CommandResult<GameState> AcceptContract(GameState state)
    {
        if (state.DepartureAuthorized)
        {
            return Locked();
        }

        if (state.Contract.Status == ContractStatus.Accepted)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.ContractAlreadyAccepted, "The medical-membrane contract is already accepted.");
        }

        if (state.Contract.OriginStationId != state.Ship.StationId)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.ContractUnavailable, "This contract can only be accepted at its origin station.");
        }

        if (state.CargoAvailable < state.Contract.Quantity)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.InsufficientCapacity, "The required sealed shipment will not fit in the hold.");
        }

        CargoLineState requiredCargo = new(state.Contract.CommodityId, state.Contract.Quantity, true, true);
        ContractState acceptedContract = state.Contract with { Status = ContractStatus.Accepted, AcceptedAt = state.Time };
        return Success(state with
        {
            Contract = acceptedContract,
            Contracts = state.AllContracts.Select(item => item.Id == acceptedContract.Id ? acceptedContract : item).ToArray(),
            Cargo = [.. state.Cargo, requiredCargo],
        });
    }

    public static CommandResult<GameState> PurchaseCargo(GameState state, CommodityId commodityId, Tonnes quantity)
    {
        if (state.DepartureAuthorized)
        {
            return Locked();
        }

        if (quantity.Value <= 0)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.InvalidQuantity, "Choose at least one tonne to purchase.");
        }

        MarketListingState? listing = state.Market.Listings.SingleOrDefault(item => item.CommodityId == commodityId);
        if (listing is null || !listing.Purchasable)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.InvalidQuantity, "That cargo is not available for open-market purchase.");
        }

        if (listing.Stock < quantity)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.InsufficientStock, $"Only {listing.Stock.Value} tonnes remain in station stock.");
        }

        if (state.CargoAvailable < quantity)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.InsufficientCapacity, $"Only {state.CargoAvailable.Value} tonnes remain in the hold.");
        }

        Credits totalPrice = listing.AskPrice * quantity;
        if (state.Money < totalPrice)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.InsufficientCredits, $"This purchase costs {totalPrice.Value:N0} credits; only {state.Money.Value:N0} are available.");
        }

        List<CargoLineState> cargo = [.. state.Cargo];
        int cargoIndex = cargo.FindIndex(item => item.CommodityId == commodityId && !item.IsContractCargo);
        if (cargoIndex >= 0)
        {
            CargoLineState existing = cargo[cargoIndex];
            cargo[cargoIndex] = existing with { Quantity = existing.Quantity + quantity };
        }
        else
        {
            cargo.Add(new CargoLineState(commodityId, quantity, false, false));
        }

        IReadOnlyList<MarketListingState> listings = state.Market.Listings
            .Select(item => item.CommodityId == commodityId ? item with { Stock = item.Stock - quantity } : item)
            .ToArray();

        return Success(state with
        {
            Money = state.Money - totalPrice,
            Cargo = cargo,
            Market = state.Market with { Listings = listings },
        });
    }

    public static CommandResult<GameState> RecordReportDecision(GameState state, ReportDecision decision)
    {
        if (state.DepartureAuthorized)
        {
            return Locked();
        }

        if (state.ReportDecision is not null)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.ReportDecisionAlreadyRecorded, "The market assessment is already recorded in the command journal.");
        }

        return Success(state with { ReportDecision = decision });
    }

    public static CommandResult<GameState> AssignEngineer(GameState state, EngineerAssignment assignment)
    {
        if (state.DepartureAuthorized)
        {
            return Locked();
        }

        if (state.EngineerAssignment is not null)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.EngineerAlreadyAssigned, "Ilya is already committed to a departure task.");
        }

        IReadOnlyList<MarketReportState> reports = state.Reports;
        string outcome;
        string reliabilityRisk = state.DepartureReliabilityRisk;

        if (assignment == EngineerAssignment.AnalyzeCourierManifest)
        {
            reports = state.Reports.Select(report => report.Id == "mars-convoy-arrival"
                ? report with { ConfidencePercent = 67, EngineerAnalyzed = true }
                : report).ToArray();
            outcome = "Ilya validates the courier's drive signature and cargo-mass ledger. The newer convoy report rises to 67% confidence; market uncertainty remains material.";
        }
        else
        {
            reliabilityRisk = "Low · Ilya inspected the 17% drive wear and cleared the departure envelope";
            outcome = "Ilya clears the worn drive for the Mars burn and lowers departure reliability risk. The competing market reports remain unresolved.";
        }

        IReadOnlyList<CrewMemberState> crew = state.Crew.Select(member => member.Id.Value == "ilya-sato"
            ? member with { CurrentAssignment = assignment == EngineerAssignment.AnalyzeCourierManifest ? "Courier manifest analysis complete" : "Drive inspection complete" }
            : member).ToArray();

        return Success(state with
        {
            EngineerAssignment = assignment,
            EngineerOutcome = outcome,
            DepartureReliabilityRisk = reliabilityRisk,
            Reports = reports,
            Crew = crew,
        });
    }

    public static CommandResult<GameState> AuthorizeDeparture(GameState state)
    {
        if (state.DepartureAuthorized)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.DepartureAlreadyAuthorized, "Departure is already authorized and the manifest is locked.");
        }

        bool requiredCargoLoaded = state.Cargo.Any(item =>
            item.CommodityId == state.Contract.CommodityId &&
            item.IsContractCargo &&
            item.Sealed &&
            item.Quantity >= state.Contract.Quantity);

        List<string> missing = [];
        if (!state.BriefingAcknowledged) missing.Add("acknowledge the crew briefing");
        if (state.Contract.Status != ContractStatus.Accepted) missing.Add("accept the medical-membrane contract");
        if (!requiredCargoLoaded) missing.Add("load the sealed 18-tonne shipment");
        if (state.EngineerAssignment is null) missing.Add("complete Ilya's engineer assignment");

        if (missing.Count > 0)
        {
            return CommandResult<GameState>.Failure(CommandErrorCodes.DepartureRequirementsNotMet, $"Before authorizing departure, {string.Join(", ", missing)}.");
        }

        long acceptedSequence = checked(state.CommandSequence + 1);
        EngineerAssignment engineerAssignment = state.EngineerAssignment
            ?? throw new InvalidOperationException("Validated departure state must include an engineer assignment.");
        DepartureManifestState manifest = new(
            state.Time,
            acceptedSequence,
            state.Contract.OriginStationId,
            state.Contract.DestinationStationId,
            state.Cargo.ToArray(),
            state.Money,
            engineerAssignment,
            state.ReportDecision,
            state.Contract.Id);

        return CommandResult<GameState>.Success(state with
        {
            CommandSequence = acceptedSequence,
            DepartureAuthorized = true,
            DepartureManifest = manifest,
            Journey = state.Journey is null ? null : state.Journey with
            {
                Phase = JourneyPhase.DepartureAuthorized,
                LastOutcome = "Departure manifest authorized. Wayfarer is ready to undock.",
            },
        });
    }

    private static CommandResult<GameState> Success(GameState state) =>
        CommandResult<GameState>.Success(state with { CommandSequence = checked(state.CommandSequence + 1) });

    private static CommandResult<GameState> Locked() =>
        CommandResult<GameState>.Failure(CommandErrorCodes.DepartureLocked, "The authorized departure manifest is locked for the active voyage.");
}
