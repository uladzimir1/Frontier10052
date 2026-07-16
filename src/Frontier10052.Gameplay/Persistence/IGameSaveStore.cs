using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Persistence;

public interface IGameSaveStore
{
    ValueTask<CommandResult<bool>> ExistsAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<GameSaveEnvelope>> LoadAsync(string playerKey, CancellationToken cancellationToken = default);
    ValueTask<CommandResult<GameSaveEnvelope>> SaveAsync(string playerKey, GameSaveEnvelope envelope, bool overwrite, CancellationToken cancellationToken = default);
}

public sealed record GameSaveEnvelope(
    string GameVersion,
    string ContentPackVersion,
    int StateSchemaVersion,
    int Seed,
    long CommandSequence,
    string StateChecksum,
    GameState State)
{
    public const string CurrentGameVersion = "0.1.0";
}
