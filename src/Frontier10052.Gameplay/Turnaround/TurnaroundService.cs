using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Turnaround;

public sealed class TurnaroundService(GameSessionCoordinator sessions) : ITurnaroundService
{
    public async ValueTask<CommandResult<TurnaroundSnapshot>> ResumeAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Map(await sessions.ResumeAsync(playerKey, cancellationToken));

    public ValueTask<CommandResult<TurnaroundSnapshot>> ResumeMarsOperationsAsync(string playerKey, CancellationToken cancellationToken = default) =>
        ResumeAsync(playerKey, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> ServiceLienAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, TurnaroundCommands.ServiceLien, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> DeferLienAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, TurnaroundCommands.DeferLien, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> RefuelAsync(string playerKey, int percentagePoints, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            StationServiceDefinition? service = ServiceAt(state.Ship.StationId);
            return service is null
                ? CommandResult<GameState>.Failure(CommandErrorCodes.TurnaroundUnavailable, "The current station has no authored fuel service.")
                : TurnaroundCommands.Refuel(state, percentagePoints, service.FuelUnitCost);
        }, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> ChargePinchAsync(string playerKey, int points, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            StationServiceDefinition? service = ServiceAt(state.Ship.StationId);
            return service is null || service.PinchUnitCost <= 0
                ? CommandResult<GameState>.Failure(CommandErrorCodes.TurnaroundUnavailable, "The current station has no metric-drive pinch service.")
                : TurnaroundCommands.ChargePinch(state, points, service.PinchUnitCost);
        }, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> RepairAsync(string playerKey, RepairService service, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            StationServiceDefinition? stationService = ServiceAt(state.Ship.StationId);
            StationDefinition? station = sessions.Content.Stations.SingleOrDefault(item => item.Id == state.Ship.StationId);
            return stationService is null || station is null
                ? CommandResult<GameState>.Failure(CommandErrorCodes.TurnaroundUnavailable, "The current station has no authored repair service.")
                : TurnaroundCommands.Repair(state, service, stationService.CertifiedRepairCost, stationService.FieldRepairCost, sessions.Content.Text(station.NameKey));
        }, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> RestCrewAsync(string playerKey, CrewRestService service, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state => TurnaroundCommands.RestCrew(state, service), cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> SelectContractAsync(string playerKey, ContractId contractId, CancellationToken cancellationToken = default)
    {
        ContractDefinition? definition = sessions.Content.Contracts.SingleOrDefault(item => item.Id == contractId && item.IsTurnaroundOffer);
        if (definition is null) return ValueTask.FromResult(CommandResult<TurnaroundSnapshot>.Failure(CommandErrorCodes.ContractUnavailable, "That contract is not in the station turnaround content collection."));
        return Mutate(playerKey, state =>
        {
            InformationItemState? information = null;
            if (definition.InformationId is InformationId informationId)
            {
                InformationDefinition authored = sessions.Content.Information.Single(item => item.Id == informationId);
                GameTime observedAt = state.AllContracts.Single(item => item.Id == definition.Id).OfferedAt ?? state.Time;
                information = new InformationItemState(
                    authored.Id,
                    sessions.Content.Text(authored.TitleKey),
                    InformationDisposition.Sealed,
                    authored.ConfidencePercent,
                    [new InformationProvenanceState(sessions.Content.Text(authored.SourceKey), observedAt, authored.ConfidencePercent, sessions.Content.Text(authored.ProvenanceKey))]);
            }
            return TurnaroundCommands.SelectContract(state, contractId, definition.LegalExposureOnAccept, information);
        }, cancellationToken);
    }

    public ValueTask<CommandResult<TurnaroundSnapshot>> AuthorizeDepartureAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            ContractId? selected = state.Turnaround?.SelectedContractId;
            ContractDefinition? contract = selected is null ? null : sessions.Content.Contracts.SingleOrDefault(item => item.Id == selected.Value);
            RouteDefinition? route = contract is null ? null : sessions.Content.Routes.SingleOrDefault(item => item.OriginStationId == state.Ship.StationId && item.DestinationStationId == contract.DestinationStationId);
            return route is null
                ? CommandResult<GameState>.Failure(CommandErrorCodes.ContractSelectionRequired, "Select a Mars contract before authorizing its route.")
                : TurnaroundCommands.AuthorizeDeparture(state, CreateRoute(route, state.Time));
        }, cancellationToken);

    private static RouteTravelState CreateRoute(RouteDefinition route, GameTime time) => new(
        route.Id,
        route.OriginStationId,
        route.DestinationStationId,
        route.DurationHours,
        route.FuelCostPercent,
        route.DriveWearPercent,
        route.EncounterAtHour,
        time,
        time.AddHours(route.DurationHours),
        0,
        0,
        route.Id.Value,
        route.PinchCost,
        route.Checkpoints?.Select(item => new RouteCheckpointState(
            item.Id,
            item.Kind switch
            {
                RouteCheckpointDefinitionKind.Undock => RouteCheckpointKind.Undock,
                RouteCheckpointDefinitionKind.GravityBoundary => RouteCheckpointKind.GravityBoundary,
                RouteCheckpointDefinitionKind.DelayedMessage => RouteCheckpointKind.DelayedMessage,
                RouteCheckpointDefinitionKind.LatticeDrift => RouteCheckpointKind.LatticeDrift,
                _ => RouteCheckpointKind.Approach,
            },
            item.ScheduledHour,
            CheckpointResolutionStatus.Pending,
            null,
            null,
            string.Empty)).ToArray());

    private StationServiceDefinition? ServiceAt(StationId stationId) => sessions.Content.StationServices.SingleOrDefault(item => item.StationId == stationId);

    private async ValueTask<CommandResult<TurnaroundSnapshot>> Mutate(string key, Func<GameState, CommandResult<GameState>> command, CancellationToken cancellationToken) =>
        Map(await sessions.MutateAsync(key, command, cancellationToken));

    private CommandResult<TurnaroundSnapshot> Map(CommandResult<GameState> result) => result.IsSuccess
        ? TurnaroundSnapshotMapper.TryMap(result.Value!, sessions.Content)
        : CommandResult<TurnaroundSnapshot>.Failure(result.Error!.Code, result.Error.Message);
}
