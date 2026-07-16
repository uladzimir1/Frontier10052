using Frontier10052.Content;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Operations;

internal static class StationSnapshotMapper
{
    public static StationOperationsSnapshot Map(GameState state, VerticalSliceContentPack pack)
    {
        StationDefinition location = pack.Stations.Single(item => item.Id == state.Ship.StationId);
        StationDefinition destination = pack.Stations.Single(item => item.Id == state.Contract.DestinationStationId);
        StationDefinition origin = pack.Stations.Single(item => item.Id == state.Contract.OriginStationId);
        ContractDefinition contractDefinition = pack.Contracts.Single(item => item.Id == state.Contract.Id);
        RouteDefinition contractRoute = pack.Routes.Single(item => item.OriginStationId == state.Contract.OriginStationId && item.DestinationStationId == state.Contract.DestinationStationId);
        string locationName = state.Journey?.Phase switch
        {
            JourneyPhase.InTransit or JourneyPhase.EncounterPending => state.Journey?.Route is null ? "Transfer orbit" : $"{pack.Text(pack.Stations.Single(item => item.Id == state.Journey.Route.OriginStationId).NameKey)}–{pack.Text(pack.Stations.Single(item => item.Id == state.Journey.Route.DestinationStationId).NameKey)} transfer",
            JourneyPhase.Approach => $"{pack.Text(destination.NameKey)} approach corridor",
            _ => pack.Text(location.NameKey),
        };
        string locationDetail = state.Journey?.Phase switch
        {
            JourneyPhase.InTransit or JourneyPhase.EncounterPending => "Sol local flight · active contract route",
            JourneyPhase.Approach => $"{destination.SystemName} traffic control · berth assignment",
            _ => pack.Text(location.FacilityKey),
        };
        CommodityDefinition contractCommodity = pack.Commodities.Single(item => item.Id == state.Contract.CommodityId);

        IReadOnlyList<CrewPresentation> crew = state.Crew.Select(member =>
        {
            CrewDefinition definition = pack.Crew.Single(item => item.Id == member.Id);
            return new CrewPresentation(
                member.Id.Value,
                pack.Text(definition.NameKey),
                pack.Text(definition.RoleKey),
                pack.Text(definition.BriefingKey),
                member.Loyalty,
                member.Fatigue,
                member.Available ? "Available" : "Unavailable",
                member.CurrentAssignment);
        }).ToArray();

        IReadOnlyList<MarketItemPresentation> market = state.Market.Listings.Select(listing =>
        {
            CommodityDefinition definition = pack.Commodities.Single(item => item.Id == listing.CommodityId);
            return new MarketItemPresentation(
                definition.Id.Value,
                pack.Text(definition.NameKey),
                pack.Text(definition.DescriptionKey),
                pack.Text(definition.LegalityKey),
                listing.AskPrice.Value,
                listing.Stock.Value,
                definition.EstimatedMarsProfitLow,
                definition.EstimatedMarsProfitHigh,
                listing.Purchasable);
        }).ToArray();

        IReadOnlyList<MarketReportPresentation> reports = state.Reports.Select(report =>
        {
            MarketReportDefinition definition = pack.Reports.Single(item => item.Id == report.Id);
            return new MarketReportPresentation(
                report.Id,
                pack.Text(definition.HeadlineKey),
                pack.Text(definition.DetailKey),
                pack.Text(definition.SourceKey),
                checked((int)(state.Time.HoursSinceStart - report.ObservedAt.HoursSinceStart)),
                report.ConfidencePercent,
                pack.Text(definition.LegalityKey),
                report.Verified,
                report.EngineerAnalyzed);
        }).ToArray();

        IReadOnlyList<CargoPresentation> cargo = state.Cargo.Select(item => MapCargo(item, pack)).ToArray();
        bool cargoReady = state.Cargo.Any(item =>
            item.CommodityId == state.Contract.CommodityId &&
            item.IsContractCargo &&
            item.Sealed &&
            item.Quantity >= state.Contract.Quantity);

        ReadinessRequirement[] requirements =
        [
            new("Crew briefing", state.BriefingAcknowledged, state.BriefingAcknowledged ? "Acknowledged in the command journal" : "Review the crew and ship context, then acknowledge it"),
            new("Contract", state.Contract.Status == ContractStatus.Accepted, state.Contract.Status == ContractStatus.Accepted ? "Terms accepted" : "Accept the Mars clinic transfer"),
            new("Required cargo", cargoReady, cargoReady ? "18 tonnes sealed and locked" : "Accepting the contract loads the issuer's sealed shipment"),
            new("Engineer assignment", state.EngineerAssignment is not null, state.EngineerAssignment is not null ? FormatEngineerAssignment(state.EngineerAssignment.Value) : "Choose data analysis or drive inspection"),
        ];

        bool canAuthorize = !state.DepartureAuthorized && requirements.All(item => item.Complete);
        string authorizationExplanation = state.DepartureAuthorized
            ? "Departure is authorized; the manifest is locked for the undock handoff."
            : canAuthorize
                ? "All required departure checks are complete. Optional speculative cargo may be skipped."
                : "Complete each required check listed beside this control. Optional speculative cargo is not required.";

        AuthorizedManifestPresentation? manifest = state.DepartureManifest is null
            ? null
            : new AuthorizedManifestPresentation(
                pack.Text(pack.Stations.Single(item => item.Id == state.DepartureManifest.OriginStationId).NameKey),
                pack.Text(pack.Stations.Single(item => item.Id == state.DepartureManifest.DestinationStationId).NameKey),
                FormatTime(state.DepartureManifest.AuthorizedAt),
                state.DepartureManifest.AuthorizedAtCommandSequence,
                FormatEngineerAssignment(state.DepartureManifest.EngineerAssignment),
                state.DepartureManifest.ReportDecision is null ? "No speculative market position recorded" : FormatReportDecision(state.DepartureManifest.ReportDecision.Value),
                state.DepartureManifest.RemainingCredits.Value,
                state.DepartureManifest.Cargo.Select(item => MapCargo(item, pack)).ToArray());

        string callsign = state.Commander.Callsign;
        string initials = string.Concat(callsign.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
        if (initials.Length == 0) initials = "NC";

        bool mutable = !state.DepartureAuthorized;
        ActionAvailability contractAction = new(
            mutable && state.Contract.Status == ContractStatus.Offered,
            state.DepartureAuthorized ? "The authorized manifest is locked." : state.Contract.Status == ContractStatus.Accepted ? "Accepted; 18 tonnes are sealed in the hold." : "Accepting loads the issuer-provided shipment at no purchase cost.");
        ActionAvailability briefingAction = new(
            mutable && !state.BriefingAcknowledged,
            state.DepartureAuthorized ? "The authorized manifest is locked." : state.BriefingAcknowledged ? "Briefing acknowledged in the command journal." : "Acknowledgement is required before departure authorization.");
        ActionAvailability engineerAction = new(
            mutable && state.EngineerAssignment is null,
            state.DepartureAuthorized ? "The authorized manifest is locked." : state.EngineerAssignment is not null ? "Ilya's single pre-departure task is complete." : "Choose one task; the other opportunity closes before departure.");

        return new StationOperationsSnapshot(
            state.GameId.Value,
            state.Seed,
            state.CommandSequence,
            pack.Version,
            locationName,
            locationDetail,
            FormatTime(state.Time),
            new CommanderPresentation($"Cmdr. {callsign}", initials, "Independent hauler · first Sol contract"),
            new ShipPresentation(
                pack.Text(pack.Ship.NameKey),
                pack.Text(pack.Ship.HullKey),
                state.CargoLoaded.Value,
                state.Ship.CargoCapacity.Value,
                state.CargoAvailable.Value,
                state.Ship.FuelPercent,
                state.Ship.DriveWearPercent),
            crew,
            new ContractPresentation(
                state.Contract.Id.Value,
                pack.Text(contractDefinition.TitleKey),
                pack.Text(contractDefinition.IssuerKey),
                pack.Text(origin.NameKey),
                pack.Text(destination.NameKey),
                pack.Text(contractCommodity.NameKey),
                state.Contract.Quantity.Value,
                checked((int)(state.Contract.Deadline.HoursSinceStart - state.Time.HoursSinceStart)),
                FormatTime(state.Contract.Deadline),
                state.Contract.Reward.Value,
                state.Contract.FailurePenalty.Value,
                pack.Text(contractDefinition.ConsequenceKey),
                state.Contract.Status != ContractStatus.Offered,
                contractAction,
                contractRoute.DurationHours),
            market,
            reports,
            state.ReportDecision is null ? null : FormatReportDecision(state.ReportDecision.Value),
            cargo,
            new FinancePresentation(state.Money.Value, state.Contract.Reward.Value, state.Contract.FailurePenalty.Value),
            new BriefingPresentation(state.BriefingAcknowledged, briefingAction),
            new EngineerPresentation(
                state.EngineerAssignment is null ? null : FormatEngineerAssignment(state.EngineerAssignment.Value),
                state.EngineerOutcome,
                state.DepartureReliabilityRisk,
                engineerAction,
                engineerAction),
            new DeparturePresentation(
                state.DepartureAuthorized,
                new ActionAvailability(canAuthorize, authorizationExplanation),
                requirements,
                manifest),
            state.Journey?.Phase.ToString() ?? JourneyPhase.DockedAtOrigin.ToString(),
            state.Journey?.Phase is JourneyPhase.InTransit or JourneyPhase.EncounterPending or JourneyPhase.Approach ? "/travel" : "/operations",
            state.Turnaround is not null || state.Ship.StationId != pack.Ship.StationId,
            state.SchemaVersion,
            state.Turnaround is not null && state.Journey?.Phase is JourneyPhase.Turnaround or JourneyPhase.DepartureAuthorized,
            state.Journey?.VoyageNumber ?? 1,
            state.Lien?.Principal.Value ?? 72_000,
            state.Maintenance is null ? $"{state.Ship.DriveWearPercent}% drive wear" : state.Maintenance.RepairDeferred ? $"Deferred · {state.Ship.DriveWearPercent}% drive wear" : $"{state.Maintenance.DriveConditionPercent}% drive condition · {state.Maintenance.RepairHistory.Count} service records",
            state.LegalExposure,
            state.AllFactionStandings.Select(item => new FactionPresentation(item.FactionId, item.FactionId == FactionIds.TerranContinuityAuthority ? "Terran Continuity Authority" : "Kuiper Syndicates", item.Standing)).ToArray(),
            ImportantConsequences(state));
    }

    public static string FormatReportDecision(ReportDecision decision) => decision switch
    {
        ReportDecision.ActuatorShortage => "Back the verified actuator shortage",
        ReportDecision.ConvoyArrival => "Back the newer convoy-arrival report",
        ReportDecision.CoolantAlternative => "Choose the safer coolant-coupling position",
        _ => "Skip speculative trade",
    };

    private static CargoPresentation MapCargo(CargoLineState item, VerticalSliceContentPack pack)
    {
        CommodityDefinition definition = pack.Commodities.Single(commodity => commodity.Id == item.CommodityId);
        return new CargoPresentation(item.CommodityId.Value, pack.Text(definition.NameKey), item.Quantity.Value, item.IsContractCargo, item.Sealed);
    }

    private static string FormatEngineerAssignment(EngineerAssignment assignment) => assignment switch
    {
        EngineerAssignment.AnalyzeCourierManifest => "Courier manifest analysis",
        _ => "Drive inspection",
    };

    private static string FormatTime(Domain.GameTime time)
    {
        long day = time.HoursSinceStart / 24;
        long hour = time.HoursSinceStart % 24;
        return $"Day {day:N0} · {hour:00}:00 station time";
    }

    private static string ImportantConsequences(GameState state)
    {
        CrewMemoryState? memory = state.Crew.SelectMany(item => item.Memories ?? []).OrderByDescending(item => item.RecordedAt.HoursSinceStart).FirstOrDefault();
        string faction = string.Join(" · ", state.AllFactionStandings.Select(item => $"{(item.FactionId == FactionIds.TerranContinuityAuthority ? "TCA" : "Kuiper Syndicates")} {item.Standing:+#;-#;0}"));
        return memory is null ? faction : $"{memory.Summary} {faction}";
    }
}
