using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Journey;

public sealed class JourneyService(GameSessionCoordinator sessions) : IJourneyService
{
    public async ValueTask<CommandResult<JourneySnapshot>> ResumeAsync(string playerKey, CancellationToken cancellationToken = default) => Map(await sessions.ResumeAsync(playerKey, cancellationToken));

    public ValueTask<CommandResult<JourneySnapshot>> BeginVoyageAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            RouteTravelState? authorized = state.Journey?.Phase == JourneyPhase.DepartureAuthorized ? state.Journey.Route : null;
            if (authorized is not null) return JourneyCommands.BeginVoyage(state, authorized);
            RouteDefinition? definition = sessions.Content.Routes.SingleOrDefault(item => item.OriginStationId == state.Ship.StationId && item.DestinationStationId == state.Contract.DestinationStationId);
            return definition is null
                ? CommandResult<GameState>.Failure(CommandErrorCodes.RouteUnavailable, "No authored route connects the current station to the active contract destination.")
                : JourneyCommands.BeginVoyage(state, CreateRoute(definition, state.Time));
        }, cancellationToken);

    public ValueTask<CommandResult<JourneySnapshot>> AdvanceVoyageAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Mutate(playerKey, state =>
        {
            RouteDefinition? route = state.Journey?.Route is null ? null : sessions.Content.Routes.SingleOrDefault(item => item.Id == state.Journey.Route.Id);
            return route is null
                ? CommandResult<GameState>.Failure(CommandErrorCodes.RouteUnavailable, "The active journey route is missing from the content pack.")
                : JourneyCommands.AdvanceVoyage(state, route.EncounterPool);
        }, cancellationToken);

    public ValueTask<CommandResult<JourneySnapshot>> ResolveCurrentEncounterAsync(string playerKey, EncounterResponse response, CancellationToken cancellationToken = default) => Mutate(playerKey, state => JourneyCommands.ResolveCurrentEncounter(state, response), cancellationToken);
    public ValueTask<CommandResult<JourneySnapshot>> ResolveCheckpointAsync(string playerKey, CheckpointResponse? response = null, CancellationToken cancellationToken = default) => Mutate(playerKey, state => JourneyCommands.ResolveNextCheckpoint(state, response), cancellationToken);
    public ValueTask<CommandResult<JourneySnapshot>> ArriveAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, JourneyCommands.Arrive, cancellationToken);
    public ValueTask<CommandResult<JourneySnapshot>> SellCargoAsync(string playerKey, CommodityId commodityId, Tonnes quantity, CancellationToken cancellationToken = default) => Mutate(playerKey, state => JourneyCommands.SellCargo(state, commodityId, quantity), cancellationToken);
    public ValueTask<CommandResult<JourneySnapshot>> DeliverCargoAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, state => JourneyCommands.DeliverCargo(
        state,
        state.Journey?.VoyageNumber switch
        {
            1 => sessions.CreateTurnaroundOffers(state.Time),
            2 => [sessions.CreateSiriusOffer(state.Time, state.Ship.StationId)],
            _ => null,
        }), cancellationToken);
    public ValueTask<CommandResult<JourneySnapshot>> ClearSiriusCustomsAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, JourneyCommands.ClearSiriusCustoms, cancellationToken);
    public ValueTask<CommandResult<JourneySnapshot>> SettleInformationContractAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, JourneyCommands.SettleInformationContract, cancellationToken);

    private static RouteTravelState CreateRoute(RouteDefinition route, GameTime time) => new(
        route.Id, route.OriginStationId, route.DestinationStationId, route.DurationHours, route.FuelCostPercent, route.DriveWearPercent,
        route.EncounterAtHour, time, time.AddHours(route.DurationHours), 0, 0, route.Id.Value);

    private async ValueTask<CommandResult<JourneySnapshot>> Mutate(string key, Func<GameState, CommandResult<GameState>> command, CancellationToken cancellationToken) => Map(await sessions.MutateAsync(key, command, cancellationToken));
    private CommandResult<JourneySnapshot> Map(CommandResult<GameState> result) => result.IsSuccess
        ? CommandResult<JourneySnapshot>.Success(JourneySnapshotMapper.Map(result.Value!, sessions.Content))
        : CommandResult<JourneySnapshot>.Failure(result.Error!.Code, result.Error.Message);
}
