using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Operations;

public interface IStationOperationsService
{
    ValueTask<CommandResult<StationOperationsSnapshot>> StartNewGameAsync(string playerKey, string callsign, bool overwriteConfirmed, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> ResumeGameAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> AcknowledgeBriefingAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> AcceptContractAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> PurchaseCargoAsync(string playerKey, CommodityId commodityId, Tonnes quantity, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> RecordReportDecisionAsync(string playerKey, ReportDecision decision, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> AssignEngineerAsync(string playerKey, EngineerAssignment assignment, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<StationOperationsSnapshot>> AuthorizeDepartureAsync(string playerKey, CancellationToken cancellationToken = default);
}
