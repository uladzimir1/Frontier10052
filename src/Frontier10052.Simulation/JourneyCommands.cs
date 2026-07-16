using System.Text;
using Frontier10052.Domain;

namespace Frontier10052.Simulation;

public static class JourneyCommands
{
    public static CommandResult<GameState> BeginVoyage(GameState state, RouteTravelState route)
    {
        JourneyState? journey = state.Journey;
        if (!state.DepartureAuthorized || journey?.Phase != JourneyPhase.DepartureAuthorized)
        {
            return journey?.Phase is JourneyPhase.InTransit or JourneyPhase.EncounterPending or JourneyPhase.Approach or JourneyPhase.Docked or JourneyPhase.Turnaround or JourneyPhase.Delivered
                ? Fail(CommandErrorCodes.JourneyAlreadyStarted, "Wayfarer has already left its origin on this manifest.")
                : Fail(CommandErrorCodes.DepartureNotAuthorized, "Authorize and lock the active departure manifest before undocking.");
        }

        if (route.OriginStationId != state.Ship.StationId || route.DestinationStationId != state.Contract.DestinationStationId)
            return Fail(CommandErrorCodes.InvalidJourneyPhase, "The selected route does not match Wayfarer's authorized manifest.");
        if (state.DepartureManifest?.RouteId is RouteId authorizedRoute && authorizedRoute != route.Id)
            return Fail(CommandErrorCodes.InvalidJourneyPhase, "The selected route does not match the route locked in the departure manifest.");
        if (state.Ship.FuelPercent < route.FuelCostPercent || state.Ship.DriveWearPercent + route.BaseDriveWearPercent > 100)
            return Fail(CommandErrorCodes.InvalidJourneyPhase, "Wayfarer cannot safely meet this route's fuel and wear requirements.");

        RouteTravelState committedRoute = route with
        {
            DepartedAt = state.Time,
            EstimatedArrival = state.Time.AddHours(route.BaselineDurationHours),
            ElapsedBaselineHours = 0,
            DelayHours = 0,
        };
        IReadOnlyList<StationVisitState> visits = state.AllStationVisits.Select((visit, index) =>
            index == state.AllStationVisits.Count - 1 && visit.StationId == state.Ship.StationId && visit.DepartedAt is null
                ? visit with { DepartedAt = state.Time }
                : visit).ToArray();

        string outcome = $"Wayfarer cleared {route.OriginStationId.Value} and committed to {route.DestinationStationId.Value}.";
        return Success(state with
        {
            Ship = state.Ship with
            {
                FuelPercent = state.Ship.FuelPercent - route.FuelCostPercent,
                DriveWearPercent = state.Ship.DriveWearPercent + route.BaseDriveWearPercent,
            },
            Maintenance = state.Maintenance is null ? null : state.Maintenance with { DriveConditionPercent = 100 - (state.Ship.DriveWearPercent + route.BaseDriveWearPercent) },
            StationVisits = visits,
            Journey = journey with
            {
                Phase = JourneyPhase.InTransit,
                DockedStationId = null,
                Route = committedRoute,
                Encounter = null,
                DestinationManifest = null,
                LastOutcome = outcome,
            },
        });
    }

    public static CommandResult<GameState> AdvanceVoyage(GameState state, IReadOnlyList<EncounterId> encounterPool)
    {
        JourneyState? journey = state.Journey;
        RouteTravelState? route = journey?.Route;
        if (journey?.Phase == JourneyPhase.EncounterPending)
            return Fail(CommandErrorCodes.EncounterResolutionRequired, "Resolve the active encounter before advancing along the route.");
        if (journey?.Phase != JourneyPhase.InTransit || route is null)
            return Fail(CommandErrorCodes.InvalidJourneyPhase, "Travel can advance only while Wayfarer is in transit.");

        if (route.ElapsedBaselineHours < route.EncounterAtHour)
        {
            int hours = route.EncounterAtHour - route.ElapsedBaselineHours;
            EncounterId selected = SelectEncounter(state, encounterPool);
            GameTime nextTime = state.Time.AddHours(hours);
            return Success(state with
            {
                Time = nextTime,
                Journey = journey with
                {
                    Phase = JourneyPhase.EncounterPending,
                    Route = route with { ElapsedBaselineHours = route.EncounterAtHour },
                    Encounter = new EncounterState(selected, EncounterStatus.Pending, nextTime, null, string.Empty),
                    LastOutcome = "Transit is paused at an unresolved route contact.",
                },
            });
        }

        int remaining = route.BaselineDurationHours - route.ElapsedBaselineHours;
        return Success(state with
        {
            Time = state.Time.AddHours(remaining),
            Journey = journey with
            {
                Phase = JourneyPhase.Approach,
                Route = route with { ElapsedBaselineHours = route.BaselineDurationHours },
                LastOutcome = $"{route.DestinationStationId.Value} granted Wayfarer an approach corridor.",
            },
        });
    }

    public static CommandResult<GameState> ResolveCurrentEncounter(GameState state, EncounterResponse response)
    {
        JourneyState? journey = state.Journey;
        EncounterState? encounter = journey?.Encounter;
        RouteTravelState? route = journey?.Route;
        if (journey?.Phase != JourneyPhase.EncounterPending || encounter?.Status != EncounterStatus.Pending || route is null)
            return Fail(CommandErrorCodes.InvalidJourneyPhase, "There is no unresolved encounter on the active route.");

        int delay = 0;
        int fuel = 0;
        int wear = 0;
        int hullWear = 0;
        long credits = 0;
        int trust = 0;
        int legalExposure = 0;
        int tca = 0;
        bool dumpCargo = false;
        IReadOnlyList<CrewMemberState> crew = state.Crew;
        string outcome;

        switch (encounter.Id.Value, response)
        {
            case ("sol-transit-inspection", EncounterResponse.InspectionStandardCompliance):
                delay = 3; trust = 1;
                outcome = "Noor opens the legal manifest. The patrol clears every seal and records Wayfarer as a cooperative carrier.";
                break;
            case ("sol-transit-inspection", EncounterResponse.InspectionMedicalPriority):
                if (!HasAvailableCrew(state, "noor-okafor") || !HasSealedContractCargo(state)) return OptionUnavailable("Medical-priority clearance requires Noor and the intact sealed shipment.");
                delay = 1; fuel = 2;
                outcome = "Noor invokes the relief corridor and preserves chain of custody while Mara spends reserve fuel on the priority vector.";
                break;
            case ("drive-coolant-failure", EncounterResponse.MechanicalFieldRepair):
                if (!HasAvailableCrew(state, "ilya-sato")) return OptionUnavailable("Field repair requires Ilya to be available.");
                bool inspected = state.EngineerAssignment == EngineerAssignment.InspectDrive;
                delay = inspected ? 2 : 5; wear = inspected ? 1 : 3;
                crew = AddFatigueAndMemory(crew, "ilya-sato", 3, 0, state.Time.AddHours(delay), "encounter", "Ilya stabilized the first-voyage coolant failure.");
                outcome = inspected
                    ? "Ilya catches the failing coupling at the inspected service point and restores the drive with limited wear."
                    : "Ilya finds the coupling without a preflight map; the repair holds, but costs time and additional drive wear.";
                break;
            case ("drive-coolant-failure", EncounterResponse.MechanicalReducedBurn):
                if (!HasAvailableCrew(state, "mara-venn")) return OptionUnavailable("The reduced-burn profile requires Mara to be available.");
                delay = 7;
                crew = AddFatigueAndMemory(crew, "mara-venn", 2, 0, state.Time.AddHours(delay), "encounter", "Mara carried the weakened drive on a reduced-burn profile.");
                outcome = "Mara reduces the burn and carries the weakened cooling loop to destination without adding drive wear.";
                break;
            case ("ceres-lane-pirate-demand", EncounterResponse.PiratePayDemand):
                if (state.Money.Value < 2_500) return OptionUnavailable("The pirate demand requires 2,500 available credits.");
                delay = 1; credits = 2_500;
                outcome = "The transfer clears. The raider releases Wayfarer and records the commander as a payer.";
                break;
            case ("ceres-lane-pirate-demand", EncounterResponse.PirateDumpSpeculativeCargo):
                if (!state.Cargo.Any(item => !item.IsContractCargo)) return OptionUnavailable("No speculative cargo is available to surrender.");
                delay = 2; dumpCargo = true;
                outcome = "The raider recovers the dumped speculative freight and breaks pursuit. Contract cargo remains sealed.";
                break;
            case ("ceres-lane-pirate-demand", EncounterResponse.PirateHardBurn):
                if (!HasAvailableCrew(state, "mara-venn")) return OptionUnavailable("The hard-burn escape requires Mara to be available.");
                if (state.Ship.FuelPercent < 4 || state.Ship.DriveWearPercent > 97) return OptionUnavailable("The hard burn requires four fuel points and enough drive margin for three wear points.");
                fuel = 4; wear = 3;
                crew = AddFatigueAndMemory(crew, "mara-venn", 3, 0, state.Time, "encounter", "Mara executed a hard-burn escape through the patrol boundary.");
                outcome = "Mara drives Wayfarer through the patrol boundary before the raider can match velocity.";
                break;
            case ("pluto-migration-medical-emergency", EncounterResponse.MigrationMedicalAssist):
                if (!HasAvailableCrew(state, "tomas-vale")) return OptionUnavailable("Medical assistance requires Tomas to be available.");
                delay = 4; trust = 2; tca = 2;
                crew = AddFatigueAndMemory(crew, "tomas-vale", 6, 2, state.Time.AddHours(delay), "humanitarian-response", "Tomas spent four hours stabilizing patients aboard Pilgrim Seven.");
                outcome = "Tomas answers the migration tender. Four hours and six fatigue buy lives, TCA standing, and commercial trust.";
                break;
            case ("pluto-migration-medical-emergency", EncounterResponse.MigrationConserveSupplies):
                trust = -1; tca = -2;
                crew = AddFatigueAndMemory(crew, "tomas-vale", 0, -4, state.Time, "humanitarian-refusal", "Command conserved Wayfarer's supplies and declined Pilgrim Seven's medical request.");
                outcome = "Wayfarer conserves its supplies. The voyage continues, but Tomas and the TCA record the refusal.";
                break;
            case ("ceres-debris-coolant-breach", EncounterResponse.DebrisIlyaRepair):
                if (!HasAvailableCrew(state, "ilya-sato")) return OptionUnavailable("The coolant repair requires Ilya to be available.");
                delay = state.Turnaround?.RepairService switch { RepairService.Certified => 2, RepairService.IlyaFieldService => 3, _ => 6 };
                wear = state.Turnaround?.RepairService == RepairService.Deferred ? 2 : state.Turnaround?.RepairService == RepairService.IlyaFieldService ? 1 : 0;
                hullWear = 1;
                crew = AddFatigueAndMemory(crew, "ilya-sato", 5, 1, state.Time.AddHours(delay), "mechanical-response", "Ilya repaired the debris-strike coolant breach; prior Mars service determined the duration.");
                outcome = $"Ilya contains the coolant breach in {delay} hours. The prior Mars service record determined the repair quality.";
                break;
            case ("ceres-debris-coolant-breach", EncounterResponse.DebrisMaraEvasion):
                if (!HasAvailableCrew(state, "mara-venn")) return OptionUnavailable("The evasive profile requires Mara to be available.");
                fuel = 4; wear = 4; hullWear = 1;
                crew = AddFatigueAndMemory(crew, "mara-venn", 3, 1, state.Time, "evasive-response", "Mara evaded the densest debris at the cost of fuel and drive wear.");
                outcome = "Mara rolls Wayfarer clear of the breach cascade. The ship keeps moving at an additional four fuel and four drive wear.";
                break;
            default:
                return OptionUnavailable("That response is not available for this encounter.");
        }

        if (state.Ship.FuelPercent < fuel || state.Ship.DriveWearPercent + wear > 100)
            return OptionUnavailable("Wayfarer lacks the required fuel or drive margin for that response.");

        IReadOnlyList<CargoLineState> cargo = dumpCargo ? state.Cargo.Where(item => item.IsContractCargo).ToArray() : state.Cargo;
        int nextWear = state.Ship.DriveWearPercent + wear;
        int nextHullWear = Math.Clamp(state.Ship.HullWearPercent + hullWear, 0, 100);
        IReadOnlyList<FactionStandingState> standings = AdjustFaction(state.AllFactionStandings, FactionIds.TerranContinuityAuthority, tca);
        return Success(state with
        {
            Time = state.Time.AddHours(delay),
            Money = state.Money - new Credits(credits),
            CommercialTrust = checked(state.CommercialTrust + trust),
            LegalExposure = checked(state.LegalExposure + legalExposure),
            FactionStandings = standings,
            Crew = crew,
            Cargo = cargo,
            Ship = state.Ship with { FuelPercent = state.Ship.FuelPercent - fuel, DriveWearPercent = nextWear, HullWearPercent = nextHullWear },
            Maintenance = state.Maintenance is null ? null : state.Maintenance with { HullConditionPercent = 100 - nextHullWear, DriveConditionPercent = 100 - nextWear },
            Journey = journey with
            {
                Phase = JourneyPhase.InTransit,
                Route = route with { DelayHours = checked(route.DelayHours + delay) },
                Encounter = encounter with { Status = EncounterStatus.Resolved, Response = response, Outcome = outcome },
                LastOutcome = outcome,
            },
        });
    }

    public static CommandResult<GameState> Arrive(GameState state)
    {
        JourneyState? journey = state.Journey;
        RouteTravelState? route = journey?.Route;
        if (journey?.Phase != JourneyPhase.Approach || route is null || route.ElapsedBaselineHours < route.BaselineDurationHours)
            return Fail(CommandErrorCodes.DockingUnavailable, "Docking is unavailable until the active route and encounter are complete.");

        DepartureManifestState manifest = state.DepartureManifest ?? throw new InvalidOperationException("Arrival requires a persisted departure manifest.");
        JourneyHistoryState history = new(
            journey.VoyageNumber,
            route.Id,
            route.OriginStationId,
            route.DestinationStationId,
            state.Contract.Id,
            route.DepartedAt,
            state.Time,
            null,
            manifest,
            journey.Encounter,
            null,
            "Arrived; destination delivery is pending.");
        IReadOnlyList<JourneyHistoryState> histories = [.. state.AllJourneyHistory.Where(item => item.VoyageNumber != journey.VoyageNumber), history];
        return Success(state with
        {
            Ship = state.Ship with { StationId = route.DestinationStationId },
            JourneyHistory = histories,
            StationVisits = [.. state.AllStationVisits, new StationVisitState(route.DestinationStationId, state.Time, null)],
            Journey = journey with { Phase = JourneyPhase.Docked, DockedStationId = route.DestinationStationId, LastOutcome = $"Wayfarer is secured at {route.DestinationStationId.Value}." },
        });
    }

    public static CommandResult<GameState> SellCargo(GameState state, CommodityId commodityId, Tonnes quantity)
    {
        JourneyState? journey = state.Journey;
        if (journey?.Phase is not (JourneyPhase.Docked or JourneyPhase.Turnaround or JourneyPhase.Delivered)) return Fail(CommandErrorCodes.InvalidJourneyPhase, "Cargo can be sold only while docked at a station with an open bid.");
        if (quantity.Value <= 0) return Fail(CommandErrorCodes.InvalidQuantity, "Choose at least one tonne to sell.");

        CargoLineState? line = state.Cargo.SingleOrDefault(item => item.CommodityId == commodityId && !item.IsContractCargo);
        if (line is null) return Fail(CommandErrorCodes.CargoNotSellable, "Contract cargo and absent commodities cannot be sold on the open market.");
        if (line.Quantity < quantity) return Fail(CommandErrorCodes.InsufficientCargo, $"Only {line.Quantity.Value} tonnes are available to sell.");
        DestinationMarketListingState? listing = journey.DestinationMarket.StationId == state.Ship.StationId
            ? journey.DestinationMarket.Listings.SingleOrDefault(item => item.CommodityId == commodityId)
            : null;
        if (listing is null) return Fail(CommandErrorCodes.CargoNotSellable, "The current station has no authored bid for that commodity.");

        Credits proceeds = listing.BidPrice * quantity;
        List<CargoLineState> cargo = [.. state.Cargo];
        int index = cargo.IndexOf(line);
        if (line.Quantity == quantity) cargo.RemoveAt(index); else cargo[index] = line with { Quantity = line.Quantity - quantity };
        CargoSaleState sale = new(commodityId, quantity, listing.BidPrice, proceeds, state.Ship.StationId);
        return Success(state with
        {
            Cargo = cargo,
            Money = state.Money + proceeds,
            Journey = journey with { Sales = [.. journey.Sales, sale], LastOutcome = $"Sold {quantity.Value} tonnes for {proceeds.Value:N0} credits at {state.Ship.StationId.Value}." },
        });
    }

    public static CommandResult<GameState> DeliverCargo(GameState state, IReadOnlyList<ContractState>? turnaroundOffers = null)
    {
        JourneyState? journey = state.Journey;
        if (journey?.Phase != JourneyPhase.Docked)
            return state.Contract.Status is ContractStatus.Completed or ContractStatus.Failed
                ? Fail(CommandErrorCodes.ContractAlreadySettled, "The active contract is already settled.")
                : Fail(CommandErrorCodes.ContractNotDeliverable, "Dock at the active contract destination before presenting its sealed shipment.");
        if (state.Ship.StationId != state.Contract.DestinationStationId)
            return Fail(CommandErrorCodes.ContractNotDeliverable, "The current station is not the active contract destination.");

        CargoLineState? line = state.Cargo.SingleOrDefault(item => item.CommodityId == state.Contract.CommodityId && item.IsContractCargo);
        bool validCargo = state.Contract.Status == ContractStatus.Accepted && line is not null && line.Sealed && line.Quantity >= state.Contract.Quantity;
        bool onTime = state.Time.CompareTo(state.Contract.Deadline) <= 0;
        bool valid = validCargo && onTime;
        ContractStatus status = valid ? ContractStatus.Completed : ContractStatus.Failed;
        (Credits money, LienState? lien) = valid
            ? (state.Money + state.Contract.Reward, state.Lien)
            : ApplyCharge(state.Money, state.Lien, state.Contract.FailurePenalty, state.Time, "Contract failure claim capitalized because available credits were insufficient.");

        IReadOnlyList<CargoLineState> cargo = line is null ? state.Cargo : state.Cargo.Where(item => item != line).ToArray();
        bool firstVoyage = journey.VoyageNumber == 1;
        string outcome = valid
            ? firstVoyage
                ? "Mars clinic custody accepted all eighteen sealed tonnes. Sol Mutual Relief released the contract reward."
                : $"{state.Contract.DestinationStationId.Value} accepted the locked cargo and released the contract reward."
            : $"Delivery failed {(validCargo ? "deadline" : "destination, seal, or quantity")} validation. The failure claim was applied once.";
        ContractState settled = state.Contract with { Status = status, SettledAt = state.Time, Outcome = outcome };

        int trustDelta = firstVoyage ? (valid ? 2 : -1) : state.Contract.IssuerFactionId == FactionIds.TerranContinuityAuthority ? (valid ? 3 : -2) : 0;
        int tcaDelta = firstVoyage ? (valid ? 1 : -1) : state.Contract.IssuerFactionId == FactionIds.TerranContinuityAuthority ? (valid ? 5 : -4) : (valid ? -1 : 0);
        int kuiperDelta = state.Contract.IssuerFactionId == FactionIds.KuiperSyndicates ? (valid ? 5 : -4) : 0;
        bool noorAvailable = HasAvailableCrew(state, "noor-okafor");
        int legalDelta = state.Contract.IssuerFactionId == FactionIds.KuiperSyndicates ? (valid ? (noorAvailable ? 1 : 3) : 2) : 0;
        IReadOnlyList<FactionStandingState> standings = AdjustFaction(state.AllFactionStandings, FactionIds.TerranContinuityAuthority, tcaDelta);
        standings = AdjustFaction(standings, FactionIds.KuiperSyndicates, kuiperDelta);

        DestinationManifestState destinationManifest = new(
            state.Ship.StationId,
            state.Contract.Id,
            state.Time,
            state.Cargo.ToArray(),
            status,
            money,
            checked(state.LegalExposure + legalDelta),
            outcome);
        JourneyHistoryState? existingHistory = state.AllJourneyHistory.SingleOrDefault(item => item.VoyageNumber == journey.VoyageNumber);
        if (existingHistory is null && state.DepartureManifest is not null && journey.Route is not null)
        {
            existingHistory = new JourneyHistoryState(journey.VoyageNumber, journey.Route.Id, journey.Route.OriginStationId, journey.Route.DestinationStationId, state.Contract.Id, journey.Route.DepartedAt, state.Time, null, state.DepartureManifest, journey.Encounter, null, string.Empty);
        }
        IReadOnlyList<JourneyHistoryState> histories = existingHistory is null
            ? state.AllJourneyHistory
            : [.. state.AllJourneyHistory.Where(item => item.VoyageNumber != journey.VoyageNumber), existingHistory with { DeliveredAt = state.Time, Encounter = journey.Encounter, DestinationManifest = destinationManifest, Outcome = outcome }];

        IReadOnlyList<ContractState> contracts = state.AllContracts.Select(item => item.Id == settled.Id ? settled : item).ToArray();
        TurnaroundState? turnaround = state.Turnaround;
        JourneyPhase nextPhase = JourneyPhase.Delivered;
        bool departureAuthorized = state.DepartureAuthorized;
        DepartureManifestState? departureManifest = state.DepartureManifest;
        if (firstVoyage)
        {
            IReadOnlyList<ContractState> offers = turnaroundOffers ?? [];
            contracts = [.. contracts, .. offers.Where(offer => contracts.All(item => item.Id != offer.Id))];
            turnaround = new TurnaroundState(state.Ship.StationId, state.Time, state.Time.AddHours(24), null, null, null, null, false, "First-contract settlement opened a 24-hour Mars offer window.");
            nextPhase = JourneyPhase.Turnaround;
            departureAuthorized = false;
            departureManifest = null;
        }

        return Success(state with
        {
            Contract = settled,
            Contracts = contracts,
            Cargo = cargo,
            Money = money,
            Lien = lien,
            CommercialTrust = checked(state.CommercialTrust + trustDelta),
            FactionStandings = standings,
            LegalExposure = checked(state.LegalExposure + legalDelta),
            DepartureAuthorized = departureAuthorized,
            DepartureManifest = departureManifest,
            Turnaround = turnaround,
            JourneyHistory = histories,
            Journey = journey with { Phase = nextPhase, DestinationManifest = destinationManifest, LastOutcome = outcome },
        });
    }

    public static EncounterId SelectEncounter(GameState state, IReadOnlyList<EncounterId> encounterPool)
    {
        if (encounterPool.Count == 0) throw new ArgumentException("At least one encounter is required.", nameof(encounterPool));
        DepartureManifestState manifest = state.DepartureManifest ?? throw new InvalidOperationException("Encounter selection requires an authorized manifest.");
        string cargo = string.Join(',', manifest.Cargo.OrderBy(item => item.CommodityId.Value, StringComparer.Ordinal).Select(item => $"{item.CommodityId.Value}:{item.Quantity.Value}"));
        string input = $"{state.Seed}|{manifest.AuthorizedAtCommandSequence}|{manifest.RouteId?.Value}|{state.Journey?.VoyageNumber}|{manifest.EngineerAssignment}|{cargo}";
        ulong hash = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(input)) { hash ^= value; hash *= 1099511628211UL; }
        return encounterPool[checked((int)(hash % (ulong)encounterPool.Count))];
    }

    private static (Credits Money, LienState? Lien) ApplyCharge(Credits money, LienState? lien, Credits charge, GameTime time, string note)
    {
        long paid = Math.Min(money.Value, charge.Value);
        long shortfall = charge.Value - paid;
        Credits nextMoney = new(money.Value - paid);
        if (shortfall == 0) return (nextMoney, lien);
        LienState current = lien ?? new LienState(new Credits(72_000), null, []);
        Credits nextPrincipal = current.Principal + new Credits(shortfall);
        LienPaymentState entry = new(time, LienDisposition.Deferred, new Credits(shortfall), nextPrincipal, note);
        return (nextMoney, current with { Principal = nextPrincipal, PaymentHistory = [.. current.PaymentHistory, entry] });
    }

    private static IReadOnlyList<CrewMemberState> AddFatigueAndMemory(IReadOnlyList<CrewMemberState> crew, string id, int fatigue, int loyalty, GameTime time, string kind, string memory) =>
        TurnaroundCommands.UpdateCrew(crew, id, member => TurnaroundCommands.AddMemory(
            member with { Fatigue = Math.Clamp(member.Fatigue + fatigue, 0, 100), Loyalty = Math.Clamp(member.Loyalty + loyalty, 0, 100) },
            time, kind, memory, loyalty));

    private static IReadOnlyList<FactionStandingState> AdjustFaction(IReadOnlyList<FactionStandingState> standings, string factionId, int delta)
    {
        if (delta == 0) return standings;
        bool exists = standings.Any(item => item.FactionId == factionId);
        return exists
            ? standings.Select(item => item.FactionId == factionId ? item with { Standing = checked(item.Standing + delta) } : item).ToArray()
            : [.. standings, new FactionStandingState(factionId, delta)];
    }

    private static bool HasAvailableCrew(GameState state, string id) => state.Crew.Any(item => item.Id.Value == id && item.Available);
    private static bool HasSealedContractCargo(GameState state) => state.Cargo.Any(item => item.IsContractCargo && item.Sealed && item.Quantity >= state.Contract.Quantity);
    private static CommandResult<GameState> Success(GameState state) => CommandResult<GameState>.Success(state with { CommandSequence = checked(state.CommandSequence + 1) });
    private static CommandResult<GameState> Fail(string code, string message) => CommandResult<GameState>.Failure(code, message);
    private static CommandResult<GameState> OptionUnavailable(string message) => Fail(CommandErrorCodes.EncounterOptionUnavailable, message);
}
