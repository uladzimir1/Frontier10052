namespace Frontier10052.Gameplay.Launcher;

/// <summary>
/// Supplies the read-only state required to render the in-world launcher.
/// </summary>
public interface ILauncherSnapshotQuery
{
    ValueTask<LauncherSnapshot> GetAsync(string? playerKey, CancellationToken cancellationToken = default);
}

public sealed record LauncherSnapshot(
    CommanderSnapshot Commander,
    ShipSnapshot Ship,
    DockSnapshot Dock,
    IReadOnlyList<CrewActivitySnapshot> Crew,
    IReadOnlyList<LauncherNoticeSnapshot> Notices,
    bool CanContinue,
    bool HasPlayerSave,
    string? RecoverableError,
    string CompatibilityMessage,
    string ContinuePath,
    long LienBalance = 72_000,
    string RepairCondition = "No service record",
    int LegalExposure = 0,
    string ImportantConsequences = "No persistent consequences recorded yet.",
    string CurrentCheckpoint = "Docked",
    int SiriusCompactStanding = 0,
    int SiriusLaborStanding = 0);

public sealed record CommanderSnapshot(
    string DisplayName,
    string Career,
    string Initials);

public sealed record ShipSnapshot(
    string Name,
    string Hull,
    int CargoLoaded,
    int CargoCapacity,
    int FuelPercent,
    int DriveWearPercent,
    string LastSaveLabel,
    int PinchReserve = 0);

public sealed record DockSnapshot(
    string Station,
    string System,
    string Berth,
    string LocalTimeLabel,
    string TrafficState);

public sealed record CrewActivitySnapshot(
    string Name,
    string Role,
    string Activity,
    string Status);

public sealed record LauncherNoticeSnapshot(
    string Category,
    string Title,
    string Detail);
