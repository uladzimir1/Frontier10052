using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Journey;

internal static class JourneySnapshotMapper
{
    public static JourneySnapshot Map(GameState state, VerticalSliceContentPack pack)
    {
        JourneyState journey = state.Journey ?? throw new InvalidOperationException("Schema-3 state requires a journey.");
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
        bool canAdvance = journey.Phase == JourneyPhase.InTransit;
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
            new JourneyFactionPresentation(item.FactionId, item.FactionId == FactionIds.TerranContinuityAuthority ? "Terran Continuity Authority" : "Kuiper Syndicates", item.Standing)).ToArray();
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
            new ShipJourneyPresentation(pack.Text(pack.Ship.NameKey), pack.Text(pack.Ship.HullKey), state.CargoLoaded.Value, state.Ship.CargoCapacity.Value, state.Ship.FuelPercent, state.Ship.DriveWearPercent),
            new RoutePresentation(definition.Id.Value, pack.Text(definition.NameKey), pack.Text(origin.NameKey), pack.Text(destination.NameKey), duration, fuelCost, definition.DriveWearPercent, route?.EncounterAtHour ?? definition.EncounterAtHour, elapsed, delay, estimated, actual, progress, pack.Text(definition.ProfileKey)),
            MapEncounter(state, pack),
            cargo,
            bids,
            sales,
            new ContractJourneyPresentation(pack.Text(contractDefinition.TitleKey), state.Contract.Status.ToString(), state.Contract.Quantity.Value, pack.Text(contractCommodity.NameKey), state.Contract.Reward.Value, state.Contract.FailurePenalty.Value, checked((int)(state.Contract.Deadline.HoursSinceStart - state.Time.HoursSinceStart)), FormatTime(state.Contract.Deadline)),
            new FinanceJourneyPresentation(state.Money.Value, state.CommercialTrust, journey.Sales.Sum(item => item.Proceeds.Value), state.Contract.Status == ContractStatus.Accepted ? state.Contract.Reward.Value : 0),
            LocalizeOutcome(journey.LastOutcome, pack),
            new JourneyAction("begin", "Commit undock and burn", $"Consumes {fuelCost}% fuel and adds {definition.DriveWearPercent}% drive wear", canBegin, canBegin ? $"The authorized {pack.Text(definition.NameKey)} manifest is ready." : "Undock is available only from an authorized station departure."),
            new JourneyAction("advance", journey.Encounter is null ? "Advance to route contact" : $"Advance to {pack.Text(destination.NameKey)} approach", "Advances authoritative simulation time; animation is presentational only", canAdvance, journey.Phase == JourneyPhase.EncounterPending ? "Resolve the active encounter first." : canAdvance ? "The next route milestone is ready." : "Travel is not in an advanceable phase."),
            new JourneyAction("dock", $"Dock at {pack.Text(destination.NameKey)}", $"Commits {pack.Text(destination.NameKey)} as Wayfarer's authoritative station", canDock, canDock ? "Approach corridor and berth are confirmed." : "Complete the route and encounter before docking."),
            new JourneyAction("deliver", "Present locked contract cargo", $"Pays {state.Contract.Reward.Value:N0} credits if destination, seals, quantity, and deadline validate", canDeliver, canDeliver ? "Destination custody is ready to validate the active manifest." : state.Contract.Status is ContractStatus.Completed or ContractStatus.Failed ? "The contract is already settled." : "Dock at the active destination with the accepted sealed shipment."),
            journey.VoyageNumber,
            crew,
            factions,
            state.Lien?.Principal.Value ?? 72_000,
            state.LegalExposure,
            destinationManifest);
    }

    private static RouteDefinition ResolveRoute(GameState state, JourneyState journey, VerticalSliceContentPack pack)
    {
        RouteId? routeId = journey.Route?.Id ?? state.DepartureManifest?.RouteId;
        if (routeId is not null) return pack.Routes.Single(item => item.Id == routeId.Value);
        return pack.Routes.Single(item => item.OriginStationId == state.Contract.OriginStationId && item.DestinationStationId == state.Contract.DestinationStationId);
    }

    private static EncounterPresentation? MapEncounter(GameState state, VerticalSliceContentPack pack)
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
        return new EncounterPresentation(encounter.Id.Value, pack.Text(definition.TitleKey), pack.Text(definition.DetailKey), pack.Text(definition.SourceKey), encounter.Status.ToString(), LocalizeOutcome(encounter.Outcome, pack), responses);
    }

    private static string RepairConsequence(GameState state) => state.Turnaround?.RepairService switch
    {
        RepairService.Certified => "+2 h · +1% hull wear",
        RepairService.IlyaFieldService => "+3 h · +1% drive/hull wear",
        _ => "+6 h · +2% drive · +1% hull wear",
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
        JourneyPhase.Docked or JourneyPhase.Turnaround or JourneyPhase.Delivered => pack.Text(destination.NameKey),
        _ => pack.Text(origin.NameKey),
    };
    private static string FormatTime(GameTime time) => $"Day {time.HoursSinceStart / 24:N0} · {time.HoursSinceStart % 24:00}:00 station time";
}
