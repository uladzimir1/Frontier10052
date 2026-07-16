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
        if (state.Ship.PinchReserve < route.PinchCost)
            return Fail(CommandErrorCodes.InsufficientPinchReserve, $"The route requires {route.PinchCost} pinch points; Wayfarer has {state.Ship.PinchReserve}.");

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

        if (committedRoute.UsesCheckpoints)
        {
            RouteCheckpointState? undock = committedRoute.AllCheckpoints.FirstOrDefault(item => item.Kind == RouteCheckpointKind.Undock && item.Status == CheckpointResolutionStatus.Pending);
            if (undock is null || undock.ScheduledHour != 2)
                return Fail(CommandErrorCodes.CheckpointUnavailable, "The Sirius route is missing its T+2 undock checkpoint.");
            if (state.Ship.FuelPercent < 4)
                return Fail(CommandErrorCodes.DepartureRequirementsNotMet, "Cinematic undock requires four local-fuel points.");

            GameTime resolvedAt = state.Time.AddHours(undock.ScheduledHour);
            IReadOnlyList<RouteCheckpointState> checkpoints = committedRoute.AllCheckpoints.Select(item => item.Id == undock.Id
                ? item with { Status = CheckpointResolutionStatus.Resolved, ResolvedAt = resolvedAt, Outcome = "Wayfarer cleared the berth and committed four local-fuel points." }
                : item).ToArray();
            return Success(state with
            {
                Time = resolvedAt,
                Ship = state.Ship with { FuelPercent = state.Ship.FuelPercent - 4 },
                StationVisits = visits,
                Journey = journey with
                {
                    Phase = JourneyPhase.InTransit,
                    DockedStationId = null,
                    Route = committedRoute with { ElapsedBaselineHours = undock.ScheduledHour, Checkpoints = checkpoints },
                    Encounter = null,
                    DestinationManifest = null,
                    LastOutcome = "Cinematic undock complete. Wayfarer is climbing toward the gravity boundary; pinch and drive wear remain uncommitted.",
                },
            });
        }

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
        if (route.UsesCheckpoints)
            return Fail(CommandErrorCodes.CheckpointUnavailable, "Sirius travel advances only by resolving its next ordered checkpoint.");

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

    public static CommandResult<GameState> ResolveNextCheckpoint(GameState state, CheckpointResponse? response)
    {
        JourneyState? journey = state.Journey;
        RouteTravelState? route = journey?.Route;
        if (journey?.Phase != JourneyPhase.InTransit || route is null || !route.UsesCheckpoints)
            return Fail(CommandErrorCodes.CheckpointUnavailable, "There is no ordered Sirius checkpoint ready on the active route.");

        RouteCheckpointState? checkpoint = route.AllCheckpoints.FirstOrDefault(item => item.Status == CheckpointResolutionStatus.Pending);
        if (checkpoint is null)
            return Fail(CommandErrorCodes.CheckpointUnavailable, "Every checkpoint on the active Sirius route is already resolved.");
        if (checkpoint.ScheduledHour < route.ElapsedBaselineHours)
            return Fail(CommandErrorCodes.CheckpointUnavailable, "The next checkpoint precedes the persisted route clock.");

        bool requiresResponse = checkpoint.Kind is RouteCheckpointKind.DelayedMessage or RouteCheckpointKind.LatticeDrift;
        if (requiresResponse && response is null)
            return Fail(CommandErrorCodes.CheckpointResponseUnavailable, "This checkpoint pauses simulation time until an available response is chosen.");
        if (!requiresResponse && response is not null)
            return Fail(CommandErrorCodes.CheckpointResponseUnavailable, "This automatic checkpoint does not accept a response choice.");

        int delay = 0;
        int fuel = 0;
        int pinch = 0;
        int wear = 0;
        int legalExposure = 0;
        IReadOnlyList<CrewMemberState> crew = state.Crew;
        IReadOnlyList<InformationItemState> information = state.AllInformationCargo;
        ContractState contract = state.Contract;
        IReadOnlyList<ContractState> contracts = state.AllContracts;
        IReadOnlyList<ContractTransformationState> transformations = state.AllContractTransformations;
        string outcome;

        switch (checkpoint.Kind)
        {
            case RouteCheckpointKind.GravityBoundary:
                fuel = route.FuelCostPercent - 4;
                pinch = route.PinchCost;
                wear = route.BaseDriveWearPercent;
                if (state.Ship.FuelPercent < fuel)
                    return Fail(CommandErrorCodes.DepartureRequirementsNotMet, $"Gravity-boundary departure requires {fuel} remaining local-fuel points; Wayfarer has {state.Ship.FuelPercent}.");
                if (state.Ship.PinchReserve < pinch)
                    return Fail(CommandErrorCodes.InsufficientPinchReserve, $"Gravity-boundary departure requires {pinch} pinch points; Wayfarer has {state.Ship.PinchReserve}.");
                if (state.Ship.DriveWearPercent + wear > 100)
                    return Fail(CommandErrorCodes.CheckpointResponseUnavailable, $"Gravity-boundary departure adds {wear} drive wear, exceeding Wayfarer's 100% mechanical limit.");
                outcome = $"Wayfarer crossed the gravity boundary and committed {fuel} fuel, {pinch} pinch, and {wear} drive wear.";
                break;
            case RouteCheckpointKind.DelayedMessage:
                InformationItemState? dossier = FindContractInformation(state);
                if (dossier is null) return Fail(CommandErrorCodes.InformationMissing, "The delayed warning cannot be resolved because the industrial forecast dossier is missing.");
                switch (response)
                {
                    case CheckpointResponse.PreserveSeal:
                        crew = AddFatigueAndMemory(crew, "noor-okafor", 0, 3, state.Time, "information-custody", "Noor preserved the SCC forecast seal against the delayed labor warning.");
                        crew = AddFatigueAndMemory(crew, "tomas-vale", 0, -3, state.Time, "labor-warning", "Tomas opposed preserving the SCC forecast seal without corroboration.");
                        outcome = "Command preserved the corporate seal. Noor gained 3 loyalty; Tomas lost 3.";
                        break;
                    case CheckpointResponse.CorroborateWarning:
                        delay = 6;
                        legalExposure = 1;
                        crew = AddFatigueAndMemory(crew, "ilya-sato", 4, 2, state.Time.AddHours(delay), "information-corroboration", "Ilya spent six hours validating the labor warning against the forecast model.");
                        crew = AddFatigueAndMemory(crew, "noor-okafor", 2, 1, state.Time.AddHours(delay), "information-custody", "Noor preserved a documented evidence chain during corroboration.");
                        dossier = dossier with
                        {
                            Disposition = InformationDisposition.Corroborated,
                            ConfidencePercent = 95,
                            Provenance = [.. dossier.Provenance, new InformationProvenanceState("Wayfarer engineering and custody review", state.Time.AddHours(delay), 95, "Labor warning corroborated without public disclosure.")],
                        };
                        information = ReplaceInformation(information, dossier);
                        outcome = "Ilya and Noor corroborated the warning in six hours. Confidence is 95%; legal exposure increased by 1.";
                        break;
                    case CheckpointResponse.LeakWarning:
                        delay = 2;
                        legalExposure = 4;
                        crew = AddFatigueAndMemory(crew, "tomas-vale", 0, 4, state.Time.AddHours(delay), "labor-disclosure", "Tomas supported leaking the labor warning before Sirius arrival.");
                        crew = AddFatigueAndMemory(crew, "noor-okafor", 0, -3, state.Time.AddHours(delay), "custody-breach", "Noor opposed breaking the corporate information seal.");
                        dossier = dossier with
                        {
                            Disposition = InformationDisposition.Disclosed,
                            Provenance = [.. dossier.Provenance, new InformationProvenanceState("Distributed labor relay", state.Time.AddHours(delay), dossier.ConfidencePercent, "Corporate forecast and warning disclosed before arrival.")],
                        };
                        information = ReplaceInformation(information, dossier);
                        ContractTransformationState transformation = new(contract.Id, state.Time.AddHours(delay), "sealed industrial forecast delivery", "labor evidence disclosure", "Command leaked the delayed labor warning and transformed the SCC contract case.");
                        contract = contract with { Status = ContractStatus.Transformed, Transformation = transformation.ToCase, Outcome = transformation.Reason };
                        contracts = contracts.Select(item => item.Id == contract.Id ? contract : item).ToArray();
                        transformations = [.. transformations, transformation];
                        outcome = "The forecast was leaked and the contract transformed into a labor evidence-disclosure case. Legal exposure increased by 4.";
                        break;
                    default:
                        return Fail(CommandErrorCodes.CheckpointResponseUnavailable, "Choose preserve seal, corroborate, or leak for the delayed labor warning.");
                }
                break;
            case RouteCheckpointKind.LatticeDrift:
                switch (response)
                {
                    case CheckpointResponse.IlyaRecalibration:
                        if (!HasAvailableCrew(state, "ilya-sato")) return Fail(CommandErrorCodes.CheckpointResponseUnavailable, "Ilya recalibration requires Ilya to be available.");
                        delay = state.Turnaround?.RepairService switch { RepairService.Certified => 3, RepairService.IlyaFieldService => 5, _ => 8 };
                        crew = AddFatigueAndMemory(crew, "ilya-sato", 4, 1, state.Time.AddHours(delay), "lattice-recalibration", $"Ilya recovered the pinch lattice in {delay} hours using the persisted service provenance.");
                        outcome = $"Ilya recalibrated the lattice in {delay} hours; the prior station repair choice determined the duration.";
                        break;
                    case CheckpointResponse.MaraPinchCorrection:
                        if (!HasAvailableCrew(state, "mara-venn")) return Fail(CommandErrorCodes.CheckpointResponseUnavailable, "Mara's correction requires Mara to be available.");
                        if (state.Ship.PinchReserve < 6)
                            return Fail(CommandErrorCodes.InsufficientPinchReserve, $"Mara's correction requires 6 extra pinch points; Wayfarer has {state.Ship.PinchReserve}.");
                        if (state.Ship.DriveWearPercent + 3 > 100)
                            return Fail(CommandErrorCodes.CheckpointResponseUnavailable, $"Mara's correction adds 3 drive wear; only {100 - state.Ship.DriveWearPercent} points of margin remain.");
                        delay = 1;
                        pinch = 6;
                        wear = 3;
                        crew = AddFatigueAndMemory(crew, "mara-venn", 4, 1, state.Time.AddHours(delay), "lattice-correction", "Mara spent six reserve pinch points to force the lattice back onto the Sirius corridor.");
                        outcome = "Mara spent 6 extra pinch, added 3 drive wear, 4 fatigue, 1 loyalty, and 1 hour.";
                        break;
                    default:
                        return Fail(CommandErrorCodes.CheckpointResponseUnavailable, "Choose Ilya recalibration or Mara's reserve-pinch correction.");
                }
                break;
            case RouteCheckpointKind.Approach:
                outcome = "Sirius Meridian Exchange assigned Wayfarer an approach corridor; customs remains a separate station command.";
                break;
            default:
                return Fail(CommandErrorCodes.CheckpointUnavailable, "The undock checkpoint is resolved only by beginning the authorized voyage.");
        }

        int baselineAdvance = checkpoint.ScheduledHour - route.ElapsedBaselineHours;
        GameTime resolvedAt = state.Time.AddHours(baselineAdvance + delay);
        int nextWear = state.Ship.DriveWearPercent + wear;
        IReadOnlyList<RouteCheckpointState> checkpoints = route.AllCheckpoints.Select(item => item.Id == checkpoint.Id
            ? item with { Status = CheckpointResolutionStatus.Resolved, ResolvedAt = resolvedAt, Response = response, Outcome = outcome }
            : item).ToArray();
        JourneyPhase phase = checkpoint.Kind == RouteCheckpointKind.Approach ? JourneyPhase.Approach : JourneyPhase.InTransit;
        return Success(state with
        {
            Time = resolvedAt,
            Ship = state.Ship with
            {
                FuelPercent = state.Ship.FuelPercent - fuel,
                PinchReserve = state.Ship.PinchReserve - pinch,
                DriveWearPercent = nextWear,
            },
            Maintenance = state.Maintenance is null ? null : state.Maintenance with { DriveConditionPercent = 100 - nextWear },
            Crew = crew,
            LegalExposure = checked(state.LegalExposure + legalExposure),
            InformationCargo = information,
            Contract = contract,
            Contracts = contracts,
            ContractTransformations = transformations,
            Journey = journey with
            {
                Phase = phase,
                Route = route with { ElapsedBaselineHours = checkpoint.ScheduledHour, DelayHours = checked(route.DelayHours + delay), Checkpoints = checkpoints },
                LastOutcome = outcome,
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
        bool siriusArrival = route.UsesCheckpoints && route.DestinationStationId.Value == "sirius-meridian-exchange";
        return Success(state with
        {
            Ship = state.Ship with { StationId = route.DestinationStationId },
            JourneyHistory = histories,
            StationVisits = [.. state.AllStationVisits, new StationVisitState(route.DestinationStationId, state.Time, null)],
            SiriusCustoms = siriusArrival ? new SiriusCustomsState(route.OriginStationId, false, null, 0, "Sirius customs clearance is pending.") : state.SiriusCustoms,
            Journey = journey with
            {
                Phase = siriusArrival ? JourneyPhase.CustomsPending : JourneyPhase.Docked,
                DockedStationId = route.DestinationStationId,
                LastOutcome = siriusArrival
                    ? "Wayfarer is secured at Sirius Meridian Exchange. Information settlement is locked behind a separate customs command."
                    : $"Wayfarer is secured at {route.DestinationStationId.Value}.",
            },
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
        (Credits money, LienState? lien, DebtLedgerEntryState? debtEntry) = valid
            ? (state.Money + state.Contract.Reward, state.Lien, null)
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
        IReadOnlyList<ContractState> offers = turnaroundOffers ?? [];
        if (offers.Count > 0)
        {
            contracts = [.. contracts, .. offers.Where(offer => contracts.All(item => item.Id != offer.Id))];
            bool siriusPreparation = journey.VoyageNumber == 2;
            GameTime expiresAt = offers.Select(item => item.AcceptanceExpiresAt).Where(item => item is not null).Select(item => item!.Value).DefaultIfEmpty(state.Time.AddHours(siriusPreparation ? 36 : 24)).Min();
            turnaround = new TurnaroundState(
                state.Ship.StationId,
                state.Time,
                expiresAt,
                null,
                null,
                null,
                null,
                false,
                siriusPreparation ? "Second-contract settlement opened the 36-hour Sirius intelligence offer." : "First-contract settlement opened a 24-hour Mars offer window.",
                siriusPreparation ? StationOperationsMode.SiriusPreparation : StationOperationsMode.MarsTurnaround);
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
            DebtLedger = debtEntry is null ? state.DebtLedger : [.. state.AllDebtLedger, debtEntry],
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

    public static CommandResult<GameState> ClearSiriusCustoms(GameState state)
    {
        JourneyState? journey = state.Journey;
        if (state.SiriusCustoms?.Cleared == true)
            return Fail(CommandErrorCodes.CustomsAlreadyCleared, "Sirius customs has already cleared this arrival.");
        if (journey?.Phase != JourneyPhase.CustomsPending || state.Ship.StationId.Value != "sirius-meridian-exchange" || state.SiriusCustoms is null)
            return Fail(CommandErrorCodes.CustomsUnavailable, "Sirius customs clearance is available only after the interstellar approach is docked.");

        int originReduction = state.SiriusCustoms.OriginStationId.Value == "pluto-gateway" ? 3 : 0;
        int delay = Math.Max(1, 6 - originReduction + Math.Min(6, state.LegalExposure));
        int noorFatigue = (delay + 1) / 2;
        GameTime clearedAt = state.Time.AddHours(delay);
        IReadOnlyList<CrewMemberState> crew = AddFatigueAndMemory(
            state.Crew,
            "noor-okafor",
            noorFatigue,
            0,
            clearedAt,
            "sirius-customs",
            $"Noor carried the information manifest through {delay} hours of Sirius customs review.");
        string outcome = $"Sirius customs cleared Wayfarer in {delay} hours; Noor gained {noorFatigue} fatigue.";
        return Success(state with
        {
            Time = clearedAt,
            Crew = crew,
            SiriusCustoms = state.SiriusCustoms with { Cleared = true, ClearedAt = clearedAt, DelayHours = delay, Outcome = outcome },
            Journey = journey with { Phase = JourneyPhase.Docked, LastOutcome = outcome },
        });
    }

    public static CommandResult<GameState> SettleInformationContract(
        GameState state,
        StationEventId aftermathEventId,
        CommodityId actuatorId,
        IReadOnlyList<ContractLeadId> leadIds)
    {
        if (state.InformationSettlement is not null)
            return Fail(CommandErrorCodes.SettlementAlreadyComplete, "The Sirius information contract is already settled.");
        JourneyState? journey = state.Journey;
        if (journey?.Phase != JourneyPhase.Docked || state.Ship.StationId.Value != "sirius-meridian-exchange" || state.SiriusCustoms?.Cleared != true)
            return Fail(CommandErrorCodes.SettlementUnavailable, "Clear Sirius customs before settling the information contract.");
        if (state.Contract.Objective?.Kind != ContractObjectiveKind.Information || state.Contract.Objective.InformationId is not InformationId informationId)
            return Fail(CommandErrorCodes.SettlementUnavailable, "The active contract is not an information-delivery case.");
        InformationItemState? dossier = state.AllInformationCargo.SingleOrDefault(item => item.Id == informationId);
        if (dossier is null) return Fail(CommandErrorCodes.InformationMissing, "The contracted industrial forecast is missing from Wayfarer's information cargo.");
        if (state.Contract.Status is not (ContractStatus.Accepted or ContractStatus.Transformed))
            return Fail(CommandErrorCodes.SettlementUnavailable, "The Sirius information case is not in a settleable state.");

        InformationDisposition disposition = dossier.Disposition;
        bool onTime = state.Time.CompareTo(state.Contract.Deadline) <= 0;
        long paymentValue;
        long claimValue;
        int compactDelta;
        int laborDelta;
        int trustDelta;
        ContractStatus status;
        string outcome;

        if (disposition == InformationDisposition.Disclosed)
        {
            paymentValue = onTime ? 8_000 : 4_000;
            claimValue = onTime ? 10_000 : 0;
            compactDelta = -6;
            laborDelta = 5 + (state.SiriusCustoms.OriginStationId.Value == "ceres-freehold-anchorage" ? 2 : 0);
            trustDelta = 0;
            status = ContractStatus.Completed;
            outcome = onTime
                ? "The evidence-disclosure case paid 8,000 credits in labor support after the 10,000-credit SCC claim."
                : "Late disclosure received only 4,000 credits in labor support.";
        }
        else if (onTime && disposition == InformationDisposition.Corroborated)
        {
            paymentValue = 20_000;
            claimValue = 0;
            compactDelta = 2;
            laborDelta = 4;
            trustDelta = 1;
            status = ContractStatus.Completed;
            outcome = "Sirius accepted the corroborated forecast on time and released 20,000 credits.";
        }
        else if (onTime)
        {
            paymentValue = 26_000;
            claimValue = 0;
            compactDelta = 6;
            laborDelta = 0;
            trustDelta = 3;
            status = ContractStatus.Completed;
            outcome = "Sirius accepted the sealed forecast on time and released the full 26,000-credit reward.";
        }
        else
        {
            paymentValue = 0;
            claimValue = 10_000;
            compactDelta = -4;
            laborDelta = 0;
            trustDelta = -2;
            status = ContractStatus.Failed;
            outcome = "Late information delivery failed and triggered the 10,000-credit SCC claim.";
        }

        Credits payment = new(paymentValue);
        Credits claim = new(claimValue);
        (Credits afterClaim, LienState? lien, DebtLedgerEntryState? debtEntry) = claimValue == 0
            ? (state.Money, state.Lien, null)
            : ApplyCharge(state.Money, state.Lien, claim, state.Time, "Sirius information claim capitalized because available credits were insufficient.");
        Credits money = afterClaim + payment;
        ContractState settledContract = state.Contract with { Status = status, SettledAt = state.Time, Outcome = outcome };
        InformationSettlementState settlement = new(
            state.Contract.Id,
            dossier.Id,
            disposition,
            onTime,
            state.Time,
            payment,
            claim,
            debtEntry?.Capitalized ?? new Credits(0),
            outcome);
        IReadOnlyList<FactionStandingState> standings = AdjustFaction(state.AllFactionStandings, FactionIds.SiriusCorporateCompact, compactDelta);
        standings = AdjustFaction(standings, FactionIds.SiriusLabor, laborDelta);
        IReadOnlyList<JourneyHistoryState> histories = state.AllJourneyHistory.Select(item => item.VoyageNumber == journey.VoyageNumber
            ? item with { DeliveredAt = state.Time, InformationSettlement = settlement, Outcome = outcome }
            : item).ToArray();

        GameState settled = state with
        {
            Money = money,
            Lien = lien,
            DebtLedger = debtEntry is null ? state.DebtLedger : [.. state.AllDebtLedger, debtEntry],
            CommercialTrust = checked(state.CommercialTrust + trustDelta),
            FactionStandings = standings,
            Contract = settledContract,
            Contracts = state.AllContracts.Select(item => item.Id == settledContract.Id ? settledContract : item).ToArray(),
            InformationCargo = ReplaceInformation(state.AllInformationCargo, dossier with { Disposition = InformationDisposition.Delivered }),
            InformationSettlement = settlement,
            JourneyHistory = histories,
            Turnaround = null,
            Journey = journey with { Phase = JourneyPhase.Delivered, LastOutcome = outcome },
        };
        return Success(SiriusAftermathCommands.CreateAftermath(settled, aftermathEventId, actuatorId, leadIds));
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

    private static (Credits Money, LienState? Lien, DebtLedgerEntryState? DebtEntry) ApplyCharge(Credits money, LienState? lien, Credits charge, GameTime time, string note)
    {
        long paid = Math.Min(money.Value, charge.Value);
        long shortfall = charge.Value - paid;
        Credits nextMoney = new(money.Value - paid);
        if (shortfall == 0) return (nextMoney, lien, null);
        LienState current = lien ?? new LienState(new Credits(72_000), null, []);
        Credits nextPrincipal = current.Principal + new Credits(shortfall);
        LienPaymentState entry = new(time, LienDisposition.Deferred, new Credits(shortfall), nextPrincipal, note);
        DebtLedgerEntryState debt = new(time, "capitalized-contract-claim", charge, new Credits(paid), new Credits(shortfall), nextPrincipal, note);
        return (nextMoney, current with { Principal = nextPrincipal, PaymentHistory = [.. current.PaymentHistory, entry] }, debt);
    }

    private static IReadOnlyList<CrewMemberState> AddFatigueAndMemory(IReadOnlyList<CrewMemberState> crew, string id, int fatigue, int loyalty, GameTime time, string kind, string memory) =>
        TurnaroundCommands.UpdateCrew(crew, id, member => TurnaroundCommands.AddMemory(
            member with { Fatigue = Math.Clamp(member.Fatigue + fatigue, 0, 100), Loyalty = Math.Clamp(member.Loyalty + loyalty, 0, 100) },
            time, kind, memory, loyalty));

    private static InformationItemState? FindContractInformation(GameState state) => state.Contract.Objective?.InformationId is InformationId id
        ? state.AllInformationCargo.SingleOrDefault(item => item.Id == id)
        : null;

    private static IReadOnlyList<InformationItemState> ReplaceInformation(IReadOnlyList<InformationItemState> information, InformationItemState replacement) =>
        information.Select(item => item.Id == replacement.Id ? replacement : item).ToArray();

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
