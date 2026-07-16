using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Simulation;

namespace Frontier10052.Infrastructure;

public sealed class JsonGameSaveStore : IGameSaveStore
{
    private readonly string _saveDirectory;
    private readonly JsonSerializerOptions _serializerOptions = GameStateCanonicalizer.CreateSerializerOptions(writeIndented: true);

    public JsonGameSaveStore(string saveDirectory)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
        {
            throw new ArgumentException("A save directory is required.", nameof(saveDirectory));
        }

        _saveDirectory = Path.GetFullPath(saveDirectory);
    }

    public string GetSavePath(string playerKey)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(playerKey))).ToLowerInvariant();
        return Path.Combine(_saveDirectory, $"journey-{digest}.json");
    }

    public ValueTask<CommandResult<bool>> ExistsAsync(string playerKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(CommandResult<bool>.Success(File.Exists(GetSavePath(playerKey))));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(CommandResult<bool>.Failure(CommandErrorCodes.PersistenceFailed, "The local save directory is unavailable. Check server storage permissions and try again."));
        }
    }

    public async ValueTask<CommandResult<GameSaveEnvelope>> LoadAsync(string playerKey, CancellationToken cancellationToken = default)
    {
        string path = GetSavePath(playerKey);
        if (!File.Exists(path))
        {
            return CommandResult<GameSaveEnvelope>.Failure(CommandErrorCodes.GameNotFound, "No active journey belongs to this browser yet.");
        }

        try
        {
            byte[] payload = await File.ReadAllBytesAsync(path, cancellationToken);
            GameSaveEnvelope? envelope = JsonSerializer.Deserialize<GameSaveEnvelope>(payload, _serializerOptions);
            if (envelope is null || envelope.State is null)
            {
                throw new JsonException("The save envelope was empty.");
            }

            string canonical = GameStateCanonicalizer.Serialize(envelope.State);
            string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            bool checksumValid = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(checksum), Encoding.ASCII.GetBytes(envelope.StateChecksum ?? string.Empty));
            if (!checksumValid && envelope.StateSchemaVersion < GameState.CurrentSchemaVersion)
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                JsonElement rawState = document.RootElement.GetProperty("state");
                string legacyCanonical = JsonSerializer.Serialize(rawState, GameStateCanonicalizer.CreateSerializerOptions());
                string legacyChecksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacyCanonical))).ToLowerInvariant();
                checksumValid = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(legacyChecksum), Encoding.ASCII.GetBytes(envelope.StateChecksum ?? string.Empty));
            }

            if (!checksumValid)
            {
                throw new JsonException("The save checksum did not match the canonical state.");
            }

            return CommandResult<GameSaveEnvelope>.Success(envelope);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            await QuarantineAsync(path, cancellationToken);
            return CommandResult<GameSaveEnvelope>.Failure(CommandErrorCodes.SaveCorrupt, "The local journey was damaged and has been quarantined. Start a new commander to recover; the damaged file was preserved for diagnostics.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CommandResult<GameSaveEnvelope>.Failure(CommandErrorCodes.PersistenceFailed, "The local journey could not be read. Check server storage permissions and try again.");
        }
    }

    public async ValueTask<CommandResult<GameSaveEnvelope>> SaveAsync(
        string playerKey,
        GameSaveEnvelope envelope,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        string path = GetSavePath(playerKey);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(_saveDirectory);
            if (!overwrite && File.Exists(path))
            {
                return CommandResult<GameSaveEnvelope>.Failure(CommandErrorCodes.ActiveGameExists, "This browser already owns an active journey. Confirm overwrite to begin again.");
            }

            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, _serializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (!overwrite && File.Exists(path))
            {
                File.Delete(temporaryPath);
                return CommandResult<GameSaveEnvelope>.Failure(CommandErrorCodes.ActiveGameExists, "This browser already owns an active journey. Confirm overwrite to begin again.");
            }

            File.Move(temporaryPath, path, overwrite);
            return CommandResult<GameSaveEnvelope>.Success(envelope);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(temporaryPath);
            return CommandResult<GameSaveEnvelope>.Failure(CommandErrorCodes.PersistenceFailed, "The journey could not be saved atomically. No command was committed; check server storage and try again.");
        }
    }

    private static ValueTask QuarantineAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string quarantinePath = Path.Combine(directory, $"{name}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json");
        File.Move(path, quarantinePath);
        return ValueTask.CompletedTask;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // The orphaned temp file is harmless and can be inspected or cleaned later.
        }
    }
}
