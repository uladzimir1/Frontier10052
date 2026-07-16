using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Journey;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Simulation;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class CinematicPresentationTests
{
    private readonly VerticalSliceContentPack _pack = VerticalSliceContentPack.Create();

    [TestMethod]
    public void EveryAuthoredRouteMapsPreviewUndockApproachAndCommittedDocking()
    {
        HashSet<string> cueIds = new(StringComparer.Ordinal);

        foreach (RouteDefinition route in _pack.Routes)
        {
            GameState state = State(route, commandSequence: 41);
            RouteTravelState travel = Travel(route);
            JourneyState journey = state.Journey! with { Route = travel };

            CinematicPresentation berthPreview = CinematicPresentationMapper.MapCurrent(state, journey with { Phase = JourneyPhase.DockedAtOrigin }, route, _pack);
            CinematicPresentation preview = CinematicPresentationMapper.MapCurrent(state, journey with { Phase = JourneyPhase.DepartureAuthorized }, route, _pack);
            CinematicPresentation undock = CinematicPresentationMapper.MapCurrent(state, journey with { Phase = JourneyPhase.InTransit, Route = ResolveUndock(travel) }, route, _pack);
            CinematicPresentation approach = CinematicPresentationMapper.MapCurrent(state, journey with { Phase = JourneyPhase.Approach, Route = ResolveApproach(travel) }, route, _pack);
            CinematicPresentation docking = CinematicPresentationMapper.MapCurrent(state, journey with { Phase = JourneyPhase.Docked, DockedStationId = route.DestinationStationId }, route, _pack);

            Assert.AreEqual("departure-preview", berthPreview.Stage);
            Assert.AreEqual("departure-preview", preview.Stage);
            Assert.AreEqual("clamp-release-undock", undock.Stage);
            Assert.AreEqual("destination-approach", approach.Stage);
            Assert.AreEqual("docking-handoff", docking.Stage);
            Assert.AreEqual("/operations", docking.ContinuationPath);
            Assert.AreEqual(14, docking.DurationSeconds);
            Assert.AreEqual(340, docking.ScrollLengthVh);
            Assert.IsTrue(preview.FallbackPlate.EndsWith(".webp", StringComparison.Ordinal));
            Assert.AreEqual(state.CommandSequence, preview.CommandSequence);
            Assert.IsTrue(cueIds.Add(berthPreview.CueId), $"Duplicate cue {berthPreview.CueId}");
            Assert.IsTrue(cueIds.Add(preview.CueId), $"Duplicate cue {preview.CueId}");
            Assert.IsTrue(cueIds.Add(undock.CueId), $"Duplicate cue {undock.CueId}");
            Assert.IsTrue(cueIds.Add(approach.CueId), $"Duplicate cue {approach.CueId}");
            Assert.IsTrue(cueIds.Add(docking.CueId), $"Duplicate cue {docking.CueId}");
        }

        Assert.HasCount(_pack.Routes.Count * 5, cueIds);
    }

    [TestMethod]
    public void EveryLegacyEncounterAndResponseHasArrivalAndOutcomeCues()
    {
        RouteDefinition route = _pack.Routes[0];
        GameState state = State(route, commandSequence: 52);
        HashSet<string> cues = new(StringComparer.Ordinal);

        foreach (EncounterDefinition definition in _pack.Encounters)
        {
            EncounterState pending = new(definition.Id, EncounterStatus.Pending, state.Time, null, string.Empty);
            CinematicPresentation arrival = CinematicPresentationMapper.MapEncounter(state, route, pending, _pack);
            Assert.AreEqual("encounter-arrival", arrival.Stage);
            Assert.AreEqual(definition.Id.Value, arrival.EventId);
            Assert.IsTrue(cues.Add(arrival.CueId));

            foreach (EncounterOptionDefinition option in definition.Options)
            {
                EncounterResponse response = Enum.Parse<EncounterResponse>(option.ResponseId);
                EncounterState resolved = pending with { Status = EncounterStatus.Resolved, Response = response, Outcome = $"{response} committed." };
                CinematicPresentation outcome = CinematicPresentationMapper.MapEncounter(state, route, resolved, _pack);
                Assert.AreEqual("response-outcome", outcome.Stage);
                Assert.AreEqual(9, outcome.DurationSeconds);
                Assert.AreEqual(240, outcome.ScrollLengthVh);
                Assert.IsTrue(cues.Add(outcome.CueId), $"Duplicate encounter cue {outcome.CueId}");
            }
        }

        Assert.HasCount(_pack.Encounters.Count + _pack.Encounters.Sum(item => item.Options.Count), cues);
    }

    [TestMethod]
    public void BothSiriusVariantsMapEveryCheckpointAndChoiceToReplayableMetadata()
    {
        RouteDefinition[] routes = _pack.Routes.Where(item => item.Checkpoints is { Count: 5 }).ToArray();
        HashSet<string> cues = new(StringComparer.Ordinal);

        foreach (RouteDefinition route in routes)
        {
            GameState state = State(route, commandSequence: 63);
            foreach (RouteCheckpointDefinition authored in route.Checkpoints!)
            {
                CheckpointResponse?[] responses = authored.Kind switch
                {
                    RouteCheckpointDefinitionKind.DelayedMessage =>
                        [CheckpointResponse.PreserveSeal, CheckpointResponse.CorroborateWarning, CheckpointResponse.LeakWarning],
                    RouteCheckpointDefinitionKind.LatticeDrift =>
                        [CheckpointResponse.IlyaRecalibration, CheckpointResponse.MaraPinchCorrection],
                    _ => [null],
                };

                foreach (CheckpointResponse? response in responses)
                {
                    RouteCheckpointState checkpoint = new(
                        authored.Id,
                        Enum.Parse<RouteCheckpointKind>(authored.Kind.ToString()),
                        authored.ScheduledHour,
                        CheckpointResolutionStatus.Resolved,
                        state.Time,
                        response,
                        $"{authored.Kind} outcome committed.");
                    CinematicPresentation cue = CinematicPresentationMapper.MapCheckpoint(state, route, checkpoint, _pack);

                    Assert.AreEqual(authored.Id.Value, cue.EventId);
                    Assert.AreEqual(route.Id.Value, cue.RouteId);
                    Assert.AreEqual(state.CommandSequence, cue.CommandSequence);
                    Assert.IsTrue(cues.Add(cue.CueId), $"Duplicate checkpoint cue {cue.CueId}");
                }
            }
        }

        Assert.HasCount(16, cues);
        CollectionAssert.AreEquivalent(
            new[] { "clamp-release-undock", "gravity-boundary-burn", "delayed-labor-warning", "pinch-lattice-drift", "destination-approach" },
            routes[0].Checkpoints!.Select(checkpoint => CinematicPresentationMapper.MapCheckpoint(
                State(routes[0], 64),
                routes[0],
                new RouteCheckpointState(checkpoint.Id, Enum.Parse<RouteCheckpointKind>(checkpoint.Kind.ToString()), checkpoint.ScheduledHour, CheckpointResolutionStatus.Resolved, new GameTime(0), null, "resolved"),
                _pack).Stage).ToArray());
    }

    [TestMethod]
    public void DerivationReplayAndReloadKeysNeverMutateCanonicalState()
    {
        RouteDefinition route = _pack.Routes.Single(item => item.Id.Value == "earth-mars-relief-corridor");
        GameState state = State(route, commandSequence: 77);
        EncounterState encounter = new(new EncounterId("sol-transit-inspection"), EncounterStatus.Resolved, state.Time, EncounterResponse.InspectionStandardCompliance, "Inspection complete.");
        JourneyState journey = state.Journey! with { Phase = JourneyPhase.InTransit, Route = Travel(route), Encounter = encounter };
        state = state with { Journey = journey };
        string before = GameStateCanonicalizer.Serialize(state);
        long creditsBefore = state.Money.Value;
        GameTime timeBefore = state.Time;
        int fuelBefore = state.Ship.FuelPercent;
        int wearBefore = state.Ship.DriveWearPercent;
        long sequenceBefore = state.CommandSequence;

        CinematicPresentation first = CinematicPresentationMapper.MapCurrent(state, journey, route, _pack);
        CinematicPresentation replay = CinematicPresentationMapper.MapCurrent(state, journey, route, _pack);
        string after = GameStateCanonicalizer.Serialize(state);

        Assert.AreEqual(first, replay);
        Assert.AreEqual(before, after);
        Assert.AreEqual(creditsBefore, state.Money.Value);
        Assert.AreEqual(timeBefore, state.Time);
        Assert.AreEqual(fuelBefore, state.Ship.FuelPercent);
        Assert.AreEqual(wearBefore, state.Ship.DriveWearPercent);
        Assert.AreEqual(sequenceBefore, state.CommandSequence);
        Assert.AreEqual(state.CommandSequence, first.CommandSequence);
        Assert.AreEqual("earth-mars-relief-corridor:encounter-sol-transit-inspection-InspectionStandardCompliance", first.CueId);
    }

    private GameState State(RouteDefinition route, long commandSequence)
    {
        GameState state = VerticalSliceGameFactory.Create(_pack, "Cinematic Test");
        return state with
        {
            CommandSequence = commandSequence,
            Ship = state.Ship with { StationId = route.OriginStationId },
            Journey = state.Journey! with
            {
                Phase = JourneyPhase.DepartureAuthorized,
                DockedStationId = route.OriginStationId,
                Route = Travel(route),
            },
        };
    }

    private static RouteTravelState Travel(RouteDefinition route) => new(
        route.Id,
        route.OriginStationId,
        route.DestinationStationId,
        route.DurationHours,
        route.FuelCostPercent,
        route.DriveWearPercent,
        route.EncounterAtHour,
        new GameTime(7_200),
        new GameTime(7_200 + route.DurationHours),
        0,
        0,
        route.Id.Value,
        route.PinchCost,
        route.Checkpoints?.Select(item => new RouteCheckpointState(
            item.Id,
            Enum.Parse<RouteCheckpointKind>(item.Kind.ToString()),
            item.ScheduledHour,
            CheckpointResolutionStatus.Pending,
            null,
            null,
            string.Empty)).ToArray());

    private static RouteTravelState ResolveUndock(RouteTravelState travel)
    {
        if (!travel.UsesCheckpoints) return travel;
        RouteCheckpointState undock = travel.AllCheckpoints[0];
        return travel with
        {
            ElapsedBaselineHours = undock.ScheduledHour,
            Checkpoints = travel.AllCheckpoints.Select(item => item.Id == undock.Id
                ? item with { Status = CheckpointResolutionStatus.Resolved, ResolvedAt = travel.DepartedAt.AddHours(2), Outcome = "Undock complete." }
                : item).ToArray(),
        };
    }

    private static RouteTravelState ResolveApproach(RouteTravelState travel)
    {
        if (!travel.UsesCheckpoints) return travel with { ElapsedBaselineHours = travel.BaselineDurationHours };
        return travel with
        {
            ElapsedBaselineHours = travel.BaselineDurationHours,
            Checkpoints = travel.AllCheckpoints.Select(item => item with
            {
                Status = CheckpointResolutionStatus.Resolved,
                ResolvedAt = travel.DepartedAt.AddHours(item.ScheduledHour),
                Outcome = $"{item.Kind} resolved.",
            }).ToArray(),
        };
    }
}
