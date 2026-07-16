using Frontier10052.Content;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Journey;

internal static class CinematicPresentationMapper
{
    public static CinematicPresentation MapCurrent(GameState state, JourneyState journey, RouteDefinition route, VerticalSliceContentPack pack)
    {
        if (journey.Phase == JourneyPhase.DockedAtOrigin)
        {
            return Cue(state, route, route.OriginStationId.Value, "departure-berth-preview", "departure-preview", null,
                $"Wayfarer rests at {Station(route.OriginStationId, pack)} while the crew turns the berth, route, and ship into one readable departure picture.");
        }

        if (journey.Phase == JourneyPhase.DepartureAuthorized)
        {
            return Cue(state, route, route.OriginStationId.Value, "departure-preview", "departure-preview", null,
                $"Wayfarer holds at {Station(route.OriginStationId, pack)} with the route manifest locked and every departure consequence still under command authority.");
        }

        if (journey.Phase is JourneyPhase.Docked or JourneyPhase.CustomsPending or JourneyPhase.Turnaround or JourneyPhase.Delivered)
        {
            return Cue(state, route, route.DestinationStationId.Value, "docking-handoff", "docking-handoff", null,
                $"Capture, seal, and custody handoff complete at {Station(route.DestinationStationId, pack)}.", "/operations");
        }

        if (journey.Phase == JourneyPhase.Approach)
        {
            return Cue(state, route, route.DestinationStationId.Value, "destination-approach", "destination-approach", LastResolvedCheckpoint(journey.Route)?.Id.Value,
                $"{Station(route.DestinationStationId, pack)} assigns Wayfarer a controlled approach corridor. Arrival remains committed separately from docking.");
        }

        if (journey.Phase == JourneyPhase.EncounterPending && journey.Encounter is not null)
        {
            EncounterDefinition encounter = pack.Encounters.Single(item => item.Id == journey.Encounter.Id);
            return Cue(state, route, route.DestinationStationId.Value, $"encounter-{encounter.Id.Value}-arrival", "encounter-arrival", encounter.Id.Value,
                $"{pack.Text(encounter.TitleKey)}. {pack.Text(encounter.DetailKey)} Simulation time is paused for command.");
        }

        if (journey.Encounter?.Status == EncounterStatus.Resolved)
        {
            EncounterDefinition encounter = pack.Encounters.Single(item => item.Id == journey.Encounter.Id);
            string response = journey.Encounter.Response?.ToString() ?? "resolved";
            return Cue(state, route, route.DestinationStationId.Value, $"encounter-{encounter.Id.Value}-{response}", "response-outcome", encounter.Id.Value,
                $"{pack.Text(encounter.TitleKey)} resolved. {journey.Encounter.Outcome} Wayfarer returns to the transfer profile.");
        }

        RouteCheckpointState? checkpoint = LastResolvedCheckpoint(journey.Route);
        if (checkpoint is not null)
        {
            return MapCheckpoint(state, route, checkpoint, pack);
        }

        return Cue(state, route, route.OriginStationId.Value, "clamp-release-undock", "clamp-release-undock", null,
            $"Clamps release at {Station(route.OriginStationId, pack)}. Wayfarer rotates clear before committing the transfer burn.");
    }

    public static CinematicPresentation MapCheckpoint(GameState state, RouteDefinition route, RouteCheckpointState checkpoint, VerticalSliceContentPack pack)
    {
        string environment = checkpoint.Kind == RouteCheckpointKind.Approach
            ? route.DestinationStationId.Value
            : route.OriginStationId.Value;
        string stage = checkpoint.Kind switch
        {
            RouteCheckpointKind.Undock => "clamp-release-undock",
            RouteCheckpointKind.GravityBoundary => "gravity-boundary-burn",
            RouteCheckpointKind.DelayedMessage => "delayed-labor-warning",
            RouteCheckpointKind.LatticeDrift => "pinch-lattice-drift",
            RouteCheckpointKind.Approach => "destination-approach",
            _ => "transfer-montage",
        };
        string response = checkpoint.Response?.ToString() ?? checkpoint.Status.ToString().ToLowerInvariant();
        RouteCheckpointDefinition authored = route.Checkpoints!.Single(item => item.Id == checkpoint.Id);
        string outcome = string.IsNullOrWhiteSpace(checkpoint.Outcome) ? pack.Text(authored.DetailKey) : checkpoint.Outcome;
        return Cue(state, route, environment, $"checkpoint-{checkpoint.Id.Value}-{response}", stage, checkpoint.Id.Value,
            $"{pack.Text(authored.TitleKey)}. {outcome}");
    }

    public static CinematicPresentation MapEncounter(GameState state, RouteDefinition route, EncounterState encounter, VerticalSliceContentPack pack)
    {
        EncounterDefinition authored = pack.Encounters.Single(item => item.Id == encounter.Id);
        string stage = encounter.Status == EncounterStatus.Pending ? "encounter-arrival" : "response-outcome";
        string response = encounter.Response?.ToString() ?? encounter.Status.ToString().ToLowerInvariant();
        string caption = encounter.Status == EncounterStatus.Pending
            ? $"{pack.Text(authored.TitleKey)}. {pack.Text(authored.DetailKey)}"
            : $"{pack.Text(authored.TitleKey)} resolved. {encounter.Outcome}";
        return Cue(state, route, route.DestinationStationId.Value, $"encounter-{encounter.Id.Value}-{response}", stage, encounter.Id.Value, caption);
    }

    private static RouteCheckpointState? LastResolvedCheckpoint(RouteTravelState? route) =>
        route?.AllCheckpoints.LastOrDefault(item => item.Status == CheckpointResolutionStatus.Resolved);

    private static CinematicPresentation Cue(
        GameState state,
        RouteDefinition route,
        string stationId,
        string cueSuffix,
        string stage,
        string? eventId,
        string caption,
        string continuationPath = "/travel")
    {
        (int duration, int scroll) = stage switch
        {
            "gravity-boundary-burn" or "pinch-lattice-drift" or "docking-handoff" => (14, 340),
            "encounter-arrival" or "response-outcome" or "delayed-labor-warning" or "transfer-montage" => (9, 240),
            _ => (12, 300),
        };
        string environment = Environment(stationId);
        return new CinematicPresentation(
            $"{route.Id.Value}:{cueSuffix}",
            stage,
            environment,
            route.Id.Value,
            eventId,
            state.CommandSequence,
            duration,
            scroll,
            caption,
            $"assets/cinematic/plates/{environment}.webp",
            continuationPath);
    }

    private static string Station(Frontier10052.Domain.StationId id, VerticalSliceContentPack pack) =>
        pack.Text(pack.Stations.Single(item => item.Id == id).NameKey);

    private static string Environment(string stationId) => stationId switch
    {
        "earth-heritage-station" => "earth",
        "mars-industrial-port" => "mars",
        "ceres-freehold-anchorage" => "ceres",
        "pluto-gateway" => "pluto",
        "sirius-meridian-exchange" => "sirius",
        _ => throw new InvalidOperationException($"No cinematic environment is registered for station '{stationId}'."),
    };
}
