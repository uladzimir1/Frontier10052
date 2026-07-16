using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Operations;

public sealed class StationOperationsService(GameSessionCoordinator sessions) : IStationOperationsService
{
    public async ValueTask<CommandResult<StationOperationsSnapshot>> StartNewGameAsync(string playerKey, string callsign, bool overwriteConfirmed, CancellationToken cancellationToken = default) =>
        Map(await sessions.StartAsync(playerKey, callsign, overwriteConfirmed, cancellationToken));

    public async ValueTask<CommandResult<StationOperationsSnapshot>> ResumeGameAsync(string playerKey, CancellationToken cancellationToken = default) =>
        Map(await sessions.ResumeAsync(playerKey, cancellationToken));

    public ValueTask<CommandResult<StationOperationsSnapshot>> AcknowledgeBriefingAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, GameCommands.AcknowledgeBriefing, cancellationToken);
    public ValueTask<CommandResult<StationOperationsSnapshot>> AcceptContractAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, GameCommands.AcceptContract, cancellationToken);
    public ValueTask<CommandResult<StationOperationsSnapshot>> PurchaseCargoAsync(string playerKey, CommodityId commodityId, Tonnes quantity, CancellationToken cancellationToken = default) => Mutate(playerKey, state => GameCommands.PurchaseCargo(state, commodityId, quantity), cancellationToken);
    public ValueTask<CommandResult<StationOperationsSnapshot>> RecordReportDecisionAsync(string playerKey, ReportDecision decision, CancellationToken cancellationToken = default) => Mutate(playerKey, state => GameCommands.RecordReportDecision(state, decision), cancellationToken);
    public ValueTask<CommandResult<StationOperationsSnapshot>> AssignEngineerAsync(string playerKey, EngineerAssignment assignment, CancellationToken cancellationToken = default) => Mutate(playerKey, state => GameCommands.AssignEngineer(state, assignment), cancellationToken);
    public ValueTask<CommandResult<StationOperationsSnapshot>> AuthorizeDepartureAsync(string playerKey, CancellationToken cancellationToken = default) => Mutate(playerKey, GameCommands.AuthorizeDeparture, cancellationToken);

    private async ValueTask<CommandResult<StationOperationsSnapshot>> Mutate(string key, Func<GameState, CommandResult<GameState>> command, CancellationToken cancellationToken) => Map(await sessions.MutateAsync(key, command, cancellationToken));
    private CommandResult<StationOperationsSnapshot> Map(CommandResult<GameState> result) => result.IsSuccess
        ? CommandResult<StationOperationsSnapshot>.Success(StationSnapshotMapper.Map(result.Value!, sessions.Content))
        : CommandResult<StationOperationsSnapshot>.Failure(result.Error!.Code, result.Error.Message);
}
