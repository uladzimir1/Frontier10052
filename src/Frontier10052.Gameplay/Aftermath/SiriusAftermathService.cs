using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Aftermath;

public sealed class SiriusAftermathService(GameSessionCoordinator sessions) : ISiriusAftermathService
{
    public async ValueTask<CommandResult<SiriusAftermathSnapshot>> ResumeAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Map(await sessions.ResumeAsync(playerKey, cancellationToken));

    public async ValueTask<CommandResult<SiriusAftermathSnapshot>> ResolveCrewConflictAsync(string playerKey, CrewConflictResponse response, CancellationToken cancellationToken = default) =>
        Map(await sessions.MutateAsync(playerKey, state => SiriusAftermathCommands.ResolveCrewConflict(state, response), cancellationToken));

    public async ValueTask<CommandResult<SiriusAftermathSnapshot>> ResolveActuatorAllocationAsync(string playerKey, ActuatorAllocationResponse response, CancellationToken cancellationToken = default) =>
        Map(await sessions.MutateAsync(playerKey, state => SiriusAftermathCommands.ResolveActuatorAllocation(state, response), cancellationToken));

    private CommandResult<SiriusAftermathSnapshot> Map(CommandResult<GameState> result)
    {
        if (!result.IsSuccess) return CommandResult<SiriusAftermathSnapshot>.Failure(result.Error!.Code, result.Error.Message);
        if (result.Value!.SiriusAftermath is null) return CommandResult<SiriusAftermathSnapshot>.Failure(CommandErrorCodes.AftermathUnavailable, "The Meridian actuator aftermath has not opened on this journey.");
        return CommandResult<SiriusAftermathSnapshot>.Success(SiriusAftermathSnapshotMapper.Map(result.Value, sessions.Content));
    }
}

internal static class SiriusAftermathSnapshotMapper
{
    public static SiriusAftermathSnapshot Map(GameState state, VerticalSliceContentPack pack)
    {
        SiriusAftermathState aftermath = state.SiriusAftermath!;
        StationEventDefinition stationEvent = pack.StationEvents.Single(item => item.Id == aftermath.Id);
        CrewMemberState noor = Crew(state, "noor-okafor");
        CrewMemberState tomas = Crew(state, "tomas-vale");
        CrewMemberState ilya = Crew(state, "ilya-sato");
        int compact = Standing(state, FactionIds.SiriusCorporateCompact);
        int labor = Standing(state, FactionIds.SiriusLabor);

        IReadOnlyList<CrewConflictAction> conflictActions =
        [
            Conflict(CrewConflictResponse.SupportNoor, "Support Noor", "+2 h · Noor +3 loyalty · Tomas −2 · pressure +15", noor.Available && noor.Loyalty >= 60,
                !noor.Available ? "Noor must be available." : noor.Loyalty < 60 ? $"Requires Noor loyalty 60; current loyalty is {noor.Loyalty}." : "Back corporate custody despite increased crew pressure."),
            Conflict(CrewConflictResponse.SupportTomas, "Support Tomas", "+2 h · Tomas +3 loyalty · Noor −2 · pressure +15", tomas.Available && tomas.Loyalty >= 65,
                !tomas.Available ? "Tomas must be available." : tomas.Loyalty < 65 ? $"Requires Tomas loyalty 65; current loyalty is {tomas.Loyalty}." : "Back labor safety despite increased crew pressure."),
            Conflict(CrewConflictResponse.JointAudit, "Order a joint audit", "+4 h · pressure −20 · Ilya +4 fatigue/+1 loyalty · Noor/Tomas +1", ilya.Available && ilya.Fatigue <= 85,
                !ilya.Available ? "Ilya must be available." : ilya.Fatigue > 85 ? $"Requires Ilya fatigue 85 or below; current fatigue is {ilya.Fatigue}." : "Reconcile corporate and labor ledgers before allocation."),
            Conflict(CrewConflictResponse.CaptainsOrder, "Issue captain’s order", "+1 h · Noor/Tomas −2 loyalty · pressure +25", true, "Always available; ends the hearing without consensus."),
        ];

        bool corporate = aftermath.ForecastDisposition is InformationDisposition.Sealed or InformationDisposition.Corroborated && compact >= 0;
        bool laborSafety = aftermath.ForecastDisposition is InformationDisposition.Corroborated or InformationDisposition.Disclosed && labor > 0;
        bool auditCrew = ilya.Available && ilya.Fatigue <= 90;
        bool jointAudit = aftermath.CrewConflict.Response == CrewConflictResponse.JointAudit;
        long auditCost = jointAudit ? 1_500 : 3_000;
        IReadOnlyList<ActuatorAllocationAction> allocationActions =
        [
            AllocationAction(ActuatorAllocationResponse.CorporatePriority, "Corporate priority", "10 Compact / 2 Wayfarer · −6 wear · +4,000 cr · Compact +4 · labor −4 · trust +2 · +2 h", corporate,
                aftermath.ForecastDisposition is not (InformationDisposition.Sealed or InformationDisposition.Corroborated) ? "Requires a sealed or corroborated forecast." : compact < 0 ? $"Requires non-negative Compact standing; current standing is {compact}." : "Compact production receives the scarce stock."),
            AllocationAction(ActuatorAllocationResponse.LaborSafety, "Labor safety", "10 labor / 2 Wayfarer · −3 wear · +2,000 cr · Compact −4 · labor +5 · +4 h", laborSafety,
                aftermath.ForecastDisposition is not (InformationDisposition.Corroborated or InformationDisposition.Disclosed) ? "Requires a corroborated or disclosed forecast." : labor <= 0 ? $"Requires positive labor standing; current standing is {labor}." : "Meridian safety crews receive the scarce stock."),
            AllocationAction(ActuatorAllocationResponse.AuditedSplit, "Audited split", $"5 Compact / 5 labor / 2 Wayfarer · −5 wear · −{auditCost:N0} cr · Compact +2 · labor +3 · trust +1 · +{(jointAudit ? 4 : 6)} h", auditCrew && state.Money.Value >= auditCost,
                !ilya.Available ? "Ilya must be available." : ilya.Fatigue > 90 ? $"Requires Ilya fatigue 90 or below; current fatigue is {ilya.Fatigue}." : state.Money.Value < auditCost ? $"Costs {auditCost:N0} credits; only {state.Money.Value:N0} are available. Audit costs cannot capitalize into the lien." : jointAudit ? "The prior joint audit reduces cost to 1,500 credits and time to four hours." : "A voluntary audit costs 3,000 credits and six hours."),
        ];

        IReadOnlyList<AftermathCrewPresentation> crew = state.Crew.Select(member =>
        {
            CrewDefinition definition = pack.Crew.Single(item => item.Id == member.Id);
            return new AftermathCrewPresentation(member.Id.Value, pack.Text(definition.NameKey), pack.Text(definition.RoleKey), member.Loyalty, member.Fatigue, member.Available, member.Memories?.LastOrDefault()?.Summary ?? "No persistent memory recorded.");
        }).ToArray();
        IReadOnlyList<OutboundLeadPresentation> leads = aftermath.Leads.Select(lead =>
        {
            OutboundLeadDefinition definition = pack.OutboundLeads.Single(item => item.Id == lead.Id);
            return new OutboundLeadPresentation(lead.Id.Value, pack.Text(definition.TitleKey), pack.Text(definition.DetailKey), lead.Available, lead.LockedReason);
        }).ToArray();

        return new SiriusAftermathSnapshot(
            aftermath.Id.Value,
            pack.Text(stationEvent.TitleKey),
            pack.Text(stationEvent.DetailKey),
            aftermath.Phase,
            aftermath.ForecastDisposition.ToString(),
            $"Day {state.Time.HoursSinceStart / 24:N0} · {state.Time.HoursSinceStart % 24:00}:00 station time",
            state.CommandSequence,
            state.SchemaVersion,
            pack.Version,
            aftermath.CrewConflict.Pressure,
            aftermath.CrewConflict.Outcome,
            aftermath.CrewConflict.Response?.ToString(),
            aftermath.ActuatorUnits,
            aftermath.Phase == SiriusAftermathPhase.Resolved ? aftermath.FinalUnitPrice.Value : aftermath.FrozenUnitPrice.Value,
            aftermath.CorporateUnits,
            aftermath.LaborUnits,
            aftermath.WayfarerUnits,
            aftermath.Allocation?.ToString(),
            aftermath.Outcome,
            state.Ship.DriveWearPercent,
            state.Maintenance?.RepairHistory.LastOrDefault()?.Provenance ?? "No allocation repair committed.",
            state.Money.Value,
            compact,
            labor,
            state.CommercialTrust,
            crew,
            aftermath.Phase == SiriusAftermathPhase.CrewConflict ? conflictActions : [],
            aftermath.Phase == SiriusAftermathPhase.ActuatorAllocation ? allocationActions : [],
            leads);
    }

    private static CrewConflictAction Conflict(CrewConflictResponse response, string label, string consequence, bool available, string explanation) => new(response.ToString(), label, consequence, available, explanation, response);
    private static ActuatorAllocationAction AllocationAction(ActuatorAllocationResponse response, string label, string consequence, bool available, string explanation) => new(response.ToString(), label, consequence, available, explanation, response);
    private static CrewMemberState Crew(GameState state, string id) => state.Crew.Single(item => item.Id.Value == id);
    private static int Standing(GameState state, string id) => state.AllFactionStandings.SingleOrDefault(item => item.FactionId == id)?.Standing ?? 0;
}
