using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Turnaround;

public sealed class TurnaroundService(GameSessionCoordinator sessions) : ITurnaroundService
{
    public async ValueTask<CommandResult<TurnaroundSnapshot>> ResumeMarsOperationsAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Map(await sessions.ResumeAsync(playerKey, cancellationToken));

    public ValueTask<CommandResult<TurnaroundSnapshot>> ServiceLienAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, TurnaroundCommands.ServiceLien, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> DeferLienAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, TurnaroundCommands.DeferLien, cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> RefuelAsync(string playerKey, int percentagePoints, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state => TurnaroundCommands.Refuel(state, percentagePoints), cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> RepairAsync(string playerKey, RepairService service, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state => TurnaroundCommands.Repair(state, service), cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> RestCrewAsync(string playerKey, CrewRestService service, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state => TurnaroundCommands.RestCrew(state, service), cancellationToken);

    public ValueTask<CommandResult<TurnaroundSnapshot>> SelectContractAsync(string playerKey, ContractId contractId, CancellationToken cancellationToken = default)
    {
        ContractDefinition? definition = sessions.Content.Contracts.SingleOrDefault(item => item.Id == contractId && item.IsTurnaroundOffer);
        if (definition is null) return ValueTask.FromResult(CommandResult<TurnaroundSnapshot>.Failure(CommandErrorCodes.ContractUnavailable, "That contract is not in the Mars turnaround content collection."));
        return Mutate(playerKey, state => TurnaroundCommands.SelectContract(state, contractId, definition.LegalExposureOnAccept), cancellationToken);
    }

    public ValueTask<CommandResult<TurnaroundSnapshot>> AuthorizeDepartureAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            ContractId? selected = state.Turnaround?.SelectedContractId;
            ContractDefinition? contract = selected is null ? null : sessions.Content.Contracts.SingleOrDefault(item => item.Id == selected.Value);
            RouteDefinition? route = contract is null ? null : sessions.Content.Routes.SingleOrDefault(item => item.OriginStationId == contract.OriginStationId && item.DestinationStationId == contract.DestinationStationId);
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
        route.Id.Value);

    private async ValueTask<CommandResult<TurnaroundSnapshot>> Mutate(string key, Func<GameState, CommandResult<GameState>> command, CancellationToken cancellationToken) =>
        Map(await sessions.MutateAsync(key, command, cancellationToken));

    private CommandResult<TurnaroundSnapshot> Map(CommandResult<GameState> result) => result.IsSuccess
        ? TurnaroundSnapshotMapper.TryMap(result.Value!, sessions.Content)
        : CommandResult<TurnaroundSnapshot>.Failure(result.Error!.Code, result.Error.Message);
}
