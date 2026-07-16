using Frontier10052.Domain;

namespace Frontier10052.Simulation;

public static class SiriusAftermathCommands
{
    public static GameState CreateAftermath(
        GameState state,
        StationEventId eventId,
        CommodityId actuatorId,
        IReadOnlyList<ContractLeadId> leadIds)
    {
        if (state.InformationSettlement is null || state.SiriusAftermath is not null) return state;

        InformationDisposition disposition = state.InformationSettlement.Disposition;
        CrewMemberState noor = Crew(state, "noor-okafor");
        CrewMemberState tomas = Crew(state, "tomas-vale");
        int dispositionModifier = disposition switch
        {
            InformationDisposition.Disclosed => 15,
            InformationDisposition.Corroborated => 0,
            _ => 10,
        };
        int pressure = Math.Min(100, 25 + Math.Abs(noor.Loyalty - tomas.Loyalty) + dispositionModifier);
        Credits frozenPrice = MarketPricing.CalculateUnitPrice(new Credits(1_280), new Tonnes(12), new Tonnes(36));
        MarketListingState listing = new(actuatorId, frozenPrice, new Tonnes(12), false);
        StationMarketState market = new(state.Ship.StationId, [listing]);
        IReadOnlyList<StationMarketState> markets = ReplaceMarket(state.AllStationMarkets, market);
        IReadOnlyList<OutboundLeadState> leads = leadIds.Select(id => new OutboundLeadState(id, false, "Resolve the Meridian actuator allocation to reveal this lead's sponsor.")).ToArray();

        return state with
        {
            Market = market,
            StationMarkets = markets,
            SiriusAftermath = new SiriusAftermathState(
                eventId,
                SiriusAftermathPhase.CrewConflict,
                state.Time,
                disposition,
                12,
                frozenPrice,
                new CrewConflictState(pressure, null, null, "Noor and Tomas have not yet submitted to a command ruling."),
                null,
                null,
                0,
                0,
                0,
                frozenPrice,
                leads,
                "The forecast is now physical: twelve actuator units remain against thirty-six units of station demand."),
        };
    }

    public static CommandResult<GameState> ResolveCrewConflict(GameState state, CrewConflictResponse response)
    {
        SiriusAftermathState? aftermath = state.SiriusAftermath;
        if (aftermath is null) return Fail(CommandErrorCodes.AftermathUnavailable, "The Meridian actuator aftermath is available only after Sirius information settlement.");
        if (aftermath.Phase != SiriusAftermathPhase.CrewConflict)
            return aftermath.CrewConflict.Response is not null
                ? Fail(CommandErrorCodes.AftermathAlreadyResolved, "The Noor–Tomas crew hearing is already resolved.")
                : Fail(CommandErrorCodes.AftermathWrongPhase, "The crew hearing is not the active aftermath phase.");

        CrewMemberState noor = Crew(state, "noor-okafor");
        CrewMemberState tomas = Crew(state, "tomas-vale");
        CrewMemberState ilya = Crew(state, "ilya-sato");
        int hours;
        int pressureDelta;
        IReadOnlyList<CrewMemberState> crew = state.Crew;
        string outcome;
        GameTime resolvedAt;

        switch (response)
        {
            case CrewConflictResponse.SupportNoor:
                if (!noor.Available) return Unavailable("Noor must be available before command can support her custody position.");
                if (noor.Loyalty < 60) return Unavailable($"Supporting Noor requires 60 loyalty; she has {noor.Loyalty}.");
                hours = 2;
                pressureDelta = 15;
                resolvedAt = state.Time.AddHours(hours);
                crew = UpdateCrew(crew, "noor-okafor", 0, 3, resolvedAt, "meridian-hearing", "Command backed Noor's corporate custody position during the actuator lockout.");
                crew = UpdateCrew(crew, "tomas-vale", 0, -2, resolvedAt, "meridian-hearing", "Command rejected Tomas's labor-first allocation argument.");
                outcome = "Command supported Noor after a two-hour hearing. Noor gained 3 loyalty; Tomas lost 2; conflict pressure rose 15.";
                break;
            case CrewConflictResponse.SupportTomas:
                if (!tomas.Available) return Unavailable("Tomas must be available before command can support his labor-safety position.");
                if (tomas.Loyalty < 65) return Unavailable($"Supporting Tomas requires 65 loyalty; he has {tomas.Loyalty}.");
                hours = 2;
                pressureDelta = 15;
                resolvedAt = state.Time.AddHours(hours);
                crew = UpdateCrew(crew, "tomas-vale", 0, 3, resolvedAt, "meridian-hearing", "Command backed Tomas's labor-safety position during the actuator lockout.");
                crew = UpdateCrew(crew, "noor-okafor", 0, -2, resolvedAt, "meridian-hearing", "Command rejected Noor's corporate custody argument.");
                outcome = "Command supported Tomas after a two-hour hearing. Tomas gained 3 loyalty; Noor lost 2; conflict pressure rose 15.";
                break;
            case CrewConflictResponse.JointAudit:
                if (!ilya.Available) return Unavailable("Ilya must be available to conduct the joint actuator audit.");
                if (ilya.Fatigue > 85) return Unavailable($"The joint audit requires Ilya at 85 fatigue or below; she is at {ilya.Fatigue}.");
                hours = 4;
                pressureDelta = -20;
                resolvedAt = state.Time.AddHours(hours);
                crew = UpdateCrew(crew, "ilya-sato", 4, 1, resolvedAt, "meridian-joint-audit", "Ilya reconciled corporate and labor actuator ledgers in a joint audit.");
                crew = UpdateCrew(crew, "noor-okafor", 0, 1, resolvedAt, "meridian-hearing", "Noor accepted Ilya's shared actuator audit.");
                crew = UpdateCrew(crew, "tomas-vale", 0, 1, resolvedAt, "meridian-hearing", "Tomas accepted Ilya's shared actuator audit.");
                outcome = "Ilya completed a four-hour joint audit. Pressure fell 20; Ilya gained 4 fatigue and 1 loyalty; Noor and Tomas each gained 1 loyalty.";
                break;
            default:
                hours = 1;
                pressureDelta = 25;
                resolvedAt = state.Time.AddHours(hours);
                crew = UpdateCrew(crew, "noor-okafor", 0, -2, resolvedAt, "captains-order", "A captain's order ended the actuator hearing without consensus.");
                crew = UpdateCrew(crew, "tomas-vale", 0, -2, resolvedAt, "captains-order", "A captain's order ended the actuator hearing without consensus.");
                outcome = "A one-hour captain's order ended the hearing. Noor and Tomas each lost 2 loyalty; conflict pressure rose 25.";
                break;
        }

        int pressure = Math.Clamp(aftermath.CrewConflict.Pressure + pressureDelta, 0, 100);
        SiriusAftermathState next = aftermath with
        {
            Phase = SiriusAftermathPhase.ActuatorAllocation,
            CrewConflict = new CrewConflictState(pressure, response, resolvedAt, outcome),
            Outcome = outcome,
        };
        return Success(state with
        {
            Time = resolvedAt,
            Crew = crew,
            SiriusAftermath = next,
            Journey = state.Journey is null ? null : state.Journey with { LastOutcome = outcome },
        });
    }

    public static CommandResult<GameState> ResolveActuatorAllocation(GameState state, ActuatorAllocationResponse response)
    {
        SiriusAftermathState? aftermath = state.SiriusAftermath;
        if (aftermath is null) return Fail(CommandErrorCodes.AftermathUnavailable, "The Meridian actuator aftermath is available only after Sirius information settlement.");
        if (aftermath.Phase == SiriusAftermathPhase.Resolved) return Fail(CommandErrorCodes.AftermathAlreadyResolved, "The twelve actuator units are already allocated.");
        if (aftermath.Phase != SiriusAftermathPhase.ActuatorAllocation) return Fail(CommandErrorCodes.AftermathWrongPhase, "Resolve the Noor–Tomas crew hearing before allocating actuators.");
        if (aftermath.ActuatorUnits != 12) return Fail(CommandErrorCodes.ActuatorAllocationUnavailable, $"Allocation requires the complete 12-unit frozen stock; {aftermath.ActuatorUnits} units remain.");

        InformationDisposition disposition = aftermath.ForecastDisposition;
        int compact = Standing(state, FactionIds.SiriusCorporateCompact);
        int labor = Standing(state, FactionIds.SiriusLabor);
        CrewMemberState ilya = Crew(state, "ilya-sato");
        int hours;
        int corporateUnits;
        int laborUnits;
        const int wayfarerUnits = 2;
        int wearRemoved;
        int compactDelta;
        int laborDelta;
        int trustDelta;
        long creditDelta;
        RepairService service;
        string provenance;
        string outcome;
        IReadOnlyList<CrewMemberState> crew = state.Crew;

        switch (response)
        {
            case ActuatorAllocationResponse.CorporatePriority:
                if (disposition is not (InformationDisposition.Sealed or InformationDisposition.Corroborated))
                    return AllocationUnavailable("Corporate priority requires a sealed or corroborated forecast disposition.");
                if (compact < 0) return AllocationUnavailable($"Corporate priority requires non-negative Compact standing; current standing is {compact}.");
                hours = 2;
                corporateUnits = 10;
                laborUnits = 0;
                wearRemoved = 6;
                compactDelta = 4;
                laborDelta = -4;
                trustDelta = 2;
                creditDelta = 4_000;
                service = RepairService.CompactAllocationRefit;
                provenance = "Sirius Corporate Compact priority actuator allocation";
                outcome = "Ten actuators entered Compact production and two refitted Wayfarer. The Compact paid 4,000 credits.";
                break;
            case ActuatorAllocationResponse.LaborSafety:
                if (disposition is not (InformationDisposition.Corroborated or InformationDisposition.Disclosed))
                    return AllocationUnavailable("Labor safety requires a corroborated or disclosed forecast disposition.");
                if (labor <= 0) return AllocationUnavailable($"Labor safety requires positive Sirius labor standing; current standing is {labor}.");
                hours = 4;
                corporateUnits = 0;
                laborUnits = 10;
                wearRemoved = 3;
                compactDelta = -4;
                laborDelta = 5;
                trustDelta = 0;
                creditDelta = 2_000;
                service = RepairService.LaborMutualAidRefit;
                provenance = "Meridian labor mutual-aid actuator allocation";
                outcome = "Ten actuators entered labor safety systems and two stabilized Wayfarer. Mutual aid released 2,000 credits.";
                break;
            default:
                if (!ilya.Available) return AllocationUnavailable("Ilya must be available to certify the audited split.");
                if (ilya.Fatigue > 90) return AllocationUnavailable($"The audited split requires Ilya at 90 fatigue or below; she is at {ilya.Fatigue}.");
                bool jointAudit = aftermath.CrewConflict.Response == CrewConflictResponse.JointAudit;
                long cost = jointAudit ? 1_500 : 3_000;
                if (state.Money.Value < cost) return Fail(CommandErrorCodes.AftermathInsufficientCredits, $"The voluntary audit costs {cost:N0} credits; only {state.Money.Value:N0} are available. This cost cannot be capitalized.");
                hours = jointAudit ? 4 : 6;
                corporateUnits = 5;
                laborUnits = 5;
                wearRemoved = 5;
                compactDelta = 2;
                laborDelta = 3;
                trustDelta = 1;
                creditDelta = -cost;
                service = RepairService.AuditedAllocationRefit;
                provenance = jointAudit ? "Joint-audit Meridian actuator split" : "Voluntary Meridian actuator allocation audit";
                crew = UpdateCrew(crew, "ilya-sato", 2, 0, state.Time.AddHours(hours), "meridian-allocation-audit", "Ilya certified the five-five-two actuator split.");
                outcome = jointAudit
                    ? "The joint audit closed at 1,500 credits and four hours: five corporate, five labor, two Wayfarer."
                    : "A 3,000-credit six-hour audit split five corporate, five labor, and two Wayfarer actuators.";
                break;
        }

        GameTime resolvedAt = state.Time.AddHours(hours);
        int actualWearRemoved = Math.Min(state.Ship.DriveWearPercent, wearRemoved);
        int nextWear = state.Ship.DriveWearPercent - actualWearRemoved;
        Credits money = creditDelta >= 0 ? state.Money + new Credits(creditDelta) : state.Money - new Credits(-creditDelta);
        IReadOnlyList<FactionStandingState> standings = AdjustStanding(state.AllFactionStandings, FactionIds.SiriusCorporateCompact, compactDelta);
        standings = AdjustStanding(standings, FactionIds.SiriusLabor, laborDelta);
        MaintenanceState maintenance = state.Maintenance ?? new MaintenanceState(100 - state.Ship.HullWearPercent, 100 - state.Ship.DriveWearPercent, true, []);
        RepairRecordState repair = new(resolvedAt, service, new Credits(Math.Max(0, -creditDelta)), hours, actualWearRemoved, provenance);
        CommodityId actuatorId = state.Market.Listings.Single(item => item.CommodityId.Value == "actuator-assemblies").CommodityId;
        Credits finalPrice = MarketPricing.CalculateUnitPrice(new Credits(1_280), new Tonnes(0), new Tonnes(36));
        StationMarketState market = new(state.Ship.StationId, [new MarketListingState(actuatorId, finalPrice, new Tonnes(0), false)]);
        IReadOnlyList<OutboundLeadState> leads = aftermath.Leads.Select(lead => lead with
        {
            Available = response == ActuatorAllocationResponse.AuditedSplit
                || (response == ActuatorAllocationResponse.CorporatePriority && lead.Id.Value == "scc-procyon-allocation-courier")
                || (response == ActuatorAllocationResponse.LaborSafety && lead.Id.Value == "meridian-outer-yard-salvage"),
            LockedReason = response == ActuatorAllocationResponse.AuditedSplit
                || (response == ActuatorAllocationResponse.CorporatePriority && lead.Id.Value == "scc-procyon-allocation-courier")
                || (response == ActuatorAllocationResponse.LaborSafety && lead.Id.Value == "meridian-outer-yard-salvage")
                    ? string.Empty
                    : "This sponsor did not receive enough of the Meridian actuator allocation.",
        }).ToArray();
        crew = UpdateCrew(crew, "noor-okafor", 0, 0, resolvedAt, "meridian-allocation", outcome);
        crew = UpdateCrew(crew, "tomas-vale", 0, 0, resolvedAt, "meridian-allocation", outcome);

        SiriusAftermathState next = aftermath with
        {
            Phase = SiriusAftermathPhase.Resolved,
            ActuatorUnits = 0,
            Allocation = response,
            ResolvedAt = resolvedAt,
            CorporateUnits = corporateUnits,
            LaborUnits = laborUnits,
            WayfarerUnits = wayfarerUnits,
            FinalUnitPrice = finalPrice,
            Leads = leads,
            Outcome = outcome,
        };

        return Success(state with
        {
            Time = resolvedAt,
            Money = money,
            Crew = crew,
            Ship = state.Ship with { DriveWearPercent = nextWear },
            Maintenance = maintenance with
            {
                DriveConditionPercent = 100 - nextWear,
                RepairDeferred = false,
                RepairHistory = [.. maintenance.RepairHistory, repair],
            },
            CommercialTrust = checked(state.CommercialTrust + trustDelta),
            FactionStandings = standings,
            Market = market,
            StationMarkets = ReplaceMarket(state.AllStationMarkets, market),
            SiriusAftermath = next,
            Journey = state.Journey is null ? null : state.Journey with { LastOutcome = outcome },
        });
    }

    private static CrewMemberState Crew(GameState state, string id) => state.Crew.Single(member => member.Id.Value == id);

    private static IReadOnlyList<CrewMemberState> UpdateCrew(IReadOnlyList<CrewMemberState> crew, string id, int fatigue, int loyalty, GameTime time, string kind, string summary) =>
        TurnaroundCommands.UpdateCrew(crew, id, member => TurnaroundCommands.AddMemory(
            member with
            {
                Fatigue = Math.Clamp(member.Fatigue + fatigue, 0, 100),
                Loyalty = Math.Clamp(member.Loyalty + loyalty, 0, 100),
                CurrentAssignment = summary,
            },
            time,
            kind,
            summary,
            loyalty));

    private static int Standing(GameState state, string factionId) => state.AllFactionStandings.SingleOrDefault(item => item.FactionId == factionId)?.Standing ?? 0;

    private static IReadOnlyList<FactionStandingState> AdjustStanding(IReadOnlyList<FactionStandingState> standings, string factionId, int delta) => standings.Any(item => item.FactionId == factionId)
        ? standings.Select(item => item.FactionId == factionId ? item with { Standing = checked(item.Standing + delta) } : item).ToArray()
        : [.. standings, new FactionStandingState(factionId, delta)];

    private static IReadOnlyList<StationMarketState> ReplaceMarket(IReadOnlyList<StationMarketState> markets, StationMarketState replacement) =>
        markets.Any(item => item.StationId == replacement.StationId)
            ? markets.Select(item => item.StationId == replacement.StationId ? replacement : item).ToArray()
            : [.. markets, replacement];

    private static CommandResult<GameState> Success(GameState state) => CommandResult<GameState>.Success(state with { CommandSequence = checked(state.CommandSequence + 1) });
    private static CommandResult<GameState> Fail(string code, string message) => CommandResult<GameState>.Failure(code, message);
    private static CommandResult<GameState> Unavailable(string message) => Fail(CommandErrorCodes.CrewConflictResponseUnavailable, message);
    private static CommandResult<GameState> AllocationUnavailable(string message) => Fail(CommandErrorCodes.ActuatorAllocationUnavailable, message);
}
