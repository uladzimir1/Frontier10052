using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Aftermath;
using Frontier10052.Gameplay.Journey;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Gameplay.Turnaround;
using Frontier10052.Infrastructure;
using Frontier10052.Simulation;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class SiriusIntelligenceIntegrationTests
{
    private const string SiriusContractId = "scc-sirius-industrial-forecast";
    private string _directory = null!;
    private JsonGameSaveStore _store = null!;
    private GameSessionCoordinator _sessions = null!;
    private StationOperationsService _station = null!;
    private TurnaroundService _turnaround = null!;
    private JourneyService _journey = null!;
    private SiriusAftermathService _aftermath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Frontier10052.SiriusTests", Guid.NewGuid().ToString("N"));
        _store = new JsonGameSaveStore(_directory);
        _sessions = new GameSessionCoordinator(_store);
        _station = new StationOperationsService(_sessions);
        _turnaround = new TurnaroundService(_sessions);
        _journey = new JourneyService(_sessions);
        _aftermath = new SiriusAftermathService(_sessions);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    [DataRow("ceres", nameof(CheckpointResponse.PreserveSeal), nameof(CheckpointResponse.IlyaRecalibration))]
    [DataRow("ceres", nameof(CheckpointResponse.PreserveSeal), nameof(CheckpointResponse.MaraPinchCorrection))]
    [DataRow("ceres", nameof(CheckpointResponse.CorroborateWarning), nameof(CheckpointResponse.IlyaRecalibration))]
    [DataRow("ceres", nameof(CheckpointResponse.CorroborateWarning), nameof(CheckpointResponse.MaraPinchCorrection))]
    [DataRow("ceres", nameof(CheckpointResponse.LeakWarning), nameof(CheckpointResponse.IlyaRecalibration))]
    [DataRow("ceres", nameof(CheckpointResponse.LeakWarning), nameof(CheckpointResponse.MaraPinchCorrection))]
    [DataRow("pluto", nameof(CheckpointResponse.PreserveSeal), nameof(CheckpointResponse.IlyaRecalibration))]
    [DataRow("pluto", nameof(CheckpointResponse.PreserveSeal), nameof(CheckpointResponse.MaraPinchCorrection))]
    [DataRow("pluto", nameof(CheckpointResponse.CorroborateWarning), nameof(CheckpointResponse.IlyaRecalibration))]
    [DataRow("pluto", nameof(CheckpointResponse.CorroborateWarning), nameof(CheckpointResponse.MaraPinchCorrection))]
    [DataRow("pluto", nameof(CheckpointResponse.LeakWarning), nameof(CheckpointResponse.IlyaRecalibration))]
    [DataRow("pluto", nameof(CheckpointResponse.LeakWarning), nameof(CheckpointResponse.MaraPinchCorrection))]
    public async Task AllOriginMessageAndMechanicalCombinationsPersistExactConsequences(
        string origin,
        string messageName,
        string mechanicalName)
    {
        CheckpointResponse message = Enum.Parse<CheckpointResponse>(messageName);
        CheckpointResponse mechanical = Enum.Parse<CheckpointResponse>(mechanicalName);
        string player = $"matrix-{origin}-{messageName}-{mechanicalName}".ToLowerInvariant();
        await CompleteSecondContractAsync(player, origin);

        GameState offered = await StateAsync(player);
        int routeHours = origin == "ceres" ? 164 : 148;
        int routeFuel = origin == "ceres" ? 12 : 10;
        int basePinch = origin == "ceres" ? 44 : 40;
        int baseWear = origin == "ceres" ? 9 : 8;
        int gravityHour = origin == "ceres" ? 12 : 10;
        int messageHour = origin == "ceres" ? 58 : 52;
        int latticeHour = origin == "ceres" ? 106 : 94;
        int extraPinch = mechanical == CheckpointResponse.MaraPinchCorrection ? 6 : 0;
        Assert.AreEqual(origin == "ceres" ? "ceres-freehold-anchorage" : "pluto-gateway", offered.Ship.StationId.Value);
        Assert.AreEqual(JourneyPhase.Turnaround, offered.Journey?.Phase);
        Assert.AreEqual(36, offered.Turnaround!.OffersExpireAt.HoursSinceStart - offered.Time.HoursSinceStart);

        await AcceptedAsync(_turnaround.SelectContractAsync(player, new ContractId(SiriusContractId)));
        await AcceptedAsync(_turnaround.RepairAsync(player, RepairService.Certified));
        await AcceptedAsync(_turnaround.ChargePinchAsync(player, basePinch + extraPinch));
        GameState prepared = await StateAsync(player);
        Assert.AreEqual(basePinch + extraPinch, prepared.Ship.PinchReserve);
        Assert.AreEqual(offered.Time.AddHours(8 + ((basePinch + extraPinch + 19) / 20)), prepared.Time);
        await AcceptedAsync(_turnaround.AuthorizeDepartureAsync(player));
        GameState authorized = await StateAsync(player);
        Assert.AreEqual(3, authorized.Journey?.VoyageNumber);
        Assert.AreEqual(basePinch + extraPinch, authorized.DepartureManifest?.PinchReserve);
        Assert.AreEqual("scc-sirius-industrial-forecast", authorized.DepartureManifest?.InformationId?.Value);
        Assert.HasCount(5, authorized.Journey!.Route!.AllCheckpoints);
        Assert.IsTrue(authorized.Journey.Route.AllCheckpoints.Select(item => item.ScheduledHour).SequenceEqual(
            origin == "ceres" ? [2, 12, 58, 106, 164] : [2, 10, 52, 94, 148]));

        GameState beforeUndock = authorized;
        await AcceptedAsync(_journey.BeginVoyageAsync(player));
        GameState undocked = await StateAsync(player);
        Assert.AreEqual(beforeUndock.Time.AddHours(2), undocked.Time);
        Assert.AreEqual(beforeUndock.Ship.FuelPercent - 4, undocked.Ship.FuelPercent);
        Assert.AreEqual(beforeUndock.Ship.PinchReserve, undocked.Ship.PinchReserve);
        Assert.AreEqual(beforeUndock.Ship.DriveWearPercent, undocked.Ship.DriveWearPercent);
        Assert.AreEqual(CheckpointResolutionStatus.Resolved, undocked.Journey!.Route!.AllCheckpoints[0].Status);
        await AssertReloadStableAsync(player);

        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        GameState gravity = await StateAsync(player);
        Assert.AreEqual(gravityHour, gravity.Journey!.Route!.ElapsedBaselineHours);
        Assert.AreEqual(beforeUndock.Ship.FuelPercent - routeFuel, gravity.Ship.FuelPercent);
        Assert.AreEqual(extraPinch, gravity.Ship.PinchReserve);
        Assert.AreEqual(beforeUndock.Ship.DriveWearPercent + baseWear, gravity.Ship.DriveWearPercent);
        await AssertReloadStableAsync(player);

        GameState beforeMessage = gravity;
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, message));
        GameState afterMessage = await StateAsync(player);
        int messageDelay = message switch
        {
            CheckpointResponse.CorroborateWarning => 6,
            CheckpointResponse.LeakWarning => 2,
            _ => 0,
        };
        Assert.AreEqual(messageHour, afterMessage.Journey!.Route!.ElapsedBaselineHours);
        Assert.AreEqual(messageDelay, afterMessage.Journey.Route.DelayHours);
        Assert.AreEqual(beforeMessage.Time.AddHours(messageHour - gravityHour + messageDelay), afterMessage.Time);
        AssertMessageConsequences(beforeMessage, afterMessage, message);
        await AssertReloadStableAsync(player);

        GameState beforeMechanical = afterMessage;
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, mechanical));
        GameState afterMechanical = await StateAsync(player);
        int mechanicalDelay = mechanical == CheckpointResponse.IlyaRecalibration ? 3 : 1;
        Assert.AreEqual(latticeHour, afterMechanical.Journey!.Route!.ElapsedBaselineHours);
        Assert.AreEqual(messageDelay + mechanicalDelay, afterMechanical.Journey.Route.DelayHours);
        Assert.AreEqual(beforeMechanical.Time.AddHours(latticeHour - messageHour + mechanicalDelay), afterMechanical.Time);
        AssertMechanicalConsequences(beforeMechanical, afterMechanical, mechanical);
        await AssertReloadStableAsync(player);

        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        GameState approach = await StateAsync(player);
        Assert.AreEqual(routeHours, approach.Journey!.Route!.ElapsedBaselineHours);
        Assert.AreEqual(JourneyPhase.Approach, approach.Journey.Phase);
        Assert.IsTrue(approach.Journey.Route.AllCheckpoints.All(item => item.Status == CheckpointResolutionStatus.Resolved));
        await AcceptedAsync(_journey.ArriveAsync(player));
        GameState arrived = await StateAsync(player);
        Assert.AreEqual("sirius-meridian-exchange", arrived.Ship.StationId.Value);
        Assert.AreEqual(JourneyPhase.CustomsPending, arrived.Journey?.Phase);
        await AssertReloadStableAsync(player);

        int noorFatigue = Crew(arrived, "noor-okafor").Fatigue;
        int expectedCustoms = Math.Max(1, 6 - (origin == "pluto" ? 3 : 0) + Math.Min(6, arrived.LegalExposure));
        await AcceptedAsync(_journey.ClearSiriusCustomsAsync(player));
        GameState cleared = await StateAsync(player);
        Assert.AreEqual(expectedCustoms, cleared.SiriusCustoms?.DelayHours);
        Assert.AreEqual(noorFatigue + ((expectedCustoms + 1) / 2), Crew(cleared, "noor-okafor").Fatigue);
        Assert.AreEqual(JourneyPhase.Docked, cleared.Journey?.Phase);

        long credits = cleared.Money.Value;
        int compact = Standing(cleared, FactionIds.SiriusCorporateCompact);
        int labor = Standing(cleared, FactionIds.SiriusLabor);
        int trust = cleared.CommercialTrust;
        await AcceptedAsync(_journey.SettleInformationContractAsync(player));
        GameState settled = await StateAsync(player);
        Assert.AreEqual(JourneyPhase.Delivered, settled.Journey?.Phase);
        Assert.AreEqual(ContractStatus.Completed, settled.Contract.Status);
        Assert.AreEqual(InformationDisposition.Delivered, settled.AllInformationCargo.Single().Disposition);
        Assert.IsTrue(settled.InformationSettlement!.OnTime);
        AssertSettlementConsequences(origin, message, credits, compact, labor, trust, settled);
        Assert.AreEqual(settled.InformationSettlement, settled.AllJourneyHistory.Single(item => item.VoyageNumber == 3).InformationSettlement);
        await AssertReloadStableAsync(player);

        Assert.IsNotNull(settled.SiriusAftermath);
        Assert.AreEqual(12, settled.SiriusAftermath.ActuatorUnits);
        Assert.AreEqual(1_579, settled.SiriusAftermath.FrozenUnitPrice.Value);
        CrewConflictResponse hearing = mechanical == CheckpointResponse.IlyaRecalibration ? CrewConflictResponse.JointAudit : CrewConflictResponse.CaptainsOrder;
        await AcceptedAsync(_aftermath.ResolveCrewConflictAsync(player, hearing));
        ActuatorAllocationResponse allocation = message switch
        {
            CheckpointResponse.PreserveSeal => ActuatorAllocationResponse.CorporatePriority,
            CheckpointResponse.CorroborateWarning => ActuatorAllocationResponse.AuditedSplit,
            _ => ActuatorAllocationResponse.LaborSafety,
        };
        await AcceptedAsync(_aftermath.ResolveActuatorAllocationAsync(player, allocation));
        SiriusAftermathSnapshot aftermath = (await _aftermath.ResumeAsync(player)).Value!;
        Assert.AreEqual(SiriusAftermathPhase.Resolved, aftermath.Phase);
        Assert.AreEqual(0, aftermath.ActuatorUnits);
        Assert.AreEqual(12, aftermath.CorporateUnits + aftermath.LaborUnits + aftermath.WayfarerUnits);
        Assert.AreEqual(1_728, aftermath.UnitPrice);
        await AssertReloadStableAsync(player);
    }

    [TestMethod]
    [DataRow("ceres", 95, 140, 44)]
    [DataRow("pluto", 110, 120, 40)]
    public async Task StationChargingUsesExactArithmeticCapsAndStableAtomicErrors(string origin, int fuelPrice, int pinchPrice, int requiredPinch)
    {
        string player = $"charging-{origin}";
        await CompleteSecondContractAsync(player, origin);
        TurnaroundSnapshot initial = (await _turnaround.ResumeAsync(player)).Value!;
        Assert.AreEqual(fuelPrice, initial.FuelUnitCost);
        Assert.AreEqual(pinchPrice, initial.PinchUnitCost);

        await AssertAtomicTurnaroundRejectionAsync(player, () => _turnaround.ChargePinchAsync(player, 0), CommandErrorCodes.InvalidPinchQuantity);
        GameState beforeCharge = await StateAsync(player);
        await AcceptedAsync(_turnaround.ChargePinchAsync(player, requiredPinch));
        GameState charged = await StateAsync(player);
        Assert.AreEqual(requiredPinch, charged.Ship.PinchReserve);
        Assert.AreEqual(beforeCharge.Money.Value - ((long)pinchPrice * requiredPinch), charged.Money.Value);
        Assert.AreEqual(beforeCharge.Time.AddHours((requiredPinch + 19) / 20), charged.Time);

        int fill = 100 - requiredPinch;
        await AcceptedAsync(_turnaround.ChargePinchAsync(player, fill));
        await AssertAtomicTurnaroundRejectionAsync(player, () => _turnaround.ChargePinchAsync(player, 1), CommandErrorCodes.PinchCapacityExceeded);

        GameState beforeFuel = await StateAsync(player);
        int fuelPoints = Math.Min(11, 100 - beforeFuel.Ship.FuelPercent);
        await AcceptedAsync(_turnaround.RefuelAsync(player, fuelPoints));
        GameState refueled = await StateAsync(player);
        Assert.AreEqual(beforeFuel.Money.Value - ((long)fuelPrice * fuelPoints), refueled.Money.Value);
        Assert.AreEqual(beforeFuel.Time.AddHours((fuelPoints + 9) / 10), refueled.Time);
        await AssertAtomicTurnaroundRejectionAsync(player, () => _turnaround.RefuelAsync(player, 0), CommandErrorCodes.InvalidFuelQuantity);
        await AssertAtomicTurnaroundRejectionAsync(player, () => _turnaround.RefuelAsync(player, 101), CommandErrorCodes.FuelTankCapacityExceeded);
    }

    [TestMethod]
    public async Task SiriusAuthorizationAndCheckpointBlockersAreExplicitAndAtomicWithFallback()
    {
        const string player = "sirius-blockers";
        await CompleteSecondContractAsync(player, "ceres");
        await AcceptedAsync(_turnaround.SelectContractAsync(player, new ContractId(SiriusContractId)));
        await AcceptedAsync(_turnaround.RepairAsync(player, RepairService.Deferred));
        await AssertAtomicTurnaroundRejectionAsync(player, () => _turnaround.AuthorizeDepartureAsync(player), CommandErrorCodes.InsufficientPinchReserve);
        await AcceptedAsync(_turnaround.ChargePinchAsync(player, 44));
        await AcceptedAsync(_turnaround.AuthorizeDepartureAsync(player));
        await AcceptedAsync(_journey.BeginVoyageAsync(player));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, CheckpointResponse.PreserveSeal));

        await AssertAtomicJourneyRejectionAsync(player, () => _journey.ResolveCheckpointAsync(player, CheckpointResponse.MaraPinchCorrection), CommandErrorCodes.InsufficientPinchReserve, "requires 6 extra pinch points");
        GameState unavailable = await MutateForTestAsync(player, state => state with
        {
            Crew = state.Crew.Select(member => member.Id.Value == "mara-venn" ? member with { Available = false } : member).ToArray(),
        });
        Assert.IsFalse(Crew(unavailable, "mara-venn").Available);
        await AssertAtomicJourneyRejectionAsync(player, () => _journey.ResolveCheckpointAsync(player, CheckpointResponse.MaraPinchCorrection), CommandErrorCodes.CheckpointResponseUnavailable, "requires Mara");
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, CheckpointResponse.IlyaRecalibration));

        await AssertAtomicJourneyRejectionAsync(player, () => _journey.ResolveCheckpointAsync(player, CheckpointResponse.IlyaRecalibration), CommandErrorCodes.CheckpointResponseUnavailable, "automatic checkpoint");
    }

    [TestMethod]
    public async Task SiriusAcceptanceWindowIncludesEqualityAndRejectsTheFirstHourAfterIt()
    {
        const string equalityPlayer = "sirius-offer-equality";
        await CompleteSecondContractAsync(equalityPlayer, "ceres");
        await MutateForTestAsync(equalityPlayer, state => state with { Time = state.Turnaround!.OffersExpireAt });
        CommandResult<TurnaroundSnapshot> accepted = await _turnaround.SelectContractAsync(equalityPlayer, new ContractId(SiriusContractId));
        Assert.IsTrue(accepted.IsSuccess, accepted.Error?.Message);

        const string expiredPlayer = "sirius-offer-expired";
        await CompleteSecondContractAsync(expiredPlayer, "pluto");
        await MutateForTestAsync(expiredPlayer, state => state with { Time = state.Turnaround!.OffersExpireAt.AddHours(1) });
        await AssertAtomicTurnaroundRejectionAsync(
            expiredPlayer,
            () => _turnaround.SelectContractAsync(expiredPlayer, new ContractId(SiriusContractId)),
            CommandErrorCodes.OfferExpired);
    }

    [TestMethod]
    [DataRow(nameof(CheckpointResponse.PreserveSeal), 0, 10_000, -4, 0, -2, false)]
    [DataRow(nameof(CheckpointResponse.CorroborateWarning), 0, 10_000, -4, 0, -2, false)]
    [DataRow(nameof(CheckpointResponse.LeakWarning), 4_000, 0, -6, 7, 0, true)]
    public async Task LateInformationOutcomesAndClaimCapitalizationAreExact(
        string messageName,
        int payment,
        int claim,
        int compactDelta,
        int laborDelta,
        int trustDelta,
        bool completed)
    {
        CheckpointResponse message = Enum.Parse<CheckpointResponse>(messageName);
        string player = $"late-{messageName}".ToLowerInvariant();
        await TravelToClearedSiriusAsync(player, "ceres", message, CheckpointResponse.IlyaRecalibration);
        GameState injected = await MutateForTestAsync(player, state => state with
        {
            Time = state.Contract.Deadline.AddHours(1),
            Money = new Credits(3_000),
        });
        long lien = injected.Lien!.Principal.Value;
        int compact = Standing(injected, FactionIds.SiriusCorporateCompact);
        int labor = Standing(injected, FactionIds.SiriusLabor);
        int trust = injected.CommercialTrust;

        await AcceptedAsync(_journey.SettleInformationContractAsync(player));
        GameState settled = await StateAsync(player);
        Assert.IsFalse(settled.InformationSettlement!.OnTime);
        Assert.AreEqual(payment, settled.InformationSettlement.Payment.Value);
        Assert.AreEqual(claim, settled.InformationSettlement.Claim.Value);
        Assert.AreEqual(completed ? ContractStatus.Completed : ContractStatus.Failed, settled.Contract.Status);
        Assert.AreEqual(compact + compactDelta, Standing(settled, FactionIds.SiriusCorporateCompact));
        Assert.AreEqual(labor + laborDelta, Standing(settled, FactionIds.SiriusLabor));
        Assert.AreEqual(trust + trustDelta, settled.CommercialTrust);
        if (claim > 0)
        {
            Assert.AreEqual(0, settled.Money.Value);
            Assert.AreEqual(lien + 7_000, settled.Lien!.Principal.Value);
            DebtLedgerEntryState entry = settled.AllDebtLedger.Single(item => item.Kind == "capitalized-contract-claim");
            Assert.AreEqual(3_000, entry.PaidFromCash.Value);
            Assert.AreEqual(7_000, entry.Capitalized.Value);
        }
        else
        {
            Assert.AreEqual(3_000 + payment, settled.Money.Value);
            Assert.AreEqual(lien, settled.Lien!.Principal.Value);
        }
    }

    [TestMethod]
    public async Task DeadlineEqualityIsOnTimeAndDisclosureClaimConsumesCashBeforeCapitalizing()
    {
        const string player = "deadline-equality";
        await TravelToClearedSiriusAsync(player, "pluto", CheckpointResponse.LeakWarning, CheckpointResponse.IlyaRecalibration);
        GameState injected = await MutateForTestAsync(player, state => state with
        {
            Time = state.Contract.Deadline,
            Money = new Credits(3_000),
        });
        long principal = injected.Lien!.Principal.Value;

        await AcceptedAsync(_journey.SettleInformationContractAsync(player));
        GameState settled = await StateAsync(player);
        Assert.IsTrue(settled.InformationSettlement!.OnTime);
        Assert.AreEqual(10_000, settled.InformationSettlement.Claim.Value);
        Assert.AreEqual(7_000, settled.InformationSettlement.CapitalizedClaim.Value);
        Assert.AreEqual(8_000, settled.Money.Value);
        Assert.AreEqual(principal + 7_000, settled.Lien!.Principal.Value);
    }

    [TestMethod]
    public async Task RepeatedCustomsAndSettlementCommandsAreStableAtomicRejections()
    {
        const string player = "repeat-settlement";
        await PrepareSiriusAsync(player, "pluto", RepairService.Certified, 40);
        await AcceptedAsync(_journey.BeginVoyageAsync(player));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, CheckpointResponse.PreserveSeal));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, CheckpointResponse.IlyaRecalibration));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        await AcceptedAsync(_journey.ArriveAsync(player));
        await AssertAtomicJourneyRejectionAsync(player, () => _journey.SettleInformationContractAsync(player), CommandErrorCodes.SettlementUnavailable, "Clear Sirius customs");
        await AcceptedAsync(_journey.ClearSiriusCustomsAsync(player));
        await AssertAtomicJourneyRejectionAsync(player, () => _journey.ClearSiriusCustomsAsync(player), CommandErrorCodes.CustomsAlreadyCleared, "already cleared");
        await AcceptedAsync(_journey.SettleInformationContractAsync(player));
        await AssertAtomicJourneyRejectionAsync(player, () => _journey.SettleInformationContractAsync(player), CommandErrorCodes.SettlementAlreadyComplete, "already settled");
    }

    [TestMethod]
    [DataRow("ceres")]
    [DataRow("pluto")]
    public async Task SchemaThreeSettlementsMigrateToSiriusOfferWithoutChangingAuthority(string origin)
    {
        string player = $"schema-three-{origin}";
        await CompleteSecondContractAsync(player, origin);
        GameState current = await StateAsync(player);
        GameState schema3 = current with
        {
            SchemaVersion = 3,
            Contracts = current.AllContracts.Where(item => item.Id.Value != SiriusContractId).ToArray(),
            Turnaround = null,
            Journey = current.Journey! with { Phase = JourneyPhase.Delivered },
            InformationCargo = null,
            ContractTransformations = null,
            DebtLedger = null,
            SiriusCustoms = null,
            InformationSettlement = null,
        };
        string canonical = GameStateCanonicalizer.Serialize(schema3);
        string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        GameSaveEnvelope envelope = new(GameSaveEnvelope.CurrentGameVersion, "vertical-slice-v2", 3, schema3.Seed, schema3.CommandSequence, checksum, schema3);
        await _store.SaveAsync(player, envelope, overwrite: true);

        TurnaroundSnapshot migrated = (await _turnaround.ResumeAsync(player)).Value!;
        GameState rewritten = await StateAsync(player);
        Assert.AreEqual(5, rewritten.SchemaVersion);
        Assert.AreEqual("vertical-slice-v4", migrated.ContentPackVersion);
        Assert.AreEqual(schema3.CommandSequence, rewritten.CommandSequence);
        Assert.AreEqual(schema3.Time, rewritten.Time);
        Assert.AreEqual(schema3.Money, rewritten.Money);
        Assert.AreEqual(schema3.Ship, rewritten.Ship);
        Assert.AreEqual(JsonSerializer.Serialize(schema3.Cargo), JsonSerializer.Serialize(rewritten.Cargo));
        Assert.AreEqual(JsonSerializer.Serialize(schema3.Crew), JsonSerializer.Serialize(rewritten.Crew));
        Assert.AreEqual(JsonSerializer.Serialize(schema3.Lien), JsonSerializer.Serialize(rewritten.Lien));
        Assert.AreEqual(JsonSerializer.Serialize(schema3.DepartureManifest), JsonSerializer.Serialize(rewritten.DepartureManifest));
        Assert.AreEqual(JsonSerializer.Serialize(schema3.AllJourneyHistory), JsonSerializer.Serialize(rewritten.AllJourneyHistory));
        Assert.AreEqual(
            JsonSerializer.Serialize(schema3.AllContracts),
            JsonSerializer.Serialize(rewritten.AllContracts.Where(item => item.Id.Value != SiriusContractId)));
        Assert.IsTrue(schema3.AllFactionStandings.All(expected =>
            rewritten.AllFactionStandings.Any(actual => actual == expected)));
        Assert.IsTrue(migrated.IsSiriusPreparation);
        Assert.AreEqual(SiriusContractId, rewritten.AllContracts.Single(item => item.Status == ContractStatus.Offered).Id.Value);
        Assert.AreEqual(JourneyPhase.Turnaround, rewritten.Journey?.Phase);
    }

    [TestMethod]
    public async Task IdenticalSiriusCommandsProduceDeterministicCanonicalState()
    {
        foreach (string player in new[] { "sirius-deterministic-a", "sirius-deterministic-b" })
        {
            await TravelToClearedSiriusAsync(player, "ceres", CheckpointResponse.CorroborateWarning, CheckpointResponse.MaraPinchCorrection);
            await AcceptedAsync(_journey.SettleInformationContractAsync(player));
            await AcceptedAsync(_aftermath.ResolveCrewConflictAsync(player, CrewConflictResponse.JointAudit));
            await AcceptedAsync(_aftermath.ResolveActuatorAllocationAsync(player, ActuatorAllocationResponse.AuditedSplit));
        }

        GameState first = await StateAsync("sirius-deterministic-a");
        GameState second = await StateAsync("sirius-deterministic-b");
        Assert.AreEqual(GameStateCanonicalizer.Serialize(first), GameStateCanonicalizer.Serialize(second));
    }

    [TestMethod]
    public async Task SchemaFourSettledSiriusSaveReceivesFrozenAftermathWithoutAuthorityDrift()
    {
        const string player = "schema-four-aftermath";
        await TravelToClearedSiriusAsync(player, "pluto", CheckpointResponse.CorroborateWarning, CheckpointResponse.IlyaRecalibration);
        await AcceptedAsync(_journey.SettleInformationContractAsync(player));
        GameState current = await StateAsync(player);
        GameState schema4 = current with { SchemaVersion = 4, SiriusAftermath = null };
        string canonical = GameStateCanonicalizer.Serialize(schema4);
        string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        await _store.SaveAsync(player, new GameSaveEnvelope(GameSaveEnvelope.CurrentGameVersion, "vertical-slice-v3", 4, schema4.Seed, schema4.CommandSequence, checksum, schema4), true);

        SiriusAftermathSnapshot snapshot = (await _aftermath.ResumeAsync(player)).Value!;
        GameState migrated = await StateAsync(player);

        Assert.AreEqual(5, migrated.SchemaVersion);
        Assert.AreEqual(schema4.CommandSequence, migrated.CommandSequence);
        Assert.AreEqual(schema4.Time, migrated.Time);
        Assert.AreEqual(schema4.Money, migrated.Money);
        Assert.AreEqual(schema4.Ship, migrated.Ship);
        Assert.AreEqual(JsonSerializer.Serialize(schema4.Crew), JsonSerializer.Serialize(migrated.Crew));
        Assert.AreEqual(JsonSerializer.Serialize(schema4.Cargo), JsonSerializer.Serialize(migrated.Cargo));
        Assert.AreEqual(JsonSerializer.Serialize(schema4.Lien), JsonSerializer.Serialize(migrated.Lien));
        Assert.AreEqual(SiriusAftermathPhase.CrewConflict, snapshot.Phase);
        Assert.AreEqual(12, snapshot.ActuatorUnits);
        Assert.AreEqual(1_579, snapshot.UnitPrice);
    }

    [TestMethod]
    public async Task AftermathWrongPhaseInsufficientCashAndRepeatedCommandsAreAtomic()
    {
        const string player = "aftermath-atomic";
        await TravelToClearedSiriusAsync(player, "ceres", CheckpointResponse.CorroborateWarning, CheckpointResponse.IlyaRecalibration);
        await AcceptedAsync(_journey.SettleInformationContractAsync(player));

        GameState beforeEarlyAllocation = await StateAsync(player);
        CommandResult<SiriusAftermathSnapshot> early = await _aftermath.ResolveActuatorAllocationAsync(player, ActuatorAllocationResponse.AuditedSplit);
        Assert.AreEqual(CommandErrorCodes.AftermathWrongPhase, early.Error?.Code);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(beforeEarlyAllocation), GameStateCanonicalizer.Serialize(await StateAsync(player)));

        await AcceptedAsync(_aftermath.ResolveCrewConflictAsync(player, CrewConflictResponse.CaptainsOrder));
        await MutateForTestAsync(player, state => state with { Money = new Credits(2_999) });
        GameState beforeCash = await StateAsync(player);
        CommandResult<SiriusAftermathSnapshot> cash = await _aftermath.ResolveActuatorAllocationAsync(player, ActuatorAllocationResponse.AuditedSplit);
        Assert.AreEqual(CommandErrorCodes.AftermathInsufficientCredits, cash.Error?.Code);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(beforeCash), GameStateCanonicalizer.Serialize(await StateAsync(player)));

        await MutateForTestAsync(player, state => state with { Money = new Credits(3_000) });
        await AcceptedAsync(_aftermath.ResolveActuatorAllocationAsync(player, ActuatorAllocationResponse.AuditedSplit));
        GameState resolved = await StateAsync(player);
        CommandResult<SiriusAftermathSnapshot> repeatedAllocation = await _aftermath.ResolveActuatorAllocationAsync(player, ActuatorAllocationResponse.AuditedSplit);
        CommandResult<SiriusAftermathSnapshot> repeatedHearing = await _aftermath.ResolveCrewConflictAsync(player, CrewConflictResponse.CaptainsOrder);
        Assert.AreEqual(CommandErrorCodes.AftermathAlreadyResolved, repeatedAllocation.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.AftermathAlreadyResolved, repeatedHearing.Error?.Code);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(resolved), GameStateCanonicalizer.Serialize(await StateAsync(player)));
    }

    private async Task TravelToClearedSiriusAsync(string player, string origin, CheckpointResponse message, CheckpointResponse mechanical)
    {
        int pinch = (origin == "ceres" ? 44 : 40) + (mechanical == CheckpointResponse.MaraPinchCorrection ? 6 : 0);
        await PrepareSiriusAsync(player, origin, RepairService.Certified, pinch);
        await AcceptedAsync(_journey.BeginVoyageAsync(player));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, message));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player, mechanical));
        await AcceptedAsync(_journey.ResolveCheckpointAsync(player));
        await AcceptedAsync(_journey.ArriveAsync(player));
        await AcceptedAsync(_journey.ClearSiriusCustomsAsync(player));
    }

    private async Task PrepareSiriusAsync(string player, string origin, RepairService repair, int pinch)
    {
        await CompleteSecondContractAsync(player, origin);
        await AcceptedAsync(_turnaround.SelectContractAsync(player, new ContractId(SiriusContractId)));
        await AcceptedAsync(_turnaround.RepairAsync(player, repair));
        await AcceptedAsync(_turnaround.ChargePinchAsync(player, pinch));
        await AcceptedAsync(_turnaround.AuthorizeDepartureAsync(player));
    }

    private async Task CompleteSecondContractAsync(string player, string origin)
    {
        Assert.IsTrue((await _station.StartNewGameAsync(player, "Sirius Test", overwriteConfirmed: false)).IsSuccess);
        await AcceptedAsync(_station.AcknowledgeBriefingAsync(player));
        await AcceptedAsync(_station.AcceptContractAsync(player));
        await AcceptedAsync(_station.AssignEngineerAsync(player, EngineerAssignment.InspectDrive));
        await AcceptedAsync(_station.AuthorizeDepartureAsync(player));
        await AcceptedAsync(_journey.BeginVoyageAsync(player));
        JourneySnapshot firstContact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        await AcceptedAsync(_journey.ResolveCurrentEncounterAsync(player, firstContact.Encounter!.Responses.First(item => item.IsAvailable).Response!.Value));
        await AcceptedAsync(_journey.AdvanceVoyageAsync(player));
        await AcceptedAsync(_journey.ArriveAsync(player));
        await AcceptedAsync(_journey.DeliverCargoAsync(player));

        await AcceptedAsync(_turnaround.ServiceLienAsync(player));
        await AcceptedAsync(_turnaround.RepairAsync(player, RepairService.Certified));
        await AcceptedAsync(_turnaround.RestCrewAsync(player, CrewRestService.FullLayover));
        string contractId = origin == "ceres" ? "kuiper-ceres-repair-freight" : "tca-pluto-migration-support";
        await AcceptedAsync(_turnaround.SelectContractAsync(player, new ContractId(contractId)));
        await AcceptedAsync(_turnaround.AuthorizeDepartureAsync(player));
        await AcceptedAsync(_journey.BeginVoyageAsync(player));
        await AcceptedAsync(_journey.AdvanceVoyageAsync(player));
        EncounterResponse response = origin == "ceres" ? EncounterResponse.DebrisIlyaRepair : EncounterResponse.MigrationMedicalAssist;
        await AcceptedAsync(_journey.ResolveCurrentEncounterAsync(player, response));
        await AcceptedAsync(_journey.AdvanceVoyageAsync(player));
        await AcceptedAsync(_journey.ArriveAsync(player));
        await AcceptedAsync(_journey.DeliverCargoAsync(player));
    }

    private async Task AssertReloadStableAsync(string player)
    {
        GameState before = await StateAsync(player);
        CommandResult<JourneySnapshot> resumed = await _journey.ResumeAsync(player);
        Assert.IsTrue(resumed.IsSuccess, resumed.Error?.Message);
        GameState after = await StateAsync(player);
        Assert.AreEqual(before.CommandSequence, after.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before), GameStateCanonicalizer.Serialize(after));
    }

    private async Task AssertAtomicTurnaroundRejectionAsync(
        string player,
        Func<ValueTask<CommandResult<TurnaroundSnapshot>>> command,
        string code)
    {
        GameState before = await StateAsync(player);
        CommandResult<TurnaroundSnapshot> result = await command();
        GameState after = await StateAsync(player);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(code, result.Error?.Code);
        Assert.AreEqual(before.CommandSequence, after.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before), GameStateCanonicalizer.Serialize(after));
    }

    private async Task AssertAtomicJourneyRejectionAsync(
        string player,
        Func<ValueTask<CommandResult<JourneySnapshot>>> command,
        string code,
        string messageFragment)
    {
        GameState before = await StateAsync(player);
        CommandResult<JourneySnapshot> result = await command();
        GameState after = await StateAsync(player);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(code, result.Error?.Code);
        StringAssert.Contains(result.Error!.Message, messageFragment);
        Assert.AreEqual(before.CommandSequence, after.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before), GameStateCanonicalizer.Serialize(after));
    }

    private async Task<GameState> MutateForTestAsync(string player, Func<GameState, GameState> update)
    {
        CommandResult<GameState> result = await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(update(state)), default);
        Assert.IsTrue(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task<GameState> StateAsync(string player) => (await _store.LoadAsync(player)).Value!.State;

    private static CrewMemberState Crew(GameState state, string id) => state.Crew.Single(item => item.Id.Value == id);

    private static int Standing(GameState state, string factionId) => state.AllFactionStandings.Single(item => item.FactionId == factionId).Standing;

    private static void AssertMessageConsequences(GameState before, GameState after, CheckpointResponse message)
    {
        InformationItemState dossier = after.AllInformationCargo.Single();
        switch (message)
        {
            case CheckpointResponse.PreserveSeal:
                Assert.AreEqual(InformationDisposition.Sealed, dossier.Disposition);
                Assert.AreEqual(Crew(before, "noor-okafor").Loyalty + 3, Crew(after, "noor-okafor").Loyalty);
                Assert.AreEqual(Crew(before, "tomas-vale").Loyalty - 3, Crew(after, "tomas-vale").Loyalty);
                Assert.AreEqual(before.LegalExposure, after.LegalExposure);
                break;
            case CheckpointResponse.CorroborateWarning:
                Assert.AreEqual(InformationDisposition.Corroborated, dossier.Disposition);
                Assert.AreEqual(95, dossier.ConfidencePercent);
                Assert.AreEqual(Crew(before, "ilya-sato").Fatigue + 4, Crew(after, "ilya-sato").Fatigue);
                Assert.AreEqual(Crew(before, "ilya-sato").Loyalty + 2, Crew(after, "ilya-sato").Loyalty);
                Assert.AreEqual(Crew(before, "noor-okafor").Fatigue + 2, Crew(after, "noor-okafor").Fatigue);
                Assert.AreEqual(Crew(before, "noor-okafor").Loyalty + 1, Crew(after, "noor-okafor").Loyalty);
                Assert.AreEqual(before.LegalExposure + 1, after.LegalExposure);
                break;
            case CheckpointResponse.LeakWarning:
                Assert.AreEqual(InformationDisposition.Disclosed, dossier.Disposition);
                Assert.AreEqual(Crew(before, "tomas-vale").Loyalty + 4, Crew(after, "tomas-vale").Loyalty);
                Assert.AreEqual(Crew(before, "noor-okafor").Loyalty - 3, Crew(after, "noor-okafor").Loyalty);
                Assert.AreEqual(before.LegalExposure + 4, after.LegalExposure);
                Assert.AreEqual(ContractStatus.Transformed, after.Contract.Status);
                Assert.HasCount(before.AllContractTransformations.Count + 1, after.AllContractTransformations);
                break;
        }
    }

    private static void AssertMechanicalConsequences(GameState before, GameState after, CheckpointResponse response)
    {
        if (response == CheckpointResponse.IlyaRecalibration)
        {
            Assert.AreEqual(before.Ship.PinchReserve, after.Ship.PinchReserve);
            Assert.AreEqual(before.Ship.DriveWearPercent, after.Ship.DriveWearPercent);
            Assert.AreEqual(Crew(before, "ilya-sato").Fatigue + 4, Crew(after, "ilya-sato").Fatigue);
            Assert.AreEqual(Crew(before, "ilya-sato").Loyalty + 1, Crew(after, "ilya-sato").Loyalty);
        }
        else
        {
            Assert.AreEqual(before.Ship.PinchReserve - 6, after.Ship.PinchReserve);
            Assert.AreEqual(before.Ship.DriveWearPercent + 3, after.Ship.DriveWearPercent);
            Assert.AreEqual(Crew(before, "mara-venn").Fatigue + 4, Crew(after, "mara-venn").Fatigue);
            Assert.AreEqual(Crew(before, "mara-venn").Loyalty + 1, Crew(after, "mara-venn").Loyalty);
        }
    }

    private static void AssertSettlementConsequences(
        string origin,
        CheckpointResponse message,
        long credits,
        int compact,
        int labor,
        int trust,
        GameState settled)
    {
        switch (message)
        {
            case CheckpointResponse.PreserveSeal:
                Assert.AreEqual(credits + 26_000, settled.Money.Value);
                Assert.AreEqual(compact + 6, Standing(settled, FactionIds.SiriusCorporateCompact));
                Assert.AreEqual(labor, Standing(settled, FactionIds.SiriusLabor));
                Assert.AreEqual(trust + 3, settled.CommercialTrust);
                break;
            case CheckpointResponse.CorroborateWarning:
                Assert.AreEqual(credits + 20_000, settled.Money.Value);
                Assert.AreEqual(compact + 2, Standing(settled, FactionIds.SiriusCorporateCompact));
                Assert.AreEqual(labor + 4, Standing(settled, FactionIds.SiriusLabor));
                Assert.AreEqual(trust + 1, settled.CommercialTrust);
                break;
            case CheckpointResponse.LeakWarning:
                Assert.AreEqual(credits - 10_000 + 8_000, settled.Money.Value);
                Assert.AreEqual(compact - 6, Standing(settled, FactionIds.SiriusCorporateCompact));
                Assert.AreEqual(labor + 5 + (origin == "ceres" ? 2 : 0), Standing(settled, FactionIds.SiriusLabor));
                Assert.AreEqual(trust, settled.CommercialTrust);
                Assert.AreEqual(10_000, settled.InformationSettlement!.Claim.Value);
                break;
        }
    }

    private static async Task AcceptedAsync<T>(ValueTask<CommandResult<T>> command)
    {
        CommandResult<T> result = await command;
        Assert.IsTrue(result.IsSuccess, $"{result.Error?.Code}: {result.Error?.Message}");
    }
}
