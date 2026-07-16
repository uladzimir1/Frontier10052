using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Journey;

internal static class JourneySnapshotMapper
{
    public static JourneySnapshot Map(GameState state, VerticalSliceContentPack pack)
    {
        JourneyState journey = state.Journey ?? throw new InvalidOperationException("Schema-4 state requires a journey.");
        RouteDefinition definition = ResolveRoute(state, journey, pack);
        RouteTravelState? route = journey.Route;
        StationDefinition origin = pack.Stations.Single(item => item.Id == definition.OriginStationId);
        StationDefinition destination = pack.Stations.Single(item => item.Id == definition.DestinationStationId);
        int elapsed = route?.ElapsedBaselineHours ?? 0;
        int delay = route?.DelayHours ?? 0;
        int duration = route?.BaselineDurationHours ?? definition.DurationHours;
        int fuelCost = route?.FuelCostPercent ?? definition.FuelCostPercent;
        int progress = duration == 0 ? 0 : Math.Clamp((int)Math.Round(elapsed * 100m / duration), 0, 100);
        string estimated = FormatTime(route?.EstimatedArrival ?? state.Time.AddHours(duration));
        string actual = route is null ? estimated : FormatTime(route.EstimatedArrival.AddHours(route.DelayHours));

        IReadOnlyList<JourneyCargoPresentation> cargo = state.Cargo.Select(item =>
        {
            CommodityDefinition commodity = pack.Commodities.Single(def => def.Id == item.CommodityId);
            return new JourneyCargoPresentation(item.CommodityId.Value, pack.Text(commodity.NameKey), item.Quantity.Value, item.IsContractCargo, item.Sealed);
        }).ToArray();

        IReadOnlyList<DestinationBidPresentation> bids = journey.DestinationMarket.Listings.Select(listing =>
        {
            CommodityDefinition commodity = pack.Commodities.Single(item => item.Id == listing.CommodityId);
            int owned = state.Cargo.Where(item => item.CommodityId == listing.CommodityId && !item.IsContractCargo).Sum(item => item.Quantity.Value);
            bool atMarket = journey.DestinationMarket.StationId == state.Ship.StationId;
            return new DestinationBidPresentation(listing.CommodityId.Value, pack.Text(commodity.NameKey), listing.BidPrice.Value, listing.RealizedMargin, owned,
                new JourneyAction($"sell-{listing.CommodityId.Value}", "Sell cargo", $"Station bid {listing.BidPrice.Value:N0} credits per tonne", atMarket && journey.Phase is (JourneyPhase.Docked or JourneyPhase.Turnaround) && owned > 0,
                    !atMarket ? "This bid belongs to a different station." : owned > 0 ? "Choose a quantity up to the owned manifest." : "No speculative tonnes of this commodity are aboard."),
                atMarket);
        }).ToArray();

        IReadOnlyList<SalePresentation> sales = journey.Sales.Select(sale =>
        {
            CommodityDefinition commodity = pack.Commodities.Single(item => item.Id == sale.CommodityId);
            return new SalePresentation(pack.Text(commodity.NameKey), sale.Quantity.Value, sale.UnitPrice.Value, sale.Proceeds.Value);
        }).ToArray();

        bool canBegin = journey.Phase == JourneyPhase.DepartureAuthorized;
        RouteCheckpointState? nextCheckpoint = route?.AllCheckpoints.FirstOrDefault(item => item.Status == CheckpointResolutionStatus.Pending);
        bool checkpointChoice = nextCheckpoint?.Kind is RouteCheckpointKind.DelayedMessage or RouteCheckpointKind.LatticeDrift;
        bool canAdvance = journey.Phase == JourneyPhase.InTransit && (route?.UsesCheckpoints != true || !checkpointChoice);
        bool canDock = journey.Phase == JourneyPhase.Approach;
        bool canDeliver = journey.Phase == JourneyPhase.Docked && state.Contract.Status == ContractStatus.Accepted;
        string callsign = state.Commander.Callsign;
        string initials = string.Concat(callsign.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
        if (initials.Length == 0) initials = "NC";

        ContractDefinition contractDefinition = pack.Contracts.Single(item => item.Id == state.Contract.Id);
        CommodityDefinition contractCommodity = pack.Commodities.Single(item => item.Id == state.Contract.CommodityId);
        IReadOnlyList<JourneyCrewPresentation> crew = state.Crew.Select(member =>
        {
            CrewDefinition crewDefinition = pack.Crew.Single(item => item.Id == member.Id);
            CrewMemoryState? memory = member.Memories?.LastOrDefault();
            return new JourneyCrewPresentation(member.Id.Value, pack.Text(crewDefinition.NameKey), pack.Text(crewDefinition.RoleKey), member.Loyalty, member.Fatigue, memory?.Summary ?? "No persistent journey memory recorded yet.");
        }).ToArray();
        IReadOnlyList<JourneyFactionPresentation> factions = state.AllFactionStandings.Select(item =>
            new JourneyFactionPresentation(item.FactionId, FactionName(item.FactionId), item.Standing)).ToArray();
        DestinationManifestPresentation? destinationManifest = journey.DestinationManifest is null ? null : new DestinationManifestPresentation(
            pack.Text(pack.Stations.Single(item => item.Id == journey.DestinationManifest.StationId).NameKey),
            pack.Text(contractDefinition.TitleKey),
            journey.DestinationManifest.SettlementStatus.ToString(),
            FormatTime(journey.DestinationManifest.PresentedAt),
            journey.DestinationManifest.CreditsAfterSettlement.Value,
            journey.DestinationManifest.LegalExposure,
            LocalizeOutcome(journey.DestinationManifest.Outcome, pack));

        return new JourneySnapshot(
            state.GameId.Value,
            state.Seed,
            state.CommandSequence,
            state.SchemaVersion,
            pack.Version,
            journey.Phase,
            ContinuePath(journey.Phase),
            FormatTime(state.Time),
            Location(journey.Phase, pack, origin, destination),
            new CommanderJourneyPresentation($"Cmdr. {callsign}", initials),
            new ShipJourneyPresentation(pack.Text(pack.Ship.NameKey), pack.Text(pack.Ship.HullKey), state.CargoLoaded.Value, state.Ship.CargoCapacity.Value, state.Ship.FuelPercent, state.Ship.DriveWearPercent, state.Ship.PinchReserve),
            new RoutePresentation(definition.Id.Value, pack.Text(definition.NameKey), pack.Text(origin.NameKey), pack.Text(destination.NameKey), duration, fuelCost, definition.DriveWearPercent, route?.EncounterAtHour ?? definition.EncounterAtHour, elapsed, delay, estimated, actual, progress, pack.Text(definition.ProfileKey), route?.PinchCost ?? definition.PinchCost, route?.UsesCheckpoints ?? definition.Checkpoints is { Count: > 0 }),
            MapEncounter(state, definition, pack),
            cargo,
            bids,
            sales,
            new ContractJourneyPresentation(pack.Text(contractDefinition.TitleKey), state.Contract.Status.ToString(), state.Contract.Quantity.Value, pack.Text(contractCommodity.NameKey), state.Contract.Reward.Value, state.Contract.FailurePenalty.Value, checked((int)(state.Contract.Deadline.HoursSinceStart - state.Time.HoursSinceStart)), FormatTime(state.Contract.Deadline)),
            new FinanceJourneyPresentation(state.Money.Value, state.CommercialTrust, journey.Sales.Sum(item => item.Proceeds.Value), state.Contract.Status == ContractStatus.Accepted ? state.Contract.Reward.Value : 0),
            LocalizeOutcome(journey.LastOutcome, pack),
            new JourneyAction("begin", route?.UsesCheckpoints == true ? "Commit cinematic undock" : "Commit undock and burn", route?.UsesCheckpoints == true ? "T+2 h · consumes 4 local-fuel points only" : $"Consumes {fuelCost}% fuel and adds {definition.DriveWearPercent}% drive wear", canBegin, canBegin ? $"The authorized {pack.Text(definition.NameKey)} manifest is ready." : "Undock is available only from an authorized station departure."),
            new JourneyAction("advance", nextCheckpoint is null ? journey.Encounter is null ? "Advance to route contact" : $"Advance to {pack.Text(destination.NameKey)} approach" : CheckpointLabel(nextCheckpoint.Kind), "Advances authoritative simulation time; animation is presentational only", canAdvance, checkpointChoice ? "Choose one checkpoint response; simulation time is paused." : journey.Phase == JourneyPhase.EncounterPending ? "Resolve the active encounter first." : canAdvance ? "The next route milestone is ready." : "Travel is not in an advanceable phase."),
            new JourneyAction("dock", $"Dock at {pack.Text(destination.NameKey)}", $"Commits {pack.Text(destination.NameKey)} as Wayfarer's authoritative station", canDock, canDock ? "Approach corridor and berth are confirmed." : "Complete the route and encounter before docking."),
            new JourneyAction("deliver", "Present locked contract cargo", $"Pays {state.Contract.Reward.Value:N0} credits if destination, seals, quantity, and deadline validate", canDeliver, canDeliver ? "Destination custody is ready to validate the active manifest." : state.Contract.Status is ContractStatus.Completed or ContractStatus.Failed ? "The contract is already settled." : "Dock at the active destination with the accepted sealed shipment."),
            journey.VoyageNumber,
            crew,
            factions,
            state.Lien?.Principal.Value ?? 72_000,
            state.LegalExposure,
            destinationManifest,
            MapCheckpoints(state, definition, pack),
            MapInformation(state),
            state.SiriusCustoms is null ? null : new SiriusCustomsPresentation(state.SiriusCustoms.Cleared, state.SiriusCustoms.DelayHours, pack.Text(pack.Stations.Single(item => item.Id == state.SiriusCustoms.OriginStationId).NameKey), state.SiriusCustoms.Outcome),
            new JourneyAction("sirius-customs", "Clear Sirius customs", "Authoritative delay from origin and legal exposure · Noor fatigue is half rounded up", journey.Phase == JourneyPhase.CustomsPending && state.SiriusCustoms?.Cleared != true, state.SiriusCustoms?.Cleared == true ? "Customs clearance is already committed." : journey.Phase == JourneyPhase.CustomsPending ? "Wayfarer is docked in the customs concourse." : "Customs opens after the Sirius approach is docked."),
            new JourneyAction("settle-information", "Settle information contract", "Outcome depends on disposition and deadline equality", journey.Phase == JourneyPhase.Docked && state.SiriusCustoms?.Cleared == true && state.InformationSettlement is null, state.InformationSettlement is not null ? "The Sirius information case is already settled." : state.SiriusCustoms?.Cleared == true ? "Information custody is ready for final settlement." : "Clear customs before settlement."),
            state.InformationSettlement is null ? null : new InformationSettlementPresentation(state.InformationSettlement.Disposition.ToString(), state.InformationSettlement.OnTime, state.InformationSettlement.Payment.Value, state.InformationSettlement.Claim.Value, state.InformationSettlement.CapitalizedClaim.Value, state.InformationSettlement.Outcome),
            CinematicPresentationMapper.MapCurrent(state, journey, definition, pack));
    }

    private static RouteDefinition ResolveRoute(GameState state, JourneyState journey, VerticalSliceContentPack pack)
    {
        RouteId? routeId = journey.Route?.Id ?? state.DepartureManifest?.RouteId;
        if (routeId is not null) return pack.Routes.Single(item => item.Id == routeId.Value);
        return pack.Routes.Single(item => item.OriginStationId == state.Contract.OriginStationId && item.DestinationStationId == state.Contract.DestinationStationId);
    }

    private static EncounterPresentation? MapEncounter(GameState state, RouteDefinition route, VerticalSliceContentPack pack)
    {
        EncounterState? encounter = state.Journey?.Encounter;
        if (encounter is null) return null;
        EncounterDefinition definition = pack.Encounters.Single(item => item.Id == encounter.Id);
        IReadOnlyList<JourneyAction> responses = encounter.Id.Value switch
        {
            "sol-transit-inspection" =>
            [
                Response(EncounterResponse.InspectionStandardCompliance, "Submit to standard inspection", "+3 h · +1 commercial trust", true, "The legal manifest can be presented without prerequisites."),
                Response(EncounterResponse.InspectionMedicalPriority, "Invoke medical-priority corridor", "+1 h · −2% fuel", HasCrew(state, "noor-okafor") && HasContractCargo(state), "Requires Noor and intact sealed medical cargo."),
            ],
            "drive-coolant-failure" =>
            [
                Response(EncounterResponse.MechanicalFieldRepair, "Authorize Ilya's field repair", state.EngineerAssignment == EngineerAssignment.InspectDrive ? "+2 h · +1% wear" : "+5 h · +3% wear", HasCrew(state, "ilya-sato"), "Ilya must be available; preflight inspection improves the outcome."),
                Response(EncounterResponse.MechanicalReducedBurn, "Reduce the burn", "+7 h · no added wear", HasCrew(state, "mara-venn"), "Mara carries the weakened drive on a slower profile."),
            ],
            "pluto-migration-medical-emergency" =>
            [
                Response(EncounterResponse.MigrationMedicalAssist, "Send Tomas to Pilgrim Seven", "+4 h · Tomas +6 fatigue · +2 trust/TCA", HasCrew(state, "tomas-vale"), "Requires Tomas; the response persists as a humanitarian crew memory."),
                Response(EncounterResponse.MigrationConserveSupplies, "Conserve Wayfarer's supplies", "Tomas −4 loyalty · −1 trust · −2 TCA", true, "The voyage continues, but the refusal is remembered."),
            ],
            "ceres-debris-coolant-breach" =>
            [
                Response(EncounterResponse.DebrisIlyaRepair, "Authorize Ilya's coolant repair", RepairConsequence(state), HasCrew(state, "ilya-sato"), "Ilya's duration and added wear depend on the persisted Mars service provenance."),
                Response(EncounterResponse.DebrisMaraEvasion, "Let Mara evade the debris fan", "−4% fuel · +4% wear · Mara +3 fatigue", HasCrew(state, "mara-venn") && state.Ship.FuelPercent >= 4 && state.Ship.DriveWearPercent <= 96, "Requires Mara, four fuel points, and four points of drive margin."),
            ],
            _ =>
            [
                Response(EncounterResponse.PiratePayDemand, "Pay the demand", "−2,500 cr · +1 h", state.Money.Value >= 2_500, "Requires 2,500 available credits."),
                Response(EncounterResponse.PirateDumpSpeculativeCargo, "Dump speculative cargo", "Lose open-market cargo · +2 h", state.Cargo.Any(item => !item.IsContractCargo), "Requires speculative cargo; contract cargo is never offered."),
                Response(EncounterResponse.PirateHardBurn, "Run for the patrol boundary", "−4% fuel · +3% wear", HasCrew(state, "mara-venn") && state.Ship.FuelPercent >= 4 && state.Ship.DriveWearPercent <= 97, "Requires Mara, fuel, and drive margin."),
            ],
        };
        return new EncounterPresentation(
            encounter.Id.Value,
            pack.Text(definition.TitleKey),
            pack.Text(definition.DetailKey),
            pack.Text(definition.SourceKey),
            encounter.Status.ToString(),
            LocalizeOutcome(encounter.Outcome, pack),
            responses,
            CinematicPresentationMapper.MapEncounter(state, route, encounter, pack));
    }

    private static IReadOnlyList<CheckpointPresentation> MapCheckpoints(GameState state, RouteDefinition route, VerticalSliceContentPack pack)
    {
        IReadOnlyList<RouteCheckpointState> checkpoints = state.Journey?.Route?.AllCheckpoints ?? [];
        RouteCheckpointState? next = checkpoints.FirstOrDefault(item => item.Status == CheckpointResolutionStatus.Pending);
        return checkpoints.Select(checkpoint =>
        {
            RouteCheckpointDefinition authored = route.Checkpoints!.Single(item => item.Id == checkpoint.Id);
            IReadOnlyList<JourneyAction> responses = checkpoint.Id == next?.Id ? CheckpointResponses(state, checkpoint.Kind) : [];
            return new CheckpointPresentation(
                checkpoint.Id.Value,
                checkpoint.Kind.ToString(),
                pack.Text(authored.TitleKey),
                pack.Text(authored.DetailKey),
                checkpoint.ScheduledHour,
                checkpoint.Status.ToString(),
                checkpoint.Outcome,
                responses,
                checkpoint.Id == next?.Id,
                CinematicPresentationMapper.MapCheckpoint(state, route, checkpoint, pack));
        }).ToArray();
    }

    private static IReadOnlyList<JourneyAction> CheckpointResponses(GameState state, RouteCheckpointKind kind) => kind switch
    {
        RouteCheckpointKind.DelayedMessage =>
        [
            CheckpointAction(CheckpointResponse.PreserveSeal, "Preserve the corporate seal", "Noor +3 loyalty · Tomas −3", FindInformation(state) is not null, "Keeps the forecast sealed for full corporate delivery."),
            CheckpointAction(CheckpointResponse.CorroborateWarning, "Corroborate the warning", "+6 h · confidence 95% · legal +1 · Ilya/Noor fatigue", FindInformation(state) is not null && HasCrew(state, "ilya-sato") && HasCrew(state, "noor-okafor"), "Requires the dossier, Ilya, and Noor to preserve a documented evidence chain."),
            CheckpointAction(CheckpointResponse.LeakWarning, "Leak the warning", "+2 h · legal +4 · disclosure transformation", FindInformation(state) is not null, "Transforms the contract into a labor evidence-disclosure case."),
        ],
        RouteCheckpointKind.LatticeDrift =>
        [
            CheckpointAction(CheckpointResponse.IlyaRecalibration, "Ilya recalibrates the lattice", LatticeConsequence(state), HasCrew(state, "ilya-sato"), "Duration is 3/5/8 hours for certified, Ilya field, or deferred preparation."),
            CheckpointAction(CheckpointResponse.MaraPinchCorrection, "Mara forces a pinch correction", "−6 pinch · +3 wear · +1 h · Mara +4 fatigue/+1 loyalty", HasCrew(state, "mara-venn") && state.Ship.PinchReserve >= 6 && state.Ship.DriveWearPercent <= 97, MaraBlocker(state)),
        ],
        _ => [],
    };

    private static JourneyAction CheckpointAction(CheckpointResponse response, string label, string consequence, bool available, string explanation) =>
        new(response.ToString(), label, consequence, available, explanation, null, response);

    private static string MaraBlocker(GameState state)
    {
        if (!HasCrew(state, "mara-venn")) return "Mara must be available.";
        if (state.Ship.PinchReserve < 6) return $"Requires 6 extra pinch points; Wayfarer has {state.Ship.PinchReserve}.";
        if (state.Ship.DriveWearPercent > 97) return $"Adds 3 drive wear; only {100 - state.Ship.DriveWearPercent} points of margin remain.";
        return "Uses reserve pinch for the fastest mechanical recovery.";
    }

    private static InformationJourneyPresentation? MapInformation(GameState state)
    {
        InformationItemState? item = FindInformation(state);
        return item is null ? null : new InformationJourneyPresentation(item.Id.Value, item.Title, item.Disposition.ToString(), item.ConfidencePercent, item.Provenance.Select(entry => $"{entry.Source} · {entry.ConfidencePercent}% · {entry.Note}").ToArray());
    }

    private static InformationItemState? FindInformation(GameState state) => state.Contract.Objective?.InformationId is InformationId id
        ? state.AllInformationCargo.SingleOrDefault(item => item.Id == id)
        : null;

    private static string CheckpointLabel(RouteCheckpointKind kind) => kind switch
    {
        RouteCheckpointKind.GravityBoundary => "Cross the gravity boundary",
        RouteCheckpointKind.Approach => "Commit Sirius approach",
        _ => "Resolve next checkpoint",
    };

    private static string RepairConsequence(GameState state) => state.Turnaround?.RepairService switch
    {
        RepairService.Certified => "+2 h · +1% hull wear",
        RepairService.IlyaFieldService => "+3 h · +1% drive/hull wear",
        _ => "+6 h · +2% drive · +1% hull wear",
    };

    private static string LatticeConsequence(GameState state) => state.Turnaround?.RepairService switch
    {
        RepairService.Certified => "+3 h · Ilya +4 fatigue/+1 loyalty",
        RepairService.IlyaFieldService => "+5 h · Ilya +4 fatigue/+1 loyalty",
        _ => "+8 h · Ilya +4 fatigue/+1 loyalty",
    };

    private static JourneyAction Response(EncounterResponse response, string label, string consequence, bool available, string explanation) => new(response.ToString(), label, consequence, available, explanation, response);
    private static bool HasCrew(GameState state, string id) => state.Crew.Any(item => item.Id.Value == id && item.Available);
    private static bool HasContractCargo(GameState state) => state.Cargo.Any(item => item.IsContractCargo && item.Sealed && item.Quantity >= state.Contract.Quantity);
    private static string LocalizeOutcome(string outcome, VerticalSliceContentPack pack)
    {
        string localized = outcome;
        foreach (StationDefinition station in pack.Stations.OrderByDescending(item => item.Id.Value.Length))
            localized = localized.Replace(station.Id.Value, pack.Text(station.NameKey), StringComparison.Ordinal);
        return localized;
    }

    private static string ContinuePath(JourneyPhase phase) => phase is JourneyPhase.InTransit or JourneyPhase.EncounterPending or JourneyPhase.Approach ? "/travel" : "/operations";
    private static string Location(JourneyPhase phase, VerticalSliceContentPack pack, StationDefinition origin, StationDefinition destination) => phase switch
    {
        JourneyPhase.InTransit or JourneyPhase.EncounterPending => $"{pack.Text(origin.NameKey)}–{pack.Text(destination.NameKey)} transfer",
        JourneyPhase.Approach => $"{pack.Text(destination.NameKey)} approach corridor",
        JourneyPhase.Docked or JourneyPhase.CustomsPending or JourneyPhase.Turnaround or JourneyPhase.Delivered => pack.Text(destination.NameKey),
        _ => pack.Text(origin.NameKey),
    };
    private static string FactionName(string factionId) => factionId switch
    {
        FactionIds.TerranContinuityAuthority => "Terran Continuity Authority",
        FactionIds.KuiperSyndicates => "Kuiper Syndicates",
        FactionIds.SiriusCorporateCompact => "Sirius Corporate Compact",
        FactionIds.SiriusLabor => "Sirius labor",
        _ => factionId,
    };
    private static string FormatTime(GameTime time) => $"Day {time.HoursSinceStart / 24:N0} · {time.HoursSinceStart % 24:00}:00 station time";
}
