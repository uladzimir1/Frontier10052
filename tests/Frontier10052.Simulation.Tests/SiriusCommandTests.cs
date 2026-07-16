using Frontier10052.Domain;

namespace Frontier10052.Simulation.Tests;

[TestClass]
public sealed class SiriusCommandTests
{
    [TestMethod]
    public void PinchChargingEnforcesQuantityCapacityCostHoursAndOneSequence()
    {
        GameState state = CreateSiriusTurnaroundState();

        CommandResult<GameState> invalid = TurnaroundCommands.ChargePinch(state, 0, 140);
        CommandResult<GameState> overflow = TurnaroundCommands.ChargePinch(state, 51, 140);
        CommandResult<GameState> charged = TurnaroundCommands.ChargePinch(state, 21, 140);

        Assert.AreEqual(CommandErrorCodes.InvalidPinchQuantity, invalid.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.PinchCapacityExceeded, overflow.Error?.Code);
        Assert.IsTrue(charged.IsSuccess, charged.Error?.Message);
        Assert.AreEqual(71, charged.Value!.Ship.PinchReserve);
        Assert.AreEqual(state.Money.Value - 2_940, charged.Value.Money.Value);
        Assert.AreEqual(state.Time.AddHours(2), charged.Value.Time);
        Assert.AreEqual(state.CommandSequence + 1, charged.Value.CommandSequence);
        Assert.AreEqual(50, state.Ship.PinchReserve);
    }

    [TestMethod]
    public void StationRepairChoicesUseExactServiceArithmeticAndCaps()
    {
        GameState state = CreateSiriusTurnaroundState() with
        {
            Ship = CreateSiriusTurnaroundState().Ship with { DriveWearPercent = 20 },
            Turnaround = CreateSiriusTurnaroundState().Turnaround! with { RepairService = null },
        };

        CommandResult<GameState> certified = TurnaroundCommands.Repair(state, RepairService.Certified, 6_200, 2_100, "Ceres Freehold Anchorage");
        CommandResult<GameState> field = TurnaroundCommands.Repair(state, RepairService.IlyaFieldService, 6_200, 2_100, "Ceres Freehold Anchorage");
        CommandResult<GameState> deferred = TurnaroundCommands.Repair(state, RepairService.Deferred, 6_200, 2_100, "Ceres Freehold Anchorage");

        AssertRepair(certified.Value!, state, cost: 6_200, hours: 8, wearRemoved: 12, expectedWear: 8);
        AssertRepair(field.Value!, state, cost: 2_100, hours: 5, wearRemoved: 6, expectedWear: 14);
        AssertRepair(deferred.Value!, state, cost: 0, hours: 0, wearRemoved: 0, expectedWear: 20);
        Assert.AreEqual(Crew(state, "ilya-sato").Fatigue + 5, Crew(field.Value!, "ilya-sato").Fatigue);

        GameState lowWear = state with { Ship = state.Ship with { DriveWearPercent = 4 } };
        CommandResult<GameState> capped = TurnaroundCommands.Repair(lowWear, RepairService.Certified, 6_200, 2_100, "Ceres Freehold Anchorage");
        Assert.AreEqual(0, capped.Value!.Ship.DriveWearPercent);
        Assert.AreEqual(4, capped.Value.Maintenance!.RepairHistory.Last().WearRemoved);
    }

    [TestMethod]
    public void AuthorizationRequiresCrewFuelPinchWearInformationAndRecordsManifestOnce()
    {
        RouteTravelState route = CreateRoute();
        GameState ready = CreateSiriusTurnaroundState();

        CommandResult<GameState> pinch = TurnaroundCommands.AuthorizeDeparture(ready with { Ship = ready.Ship with { PinchReserve = 43 } }, route);
        CommandResult<GameState> fuel = TurnaroundCommands.AuthorizeDeparture(ready with { Ship = ready.Ship with { FuelPercent = 11 } }, route);
        CommandResult<GameState> wear = TurnaroundCommands.AuthorizeDeparture(ready with { Ship = ready.Ship with { DriveWearPercent = 92 } }, route);
        CommandResult<GameState> information = TurnaroundCommands.AuthorizeDeparture(ready with { InformationCargo = [] }, route);
        CommandResult<GameState> crew = TurnaroundCommands.AuthorizeDeparture(ready with
        {
            Crew = ready.Crew.Select(item => item.Id.Value == "mara-venn" ? item with { Available = false } : item).ToArray(),
        }, route);
        CommandResult<GameState> accepted = TurnaroundCommands.AuthorizeDeparture(ready, route);

        Assert.AreEqual(CommandErrorCodes.InsufficientPinchReserve, pinch.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.DepartureRequirementsNotMet, fuel.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.DepartureRequirementsNotMet, wear.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.InformationMissing, information.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.ServiceRequirementsNotMet, crew.Error?.Code);
        Assert.IsTrue(accepted.IsSuccess, accepted.Error?.Message);
        Assert.AreEqual(ready.CommandSequence + 1, accepted.Value!.CommandSequence);
        Assert.AreEqual(3, accepted.Value.Journey?.VoyageNumber);
        Assert.AreEqual(ready.Ship.PinchReserve, accepted.Value.DepartureManifest?.PinchReserve);
        Assert.AreEqual("scc-sirius-industrial-forecast", accepted.Value.DepartureManifest?.InformationId?.Value);
        Assert.HasCount(5, accepted.Value.Journey!.Route!.AllCheckpoints);
    }

    [TestMethod]
    public void UndockAndGravityBoundarySplitFuelPinchAndWearExactly()
    {
        GameState authorized = TurnaroundCommands.AuthorizeDeparture(CreateSiriusTurnaroundState(), CreateRoute()).Value!;

        GameState undocked = JourneyCommands.BeginVoyage(authorized, authorized.Journey!.Route!).Value!;
        GameState gravity = JourneyCommands.ResolveNextCheckpoint(undocked, null).Value!;

        Assert.AreEqual(authorized.Time.AddHours(2), undocked.Time);
        Assert.AreEqual(authorized.Ship.FuelPercent - 4, undocked.Ship.FuelPercent);
        Assert.AreEqual(authorized.Ship.PinchReserve, undocked.Ship.PinchReserve);
        Assert.AreEqual(authorized.Ship.DriveWearPercent, undocked.Ship.DriveWearPercent);
        Assert.AreEqual(authorized.CommandSequence + 1, undocked.CommandSequence);

        Assert.AreEqual(authorized.Time.AddHours(12), gravity.Time);
        Assert.AreEqual(authorized.Ship.FuelPercent - 12, gravity.Ship.FuelPercent);
        Assert.AreEqual(authorized.Ship.PinchReserve - 44, gravity.Ship.PinchReserve);
        Assert.AreEqual(authorized.Ship.DriveWearPercent + 9, gravity.Ship.DriveWearPercent);
        Assert.AreEqual(undocked.CommandSequence + 1, gravity.CommandSequence);
    }

    [TestMethod]
    [DataRow(nameof(RepairService.Certified), 3)]
    [DataRow(nameof(RepairService.IlyaFieldService), 5)]
    [DataRow(nameof(RepairService.Deferred), 8)]
    public void IlyaLatticeDelayUsesPersistedRepairProvenance(string repairName, int expectedDelay)
    {
        RepairService repair = Enum.Parse<RepairService>(repairName);
        GameState state = CreateSiriusTurnaroundState() with
        {
            Turnaround = CreateSiriusTurnaroundState().Turnaround! with { RepairService = repair },
        };
        state = TurnaroundCommands.AuthorizeDeparture(state, CreateRoute()).Value!;
        state = JourneyCommands.BeginVoyage(state, state.Journey!.Route!).Value!;
        state = JourneyCommands.ResolveNextCheckpoint(state, null).Value!;
        state = JourneyCommands.ResolveNextCheckpoint(state, CheckpointResponse.PreserveSeal).Value!;
        GameTime before = state.Time;

        CommandResult<GameState> result = JourneyCommands.ResolveNextCheckpoint(state, CheckpointResponse.IlyaRecalibration);

        Assert.IsTrue(result.IsSuccess, result.Error?.Message);
        Assert.AreEqual(before.AddHours(48 + expectedDelay), result.Value!.Time);
        Assert.AreEqual(expectedDelay, result.Value.Journey!.Route!.DelayHours);
        Assert.AreEqual(Crew(state, "ilya-sato").Fatigue + 4, Crew(result.Value, "ilya-sato").Fatigue);
        Assert.AreEqual(Crew(state, "ilya-sato").Loyalty + 1, Crew(result.Value, "ilya-sato").Loyalty);
    }

    [TestMethod]
    [DataRow("ceres-freehold-anchorage", 0, 6)]
    [DataRow("ceres-freehold-anchorage", 4, 10)]
    [DataRow("ceres-freehold-anchorage", 12, 12)]
    [DataRow("pluto-gateway", 0, 3)]
    [DataRow("pluto-gateway", 4, 7)]
    [DataRow("pluto-gateway", 12, 9)]
    public void CustomsFormulaUsesOriginAndCappedExposure(string origin, int exposure, int expectedDelay)
    {
        GameState state = CreateSiriusTurnaroundState() with
        {
            Ship = CreateSiriusTurnaroundState().Ship with { StationId = new StationId("sirius-meridian-exchange") },
            LegalExposure = exposure,
            SiriusCustoms = new SiriusCustomsState(new StationId(origin), false, null, 0, "Pending"),
            Journey = CreateSiriusTurnaroundState().Journey! with { Phase = JourneyPhase.CustomsPending },
        };

        CommandResult<GameState> result = JourneyCommands.ClearSiriusCustoms(state);

        Assert.IsTrue(result.IsSuccess, result.Error?.Message);
        Assert.AreEqual(expectedDelay, result.Value!.SiriusCustoms!.DelayHours);
        Assert.AreEqual(state.Time.AddHours(expectedDelay), result.Value.Time);
        Assert.AreEqual(Crew(state, "noor-okafor").Fatigue + ((expectedDelay + 1) / 2), Crew(result.Value, "noor-okafor").Fatigue);
        Assert.AreEqual(state.CommandSequence + 1, result.Value.CommandSequence);
    }

    [TestMethod]
    public void MaraLatticeChoiceReportsExactReserveAndWearBlockers()
    {
        GameState state = CreateAtLattice();
        CommandResult<GameState> reserve = JourneyCommands.ResolveNextCheckpoint(state with
        {
            Ship = state.Ship with { PinchReserve = 5 },
        }, CheckpointResponse.MaraPinchCorrection);
        CommandResult<GameState> wear = JourneyCommands.ResolveNextCheckpoint(state with
        {
            Ship = state.Ship with { DriveWearPercent = 98 },
        }, CheckpointResponse.MaraPinchCorrection);

        Assert.AreEqual(CommandErrorCodes.InsufficientPinchReserve, reserve.Error?.Code);
        StringAssert.Contains(reserve.Error!.Message, "requires 6 extra pinch points; Wayfarer has 5");
        Assert.AreEqual(CommandErrorCodes.CheckpointResponseUnavailable, wear.Error?.Code);
        StringAssert.Contains(wear.Error!.Message, "adds 3 drive wear; only 2 points of margin remain");
    }

    private static GameState CreateAtLattice()
    {
        GameState state = TurnaroundCommands.AuthorizeDeparture(CreateSiriusTurnaroundState(), CreateRoute()).Value!;
        state = JourneyCommands.BeginVoyage(state, state.Journey!.Route!).Value!;
        state = JourneyCommands.ResolveNextCheckpoint(state, null).Value!;
        return JourneyCommands.ResolveNextCheckpoint(state, CheckpointResponse.PreserveSeal).Value!;
    }

    private static GameState CreateSiriusTurnaroundState()
    {
        GameTime time = new(1_000);
        StationId ceres = new("ceres-freehold-anchorage");
        StationId sirius = new("sirius-meridian-exchange");
        InformationId informationId = new("scc-sirius-industrial-forecast");
        ContractState contract = new(
            new ContractId("scc-sirius-industrial-forecast"),
            ceres,
            sirius,
            new CommodityId("sirius-industrial-forecast"),
            new Tonnes(0),
            time.AddHours(216),
            new Credits(26_000),
            new Credits(10_000),
            ContractStatus.Accepted,
            FactionIds.SiriusCorporateCompact,
            true,
            time,
            time,
            Objective: new ContractObjectiveState(ContractObjectiveKind.Information, null, new Tonnes(0), informationId),
            AcceptanceExpiresAt: time.AddHours(36));
        IReadOnlyList<CrewMemberState> crew =
        [
            new(new CrewId("mara-venn"), 60, 10, true, "Available", []),
            new(new CrewId("ilya-sato"), 60, 10, true, "Available", []),
            new(new CrewId("noor-okafor"), 60, 10, true, "Available", []),
            new(new CrewId("tomas-vale"), 60, 10, true, "Available", []),
        ];
        InformationItemState dossier = new(
            informationId,
            "Sirius industrial allocation forecast",
            InformationDisposition.Sealed,
            78,
            [new InformationProvenanceState("SCC Industrial Coordination Office", time, 78, "Issuer signed")]);
        StationMarketState market = new(ceres, []);
        JourneyState journey = new(
            JourneyPhase.Turnaround,
            ceres,
            null,
            null,
            new DestinationMarketState(sirius, []),
            [],
            "Sirius preparation",
            2,
            contract.Id);

        return new GameState(
            GameState.CurrentSchemaVersion,
            10052,
            20,
            new GameId("game-10052-sirius-unit"),
            time,
            new CommanderState("Unit Test"),
            new ShipState(new ShipId("wayfarer"), ceres, new Tonnes(72), 60, 20, 8, 50),
            crew,
            market,
            [],
            contract,
            [],
            new Credits(50_000),
            true,
            null,
            EngineerAssignment.InspectDrive,
            "Ready",
            "Ready",
            false,
            null,
            journey,
            0,
            [contract],
            [market],
            new LienState(new Credits(66_000), LienDisposition.Serviced, []),
            new MaintenanceState(92, 80, false, []),
            [
                new FactionStandingState(FactionIds.TerranContinuityAuthority, 0),
                new FactionStandingState(FactionIds.KuiperSyndicates, 0),
                new FactionStandingState(FactionIds.SiriusCorporateCompact, 0),
                new FactionStandingState(FactionIds.SiriusLabor, 0),
            ],
            0,
            new TurnaroundState(ceres, time, time.AddHours(36), null, RepairService.Certified, null, contract.Id, false, "Ready", StationOperationsMode.SiriusPreparation),
            [],
            [new StationVisitState(ceres, time, null)],
            78,
            [dossier],
            [],
            []);
    }

    private static RouteTravelState CreateRoute()
    {
        GameTime time = new(1_000);
        return new RouteTravelState(
            new RouteId("ceres-sirius-intelligence-run"),
            new StationId("ceres-freehold-anchorage"),
            new StationId("sirius-meridian-exchange"),
            164,
            12,
            9,
            58,
            time,
            time.AddHours(164),
            0,
            0,
            "ceres-sirius-intelligence-run",
            44,
            [
                new RouteCheckpointState(new RouteCheckpointId("undock"), RouteCheckpointKind.Undock, 2, CheckpointResolutionStatus.Pending, null, null, ""),
                new RouteCheckpointState(new RouteCheckpointId("gravity"), RouteCheckpointKind.GravityBoundary, 12, CheckpointResolutionStatus.Pending, null, null, ""),
                new RouteCheckpointState(new RouteCheckpointId("message"), RouteCheckpointKind.DelayedMessage, 58, CheckpointResolutionStatus.Pending, null, null, ""),
                new RouteCheckpointState(new RouteCheckpointId("lattice"), RouteCheckpointKind.LatticeDrift, 106, CheckpointResolutionStatus.Pending, null, null, ""),
                new RouteCheckpointState(new RouteCheckpointId("approach"), RouteCheckpointKind.Approach, 164, CheckpointResolutionStatus.Pending, null, null, ""),
            ]);
    }

    private static CrewMemberState Crew(GameState state, string id) => state.Crew.Single(item => item.Id.Value == id);

    private static void AssertRepair(GameState actual, GameState before, long cost, int hours, int wearRemoved, int expectedWear)
    {
        Assert.AreEqual(before.Money.Value - cost, actual.Money.Value);
        Assert.AreEqual(before.Time.AddHours(hours), actual.Time);
        Assert.AreEqual(expectedWear, actual.Ship.DriveWearPercent);
        Assert.AreEqual(wearRemoved, actual.Maintenance!.RepairHistory.Last().WearRemoved);
        Assert.AreEqual(before.CommandSequence + 1, actual.CommandSequence);
    }
}
