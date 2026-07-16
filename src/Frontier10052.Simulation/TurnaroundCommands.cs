using Frontier10052.Domain;

namespace Frontier10052.Simulation;

public static class TurnaroundCommands
{
    private const long LienServiceCost = 6_000;
    private const long LienCapitalization = 1_500;
    private const long FuelUnitCost = 85;

    public static CommandResult<GameState> ServiceLien(GameState state)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        if (check.Value!.LienDisposition is not null) return Failure(CommandErrorCodes.LienAlreadyResolved, "The inherited lien decision is already recorded for this turnaround.");
        if (state.Money.Value < LienServiceCost) return Failure(CommandErrorCodes.InsufficientCredits, $"Servicing the lien requires {LienServiceCost:N0} credits; only {state.Money.Value:N0} are available.");

        LienState lien = state.Lien ?? new LienState(new Credits(72_000), null, []);
        Credits payment = new(LienServiceCost);
        Credits principal = lien.Principal - payment;
        LienPaymentState entry = new(state.Time, LienDisposition.Serviced, payment, principal, "Scheduled inherited-lien service paid at Mars; commercial trust improved.");
        TurnaroundState turnaround = check.Value with { LienDisposition = LienDisposition.Serviced, LastOutcome = "Paid 6,000 credits against Wayfarer's inherited lien. Principal is now 66,000 credits." };
        return Success(state with
        {
            Money = state.Money - payment,
            Lien = new LienState(principal, LienDisposition.Serviced, [.. lien.PaymentHistory, entry]),
            CommercialTrust = checked(state.CommercialTrust + 2),
            Turnaround = turnaround,
        });
    }

    public static CommandResult<GameState> DeferLien(GameState state)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        if (check.Value!.LienDisposition is not null) return Failure(CommandErrorCodes.LienAlreadyResolved, "The inherited lien decision is already recorded for this turnaround.");

        LienState lien = state.Lien ?? new LienState(new Credits(72_000), null, []);
        Credits capitalization = new(LienCapitalization);
        Credits principal = lien.Principal + capitalization;
        LienPaymentState entry = new(state.Time, LienDisposition.Deferred, capitalization, principal, "Lien service deferred; 1,500 credits capitalized into principal and commercial trust reduced.");
        TurnaroundState turnaround = check.Value with { LienDisposition = LienDisposition.Deferred, LastOutcome = "Deferred the inherited lien. Capitalized principal is now 73,500 credits." };
        return Success(state with
        {
            Lien = new LienState(principal, LienDisposition.Deferred, [.. lien.PaymentHistory, entry]),
            CommercialTrust = checked(state.CommercialTrust - 2),
            Turnaround = turnaround,
        });
    }

    public static CommandResult<GameState> Refuel(GameState state, int percentagePoints)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        if (percentagePoints <= 0) return Failure(CommandErrorCodes.InvalidFuelQuantity, "Choose at least one fuel percentage point.");
        if (state.Ship.FuelPercent + percentagePoints > 100) return Failure(CommandErrorCodes.FuelTankCapacityExceeded, $"Wayfarer can accept at most {100 - state.Ship.FuelPercent} more fuel points.");

        long total = checked(FuelUnitCost * percentagePoints);
        if (state.Money.Value < total) return Failure(CommandErrorCodes.InsufficientCredits, $"This refuel costs {total:N0} credits; only {state.Money.Value:N0} are available.");
        int hours = checked((percentagePoints + 9) / 10);
        CommandResult<bool> timeCheck = CheckOfferWindow(state, check.Value!, hours);
        if (!timeCheck.IsSuccess) return Fail(timeCheck.Error!);

        TurnaroundState turnaround = check.Value! with { LastOutcome = $"Added {percentagePoints}% fuel for {total:N0} credits in {hours} hour{(hours == 1 ? string.Empty : "s")}." };
        return Success(state with
        {
            Time = state.Time.AddHours(hours),
            Money = state.Money - new Credits(total),
            Ship = state.Ship with { FuelPercent = state.Ship.FuelPercent + percentagePoints },
            Turnaround = turnaround,
        });
    }

    public static CommandResult<GameState> Repair(GameState state, RepairService service)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        if (check.Value!.RepairService is not null) return Failure(CommandErrorCodes.RepairAlreadyResolved, "The repair decision is already recorded for this turnaround.");

        (long cost, int hours, int removal, string provenance) = service switch
        {
            RepairService.Certified => (5_400, 8, 12, "Mars Industrial Port certified drive service"),
            RepairService.IlyaFieldService => (1_800, 5, 6, "Ilya Sato field service aboard Wayfarer"),
            RepairService.Deferred => (0, 0, 0, "Repair deferred by command decision"),
            _ => throw new ArgumentOutOfRangeException(nameof(service)),
        };
        if (state.Money.Value < cost) return Failure(CommandErrorCodes.InsufficientCredits, $"This repair requires {cost:N0} credits; only {state.Money.Value:N0} are available.");
        CommandResult<bool> timeCheck = CheckOfferWindow(state, check.Value, hours);
        if (!timeCheck.IsSuccess) return Fail(timeCheck.Error!);

        int removed = Math.Min(removal, state.Ship.DriveWearPercent);
        int nextWear = state.Ship.DriveWearPercent - removed;
        MaintenanceState maintenance = state.Maintenance ?? new MaintenanceState(100 - state.Ship.HullWearPercent, 100 - state.Ship.DriveWearPercent, false, []);
        RepairRecordState repair = new(state.Time.AddHours(hours), service, new Credits(cost), hours, removed, provenance);
        IReadOnlyList<CrewMemberState> crew = service == RepairService.IlyaFieldService
            ? UpdateCrew(state.Crew, "ilya-sato", member => AddMemory(member with { Fatigue = Math.Clamp(member.Fatigue + 5, 0, 100), CurrentAssignment = "Completed Mars field service" }, state.Time.AddHours(hours), "repair", "Ilya performed the lower-cost field service and carries its fatigue into the next voyage.", 0))
            : state.Crew;
        string outcome = service switch
        {
            RepairService.Certified => $"Certified service removed {removed}% drive wear in eight hours.",
            RepairService.IlyaFieldService => $"Ilya's field service removed {removed}% drive wear and added 5 fatigue.",
            _ => "Repairs were explicitly deferred; the active route retains the additional drive-risk envelope.",
        };

        return Success(state with
        {
            Time = state.Time.AddHours(hours),
            Money = state.Money - new Credits(cost),
            Ship = state.Ship with { DriveWearPercent = nextWear },
            Crew = crew,
            Maintenance = new MaintenanceState(maintenance.HullConditionPercent, 100 - nextWear, service == RepairService.Deferred, [.. maintenance.RepairHistory, repair]),
            Turnaround = check.Value with { RepairService = service, LastOutcome = outcome },
        });
    }

    public static CommandResult<GameState> RestCrew(GameState state, CrewRestService service)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        if (check.Value!.CrewRestService is not null) return Failure(CommandErrorCodes.RestAlreadyResolved, "The crew-rest decision is already recorded for this turnaround.");

        (long cost, int hours, int reduction, string label) = service switch
        {
            CrewRestService.FullLayover => (1_200, 8, 10, "full Mars layover"),
            CrewRestService.TurnaroundWatches => (400, 4, 4, "turnaround watches"),
            _ => throw new ArgumentOutOfRangeException(nameof(service)),
        };
        if (state.Money.Value < cost) return Failure(CommandErrorCodes.InsufficientCredits, $"This crew rest requires {cost:N0} credits; only {state.Money.Value:N0} are available.");
        CommandResult<bool> timeCheck = CheckOfferWindow(state, check.Value, hours);
        if (!timeCheck.IsSuccess) return Fail(timeCheck.Error!);

        GameTime completedAt = state.Time.AddHours(hours);
        IReadOnlyList<CrewMemberState> crew = state.Crew.Select(member => AddMemory(
            member with { Fatigue = Math.Max(0, member.Fatigue - reduction), CurrentAssignment = $"Completed {label}" },
            completedAt, "rest", $"The crew completed a {label}; fatigue fell by up to {reduction} points.", 0)).ToArray();
        return Success(state with
        {
            Time = completedAt,
            Money = state.Money - new Credits(cost),
            Crew = crew,
            Turnaround = check.Value with { CrewRestService = service, LastOutcome = $"Crew completed {label}: {hours} hours, {cost:N0} credits, {reduction} fatigue removed." },
        });
    }

    public static CommandResult<GameState> SelectContract(GameState state, ContractId contractId, int legalExposureOnAccept)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        TurnaroundState turnaround = check.Value!;
        if (turnaround.SelectedContractId is not null) return Failure(CommandErrorCodes.OfferAlreadyClosed, "A turnaround contract is already selected; the competing opportunity is closed.");
        if (state.Time.CompareTo(turnaround.OffersExpireAt) > 0) return Failure(CommandErrorCodes.OfferExpired, "Both Mars offers have expired. No second voyage can be authorized from this turnaround.");

        ContractState? offered = state.AllContracts.SingleOrDefault(item => item.Id == contractId && item.IsTurnaroundOffer);
        if (offered is null || offered.Status != ContractStatus.Offered) return Failure(CommandErrorCodes.ContractUnavailable, "That contract is not an open Mars turnaround offer.");
        if (state.CargoAvailable < offered.Quantity) return Failure(CommandErrorCodes.InsufficientCapacity, $"The selected contract needs {offered.Quantity.Value} free tonnes; only {state.CargoAvailable.Value} remain in the hold.");

        ContractState accepted = offered with { Status = ContractStatus.Accepted, AcceptedAt = state.Time, Outcome = "Selected for the Mars turnaround; issuer cargo locked in Wayfarer's hold." };
        IReadOnlyList<ContractState> contracts = state.AllContracts.Select(item => item.Id == contractId
            ? accepted
            : item.IsTurnaroundOffer && item.Status == ContractStatus.Offered
                ? item with { Status = ContractStatus.Transformed, SettledAt = state.Time, Outcome = "Closed when the competing Mars offer was selected; retained as transformed opportunity history." }
                : item).ToArray();

        bool pluto = accepted.DestinationStationId.Value == "pluto-gateway";
        IReadOnlyList<CrewMemberState> crew = ApplyDestinationConflict(state.Crew, state.Time, pluto);
        bool noorAvailable = state.Crew.Any(item => item.Id.Value == "noor-okafor" && item.Available);
        int confidence = pluto ? (noorAvailable ? 100 : 82) : (noorAvailable ? 85 : 55);
        int exposure = checked(state.LegalExposure + legalExposureOnAccept + (!pluto && !noorAvailable ? 2 : 0));
        string destination = pluto ? "Pluto Gateway" : "Ceres Freehold Anchorage";

        return Success(state with
        {
            Contract = accepted,
            Contracts = contracts,
            Cargo = [.. state.Cargo, new CargoLineState(accepted.CommodityId, accepted.Quantity, true, true)],
            Crew = crew,
            LegalExposure = exposure,
            ManifestConfidencePercent = confidence,
            Turnaround = turnaround with { SelectedContractId = contractId, LastOutcome = $"Selected {destination}. The required cargo is sealed; the competing offer is now transformed history." },
            Journey = state.Journey is null ? null : state.Journey with { ActiveContractId = contractId, LastOutcome = $"Mars turnaround contract selected for {destination}." },
        });
    }

    public static CommandResult<GameState> AuthorizeDeparture(GameState state, RouteTravelState authoredRoute)
    {
        CommandResult<TurnaroundState> check = RequireMutableTurnaround(state);
        if (!check.IsSuccess) return Fail(check.Error!);
        TurnaroundState turnaround = check.Value!;
        List<string> missing = [];
        if (turnaround.LienDisposition is null) missing.Add("service or defer the inherited lien");
        if (turnaround.RepairService is null) missing.Add("choose a repair or explicitly defer it");
        if (turnaround.CrewRestService is null) missing.Add("complete a crew-rest plan");
        if (turnaround.SelectedContractId is null) missing.Add("select one Mars contract");
        if (missing.Count > 0) return Failure(CommandErrorCodes.ServiceRequirementsNotMet, $"Before authorizing departure, {string.Join(", ", missing)}.");

        if (authoredRoute.OriginStationId != state.Ship.StationId || authoredRoute.DestinationStationId != state.Contract.DestinationStationId)
            return Failure(CommandErrorCodes.RouteUnavailable, "The selected route does not connect the current station to the active contract destination.");

        CrewMemberState? mara = state.Crew.SingleOrDefault(item => item.Id.Value == "mara-venn");
        bool maraProfileAvailable = mara is { Available: true, Fatigue: < 70 };
        int fuelCost = authoredRoute.FuelCostPercent + (maraProfileAvailable ? 0 : 2);
        int duration = authoredRoute.BaselineDurationHours + (maraProfileAvailable ? 0 : 4);
        RouteTravelState route = authoredRoute with
        {
            BaselineDurationHours = duration,
            FuelCostPercent = fuelCost,
            EncounterAtHour = Math.Min(authoredRoute.EncounterAtHour, duration - 1),
            DepartedAt = state.Time,
            EstimatedArrival = state.Time.AddHours(duration),
            ElapsedBaselineHours = 0,
            DelayHours = 0,
        };

        CargoLineState? required = state.Cargo.SingleOrDefault(item => item.IsContractCargo && item.CommodityId == state.Contract.CommodityId);
        if (required is null || !required.Sealed || required.Quantity < state.Contract.Quantity)
            return Failure(CommandErrorCodes.DepartureRequirementsNotMet, "The selected contract's free issuer cargo is not sealed at its required quantity.");
        if (state.Ship.FuelPercent < fuelCost)
            return Failure(CommandErrorCodes.DepartureRequirementsNotMet, $"The {fuelCost}% route profile exceeds Wayfarer's {state.Ship.FuelPercent}% fuel state.");
        if (state.Ship.DriveWearPercent + route.BaseDriveWearPercent > 100)
            return Failure(CommandErrorCodes.DepartureRequirementsNotMet, "The route's drive wear would exceed Wayfarer's mechanical limit.");

        long acceptedSequence = checked(state.CommandSequence + 1);
        DepartureManifestState manifest = new(
            state.Time,
            acceptedSequence,
            route.OriginStationId,
            route.DestinationStationId,
            state.Cargo.ToArray(),
            state.Money,
            state.EngineerAssignment ?? EngineerAssignment.InspectDrive,
            state.ReportDecision,
            state.Contract.Id,
            route.Id,
            state.ManifestConfidencePercent);

        return CommandResult<GameState>.Success(state with
        {
            CommandSequence = acceptedSequence,
            DepartureAuthorized = true,
            DepartureManifest = manifest,
            Turnaround = turnaround with { DepartureAuthorized = true, LastOutcome = "Second-voyage manifest authorized. Wayfarer is ready to leave Mars." },
            Journey = state.Journey is null
                ? null
                : state.Journey with { Phase = JourneyPhase.DepartureAuthorized, Route = route, Encounter = null, DestinationManifest = null, VoyageNumber = 2, LastOutcome = "Second-voyage manifest authorized at Mars." },
        });
    }

    private static CommandResult<TurnaroundState> RequireMutableTurnaround(GameState state)
    {
        if (state.Turnaround is null || state.Ship.StationId != state.Turnaround.StationId)
            return CommandResult<TurnaroundState>.Failure(CommandErrorCodes.TurnaroundUnavailable, "Mars turnaround services are available only after settling the first contract.");
        if (state.DepartureAuthorized || state.Turnaround.DepartureAuthorized)
            return CommandResult<TurnaroundState>.Failure(CommandErrorCodes.DepartureLocked, "The second-voyage departure manifest is authorized and locked.");
        return CommandResult<TurnaroundState>.Success(state.Turnaround);
    }

    private static CommandResult<bool> CheckOfferWindow(GameState state, TurnaroundState turnaround, int hours)
    {
        if (turnaround.SelectedContractId is null && state.Time.AddHours(hours).CompareTo(turnaround.OffersExpireAt) > 0)
            return CommandResult<bool>.Failure(CommandErrorCodes.OfferExpirationRisk, "This service would consume the remaining offer window before either Mars contract is selected. Select an offer first.");
        return CommandResult<bool>.Success(true);
    }

    private static IReadOnlyList<CrewMemberState> ApplyDestinationConflict(IReadOnlyList<CrewMemberState> crew, GameTime time, bool pluto)
    {
        string destination = pluto ? "Pluto" : "Ceres";
        return crew.Select(member =>
        {
            int delta = member.Id.Value switch
            {
                "tomas-vale" => pluto ? 5 : -2,
                "ilya-sato" => pluto ? -2 : 5,
                _ => 0,
            };
            CrewMemberState remembered = AddMemory(member, time, "destination-decision", $"Command selected {destination}; the rejected route closed for this Mars turnaround.", 0);
            return delta == 0 ? remembered : AddMemory(
                remembered with { Loyalty = Math.Clamp(remembered.Loyalty + delta, 0, 100) },
                time,
                "crew-reaction",
                delta > 0 ? $"{member.Id.Value} supported the {destination} decision." : $"{member.Id.Value} opposed the {destination} decision.",
                delta);
        }).ToArray();
    }

    internal static CrewMemberState AddMemory(CrewMemberState member, GameTime time, string kind, string summary, int loyaltyDelta) =>
        member with { Memories = [.. member.Memories ?? [], new CrewMemoryState(time, kind, summary, loyaltyDelta)] };

    internal static IReadOnlyList<CrewMemberState> UpdateCrew(IReadOnlyList<CrewMemberState> crew, string id, Func<CrewMemberState, CrewMemberState> update) =>
        crew.Select(member => member.Id.Value == id ? update(member) : member).ToArray();

    private static CommandResult<GameState> Success(GameState state) => CommandResult<GameState>.Success(state with { CommandSequence = checked(state.CommandSequence + 1) });
    private static CommandResult<GameState> Failure(string code, string message) => CommandResult<GameState>.Failure(code, message);
    private static CommandResult<GameState> Fail(CommandError error) => Failure(error.Code, error.Message);
}
