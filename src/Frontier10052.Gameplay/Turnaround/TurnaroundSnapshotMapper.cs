using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Turnaround;

internal static class TurnaroundSnapshotMapper
{
    public static CommandResult<TurnaroundSnapshot> TryMap(GameState state, VerticalSliceContentPack pack)
    {
        TurnaroundState? turnaround = state.Turnaround;
        if (turnaround is null)
            return CommandResult<TurnaroundSnapshot>.Failure(CommandErrorCodes.TurnaroundUnavailable, "The Mars turnaround opens after the first contract is settled.");

        StationDefinition station = pack.Stations.Single(item => item.Id == turnaround.StationId);
        MaintenanceState maintenance = state.Maintenance ?? new MaintenanceState(100 - state.Ship.HullWearPercent, 100 - state.Ship.DriveWearPercent, false, []);
        LienState lien = state.Lien ?? new LienState(new Credits(72_000), null, []);
        bool mutable = !state.DepartureAuthorized && !turnaround.DepartureAuthorized;
        int offerHours = checked((int)(turnaround.OffersExpireAt.HoursSinceStart - state.Time.HoursSinceStart));

        IReadOnlyList<TurnaroundCrewPresentation> crew = state.Crew.Select(member =>
        {
            CrewDefinition definition = pack.Crew.Single(item => item.Id == member.Id);
            IReadOnlyList<TurnaroundCrewMemoryPresentation> memories = (member.Memories ?? []).OrderByDescending(item => item.RecordedAt.HoursSinceStart).Select(memory =>
                new TurnaroundCrewMemoryPresentation(memory.Kind, memory.Summary, memory.LoyaltyDelta, FormatTime(memory.RecordedAt))).ToArray();
            return new TurnaroundCrewPresentation(member.Id.Value, pack.Text(definition.NameKey), pack.Text(definition.RoleKey), member.Loyalty, member.Fatigue, member.CurrentAssignment, memories);
        }).ToArray();

        IReadOnlyList<TurnaroundOfferPresentation> offers = pack.Contracts.Where(item => item.IsTurnaroundOffer).Select(definition =>
        {
            ContractState? contract = state.AllContracts.SingleOrDefault(item => item.Id == definition.Id);
            RouteDefinition route = pack.Routes.Single(item => item.OriginStationId == definition.OriginStationId && item.DestinationStationId == definition.DestinationStationId);
            EncounterDefinition encounter = pack.Encounters.Single(item => item.Id == route.EncounterPool[0]);
            CommodityDefinition commodity = pack.Commodities.Single(item => item.Id == definition.CommodityId);
            bool open = contract?.Status == ContractStatus.Offered && turnaround.SelectedContractId is null && offerHours >= 0;
            bool capacity = state.CargoAvailable >= definition.Quantity;
            string explanation = !mutable
                ? "The authorized departure manifest is locked."
                : turnaround.SelectedContractId is not null
                    ? contract?.Status == ContractStatus.Accepted ? "Selected; issuer cargo is sealed in the hold." : "Closed as transformed history when the competing offer was selected."
                    : offerHours < 0
                        ? "The 24-hour Mars offer window has expired."
                        : !capacity
                            ? $"Requires {definition.Quantity.Value} free tonnes; only {state.CargoAvailable.Value} remain."
                            : "Selection locks free issuer cargo, closes the competing offer, and records both crew reactions.";
            return new TurnaroundOfferPresentation(
                definition.Id.Value,
                pack.Text(definition.TitleKey),
                pack.Text(definition.IssuerKey),
                pack.Text(pack.Stations.Single(item => item.Id == definition.DestinationStationId).NameKey),
                pack.Text(commodity.NameKey),
                definition.Quantity.Value,
                checked((int)((contract?.Deadline ?? state.Time.AddHours(definition.DeadlineHours)).HoursSinceStart - state.Time.HoursSinceStart)),
                FormatTime(contract?.Deadline ?? state.Time.AddHours(definition.DeadlineHours)),
                definition.Reward.Value,
                definition.FailurePenalty.Value,
                pack.Text(commodity.LegalityKey),
                pack.Text(definition.ConsequenceKey),
                contract?.Status.ToString() ?? ContractStatus.Offered.ToString(),
                route.DurationHours,
                route.FuelCostPercent,
                route.DriveWearPercent,
                pack.Text(encounter.TitleKey),
                definition.DestinationStationId.Value == "pluto-gateway" ? "Tomas +5 loyalty · Ilya −2" : "Ilya +5 loyalty · Tomas −2",
                new TurnaroundAction($"select-{definition.Id.Value}", "Select contract", $"Locks {definition.Quantity.Value} t · {route.DurationHours} h route", open && capacity && mutable, explanation));
        }).ToArray();

        ContractDefinition? selectedDefinition = turnaround.SelectedContractId is null ? null : pack.Contracts.Single(item => item.Id == turnaround.SelectedContractId.Value);
        RouteDefinition? selectedRoute = selectedDefinition is null ? null : pack.Routes.Single(item => item.OriginStationId == selectedDefinition.OriginStationId && item.DestinationStationId == selectedDefinition.DestinationStationId);
        IReadOnlyList<TurnaroundRequirement> requirements =
        [
            new("Inherited lien", turnaround.LienDisposition is not null, turnaround.LienDisposition is null ? "Service or defer the 72,000-credit lien" : $"{LienLabel(turnaround.LienDisposition.Value)} · {lien.Principal.Value:N0} credits principal"),
            new("Drive service", turnaround.RepairService is not null, turnaround.RepairService is null ? "Choose certified, Ilya field service, or explicit deferral" : $"{RepairLabel(turnaround.RepairService.Value)} · {state.Ship.DriveWearPercent}% wear"),
            new("Crew rest", turnaround.CrewRestService is not null, turnaround.CrewRestService is null ? "Choose a full layover or turnaround watches" : RestLabel(turnaround.CrewRestService.Value)),
            new("Contract", selectedDefinition is not null, selectedDefinition is null ? "Select Pluto or Ceres before the offer window closes" : pack.Text(selectedDefinition.TitleKey)),
            new("Cargo capacity", selectedDefinition is not null && state.Cargo.Any(item => item.IsContractCargo && item.CommodityId == selectedDefinition.CommodityId && item.Quantity >= selectedDefinition.Quantity), selectedDefinition is null ? "Cargo requirement is known after selection" : $"{selectedDefinition.Quantity.Value} sealed tonnes required"),
            new("Route fuel", selectedRoute is not null && state.Ship.FuelPercent >= selectedRoute.FuelCostPercent, selectedRoute is null ? "Route requirement is known after selection" : $"{selectedRoute.FuelCostPercent}% required · {state.Ship.FuelPercent}% aboard"),
            new("Drive margin", selectedRoute is not null && state.Ship.DriveWearPercent + selectedRoute.DriveWearPercent <= 100, selectedRoute is null ? "Route requirement is known after selection" : $"+{selectedRoute.DriveWearPercent}% route wear · {state.Ship.DriveWearPercent}% current"),
        ];
        bool ready = mutable && requirements.All(item => item.Complete);
        string departureExplanation = state.DepartureAuthorized
            ? "Second-voyage departure is authorized and the route manifest is locked."
            : ready ? "All debt, ship, crew, cargo, fuel, and route checks are complete." : "Complete every requirement shown beside this control.";

        string callsign = state.Commander.Callsign;
        string initials = string.Concat(callsign.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
        if (initials.Length == 0) initials = "NC";
        return CommandResult<TurnaroundSnapshot>.Success(new TurnaroundSnapshot(
            state.GameId.Value,
            state.Seed,
            state.CommandSequence,
            state.SchemaVersion,
            pack.Version,
            pack.Text(station.NameKey),
            pack.Text(station.FacilityKey),
            FormatTime(state.Time),
            $"Cmdr. {callsign}",
            initials,
            new TurnaroundShipPresentation(pack.Text(pack.Ship.NameKey), pack.Text(pack.Ship.HullKey), state.CargoLoaded.Value, state.Ship.CargoCapacity.Value, state.CargoAvailable.Value, state.Ship.FuelPercent, state.Ship.DriveWearPercent, maintenance.HullConditionPercent),
            crew,
            new TurnaroundLienPresentation(lien.Principal.Value, turnaround.LienDisposition is null ? null : LienLabel(turnaround.LienDisposition.Value), lien.PaymentHistory.Select(item => new TurnaroundLienHistoryPresentation(LienLabel(item.Disposition), item.Amount.Value, item.PrincipalAfter.Value, item.Note, FormatTime(item.RecordedAt))).ToArray()),
            new TurnaroundMaintenancePresentation(maintenance.HullConditionPercent, maintenance.DriveConditionPercent, turnaround.RepairService is null ? "Pending" : RepairLabel(turnaround.RepairService.Value), maintenance.RepairDeferred, maintenance.RepairHistory.Select(item => new TurnaroundRepairHistoryPresentation(RepairLabel(item.Service), item.Cost.Value, item.Hours, item.WearRemoved, item.Provenance, FormatTime(item.CompletedAt))).ToArray()),
            state.AllFactionStandings.Select(item => new TurnaroundFactionPresentation(item.FactionId, item.FactionId == FactionIds.TerranContinuityAuthority ? "Terran Continuity Authority" : "Kuiper Syndicates", item.Standing)).ToArray(),
            state.LegalExposure,
            state.Money.Value,
            offerHours,
            offers,
            Action("service-lien", "Service lien", "−6,000 cr · principal 66,000 · +2 trust", mutable && turnaround.LienDisposition is null && state.Money.Value >= 6_000, LienExplanation(state, turnaround, service: true)),
            Action("defer-lien", "Defer lien", "+1,500 principal · −2 trust", mutable && turnaround.LienDisposition is null, LienExplanation(state, turnaround, service: false)),
            ServiceAction("repair-certified", "Certified repair", "−5,400 cr · +8 h · −12% wear", mutable, turnaround.RepairService is null, state.Money.Value >= 5_400, CanConsumeTime(turnaround, state.Time, 8), "Certified port provenance and the largest wear reduction."),
            ServiceAction("repair-field", "Ilya field service", "−1,800 cr · +5 h · −6% wear · Ilya +5 fatigue", mutable, turnaround.RepairService is null, state.Money.Value >= 1_800, CanConsumeTime(turnaround, state.Time, 5), "Lower-cost repair whose provenance and crew fatigue persist."),
            ServiceAction("repair-defer", "Defer repairs", "No cost · retained voyage risk", mutable, turnaround.RepairService is null, true, true, "Explicitly accepts the current drive-risk envelope."),
            ServiceAction("rest-full", "Full crew layover", "−1,200 cr · +8 h · −10 fatigue", mutable, turnaround.CrewRestService is null, state.Money.Value >= 1_200, CanConsumeTime(turnaround, state.Time, 8), "Reduces every crew member's fatigue by up to ten."),
            ServiceAction("rest-watches", "Turnaround watches", "−400 cr · +4 h · −4 fatigue", mutable, turnaround.CrewRestService is null, state.Money.Value >= 400, CanConsumeTime(turnaround, state.Time, 4), "A shorter rest that preserves more of the offer window."),
            requirements,
            new TurnaroundAction("authorize", "Authorize second-voyage departure", selectedRoute is null ? "Select a route first" : $"{selectedRoute.DurationHours} h · −{selectedRoute.FuelCostPercent}% fuel · +{selectedRoute.DriveWearPercent}% wear", ready, departureExplanation),
            state.DepartureAuthorized,
            selectedDefinition is null ? null : pack.Text(pack.Stations.Single(item => item.Id == selectedDefinition.DestinationStationId).NameKey),
            turnaround.LastOutcome));
    }

    private static TurnaroundAction Action(string id, string label, string consequence, bool available, string explanation) => new(id, label, consequence, available, explanation);

    private static TurnaroundAction ServiceAction(string id, string label, string consequence, bool mutable, bool unresolved, bool affordable, bool timeAllowed, string availableExplanation)
    {
        bool available = mutable && unresolved && affordable && timeAllowed;
        string explanation = !mutable ? "The authorized manifest is locked."
            : !unresolved ? "A choice for this service category is already recorded."
            : !affordable ? "Available credits are insufficient for this service."
            : !timeAllowed ? "This service would expire both offers before one is selected."
            : availableExplanation;
        return Action(id, label, consequence, available, explanation);
    }

    private static string LienExplanation(GameState state, TurnaroundState turnaround, bool service)
    {
        if (state.DepartureAuthorized) return "The authorized manifest is locked.";
        if (turnaround.LienDisposition is not null) return $"Lien choice recorded: {turnaround.LienDisposition}.";
        if (service && state.Money.Value < 6_000) return $"Requires 6,000 credits; only {state.Money.Value:N0} are available.";
        return service ? "Reduces principal from 72,000 to 66,000 and improves commercial trust." : "Capitalizes 1,500 credits into principal and reduces commercial trust.";
    }

    private static bool CanConsumeTime(TurnaroundState turnaround, GameTime time, int hours) =>
        turnaround.SelectedContractId is not null || time.AddHours(hours).CompareTo(turnaround.OffersExpireAt) <= 0;

    private static string LienLabel(LienDisposition disposition) => disposition switch
    {
        LienDisposition.Serviced => "Serviced",
        LienDisposition.Deferred => "Deferred",
        _ => disposition.ToString(),
    };

    private static string RepairLabel(RepairService service) => service switch
    {
        RepairService.Certified => "Certified repair",
        RepairService.IlyaFieldService => "Ilya field service",
        RepairService.Deferred => "Repairs deferred",
        _ => service.ToString(),
    };

    private static string RestLabel(CrewRestService service) => service switch
    {
        CrewRestService.FullLayover => "Full crew layover",
        CrewRestService.TurnaroundWatches => "Turnaround watches",
        _ => service.ToString(),
    };

    private static string FormatTime(GameTime time) => $"Day {time.HoursSinceStart / 24:N0} · {time.HoursSinceStart % 24:00}:00 station time";
}
