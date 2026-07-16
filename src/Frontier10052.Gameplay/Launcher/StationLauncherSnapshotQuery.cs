using Frontier10052.Domain;
using Frontier10052.Gameplay.Operations;

namespace Frontier10052.Gameplay.Launcher;

public sealed class StationLauncherSnapshotQuery(IStationOperationsService operations) : ILauncherSnapshotQuery
{
    public async ValueTask<LauncherSnapshot> GetAsync(string? playerKey, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(playerKey))
        {
            CommandResult<StationOperationsSnapshot> result = await operations.ResumeGameAsync(playerKey, cancellationToken);
            if (result.IsSuccess)
            {
                return FromSave(result.Value!);
            }

            if (result.Error?.Code != CommandErrorCodes.GameNotFound)
            {
                return Showcase(result.Error?.Message);
            }
        }

        return Showcase(null);
    }

    private static LauncherSnapshot FromSave(StationOperationsSnapshot snapshot) => new(
        new CommanderSnapshot(snapshot.Commander.DisplayName, snapshot.Commander.Career, snapshot.Commander.Initials),
        new ShipSnapshot(
            snapshot.Ship.Name,
            snapshot.Ship.Hull,
            snapshot.Ship.CargoLoaded,
            snapshot.Ship.CargoCapacity,
            snapshot.Ship.FuelPercent,
            snapshot.Ship.DriveWearPercent,
            $"Local journey · command {snapshot.CommandSequence}"),
        new DockSnapshot(
            snapshot.LocationName,
            snapshot.LocationDetail,
            snapshot.JourneyPhase switch
            {
                nameof(Simulation.JourneyPhase.InTransit) or nameof(Simulation.JourneyPhase.EncounterPending) => $"{snapshot.Contract.Origin}–{snapshot.Contract.Destination} transfer orbit",
                nameof(Simulation.JourneyPhase.Approach) => "Destination approach corridor",
                nameof(Simulation.JourneyPhase.Docked) => "Station cargo berth",
                nameof(Simulation.JourneyPhase.Turnaround) => "Mars turnaround berth M-14",
                nameof(Simulation.JourneyPhase.Delivered) => "Destination settlement berth",
                _ => "Exchange berth E-07",
            },
            snapshot.GameTimeLabel,
            snapshot.JourneyPhase switch
            {
                nameof(Simulation.JourneyPhase.DepartureAuthorized) => "Departure authorized · awaiting undock",
                nameof(Simulation.JourneyPhase.InTransit) => "Wayfarer in transit",
                nameof(Simulation.JourneyPhase.EncounterPending) => "Transit contact · response required",
                nameof(Simulation.JourneyPhase.Approach) => "Destination docking corridor assigned",
                nameof(Simulation.JourneyPhase.Docked) => "Destination cargo custody ready",
                nameof(Simulation.JourneyPhase.Turnaround) => "Mars debt, service, crew, and contract turnaround",
                nameof(Simulation.JourneyPhase.Delivered) => "Second contract settled · vertical slice complete",
                _ => "Station services connected",
            }),
        snapshot.Crew.Select(member => new CrewActivitySnapshot(member.Name, member.Role, member.Assignment, member.Status)).ToArray(),
        [
            new LauncherNoticeSnapshot("Contract", snapshot.Contract.Title, snapshot.Contract.Accepted ? $"{snapshot.Contract.Destination} · {snapshot.Contract.Quantity} sealed tonnes" : "Awaiting commander acceptance"),
            new LauncherNoticeSnapshot("Lien", $"{snapshot.LienBalance:N0}-credit inherited balance", $"Repair condition: {snapshot.RepairCondition}"),
            new LauncherNoticeSnapshot("Consequences", $"Legal exposure {snapshot.LegalExposure}", snapshot.ImportantConsequences),
            new LauncherNoticeSnapshot("Patch 0.3", "Mars turnaround and second voyage", "Debt, service, crew memories, faction standing, and both destination branches are online."),
        ],
        true,
        true,
        null,
        $"{snapshot.ContentPackVersion} · save schema {snapshot.SchemaVersion} · seed {snapshot.Seed}",
        snapshot.ContinuePath,
        snapshot.LienBalance,
        snapshot.RepairCondition,
        snapshot.LegalExposure,
        snapshot.ImportantConsequences);

    private static LauncherSnapshot Showcase(string? recoverableError) => new(
        new CommanderSnapshot("Cmdr. Alex Mercer", "Showcase commander · no local save", "AM"),
        new ShipSnapshot(
            "Wayfarer",
            "Tern-class modular freighter",
            38,
            72,
            84,
            17,
            "No browser journey found"),
        new DockSnapshot(
            "Pluto Gateway",
            "Sol Heritage Zone",
            "Freight berth K-12",
            "03:17 station time",
            "Showcase state · start a new commander"),
        [
            new CrewActivitySnapshot("Mara Venn", "Pilot", "Reviewing departure vectors", "Ready"),
            new CrewActivitySnapshot("Ilya Sato", "Engineer", "Supervising fuel coupling", "On task"),
            new CrewActivitySnapshot("Noor Okafor", "Security", "Checking cargo seals", "Clear"),
            new CrewActivitySnapshot("Tomas Vale", "Medic", "Restocking the infirmary", "Available"),
        ],
        [
            new LauncherNoticeSnapshot("News", "Gate Authority revises outbound manifests", "New inspection windows take effect after the next courier arrival."),
            new LauncherNoticeSnapshot("Patch 0.2", "Earth-to-Mars onboarding ready", "Create a local commander to enter Earth Heritage Station operations."),
        ],
        false,
        false,
        recoverableError,
        "vertical-slice-v2 · save schema 3 · ready for a new commander",
        "/operations",
        72_000,
        "83% drive condition · no service record",
        0,
        "Start a commander to create persistent crew, faction, and journey history.");
}
