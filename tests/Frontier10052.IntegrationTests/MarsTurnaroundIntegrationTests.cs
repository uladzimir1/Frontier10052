using System.Security.Cryptography;
using System.Text;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Journey;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Gameplay.Turnaround;
using Frontier10052.Infrastructure;
using Frontier10052.Simulation;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class MarsTurnaroundIntegrationTests
{
    private string _directory = null!;
    private JsonGameSaveStore _store = null!;
    private GameSessionCoordinator _sessions = null!;
    private StationOperationsService _station = null!;
    private JourneyService _journey = null!;
    private TurnaroundService _turnaround = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Frontier10052.TurnaroundTests", Guid.NewGuid().ToString("N"));
        _store = new JsonGameSaveStore(_directory);
        _sessions = new GameSessionCoordinator(_store);
        _station = new StationOperationsService(_sessions);
        _journey = new JourneyService(_sessions);
        _turnaround = new TurnaroundService(_sessions);
    }

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [TestMethod]
    public async Task MarsServicesPersistExactDebtRepairFuelTimeAndCrewConsequences()
    {
        const string player = "services";
        await CompleteFirstContractAsync(player);
        TurnaroundSnapshot start = (await _turnaround.ResumeMarsOperationsAsync(player)).Value!;

        TurnaroundSnapshot lien = (await _turnaround.ServiceLienAsync(player)).Value!;
        TurnaroundSnapshot fuel = (await _turnaround.RefuelAsync(player, 11)).Value!;
        TurnaroundSnapshot repair = (await _turnaround.RepairAsync(player, RepairService.IlyaFieldService)).Value!;
        TurnaroundSnapshot rest = (await _turnaround.RestCrewAsync(player, CrewRestService.TurnaroundWatches)).Value!;
        GameState state = (await _store.LoadAsync(player)).Value!.State;

        Assert.AreEqual(66_000, lien.Lien.Principal);
        Assert.AreEqual(start.AvailableCredits - 6_000, lien.AvailableCredits);
        Assert.AreEqual(start.Ship.FuelPercent + 11, fuel.Ship.FuelPercent);
        Assert.AreEqual(2, ParseDayHours(fuel.GameTimeLabel) - ParseDayHours(lien.GameTimeLabel));
        Assert.AreEqual(fuel.Ship.DriveWearPercent - 6, repair.Ship.DriveWearPercent);
        Assert.AreEqual(5, ParseDayHours(repair.GameTimeLabel) - ParseDayHours(fuel.GameTimeLabel));
        Assert.AreEqual(4, ParseDayHours(rest.GameTimeLabel) - ParseDayHours(repair.GameTimeLabel));
        Assert.AreEqual(RepairService.IlyaFieldService, state.Turnaround?.RepairService);
        Assert.AreEqual("Ilya Sato field service aboard Wayfarer", state.Maintenance?.RepairHistory.Single().Provenance);
        Assert.IsTrue(state.Crew.Single(item => item.Id.Value == "ilya-sato").Memories!.Any(item => item.Kind == "repair"));
        Assert.IsTrue(state.Crew.All(item => item.Memories!.Any(memory => memory.Kind == "rest")));
    }

    [TestMethod]
    public async Task RejectedServiceThatWouldExpireOffersIsAtomicAndStable()
    {
        const string player = "offer-window";
        await CompleteFirstContractAsync(player);
        await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Time = state.Turnaround!.OffersExpireAt.AddHours(0) }), default);
        GameState before = (await _store.LoadAsync(player)).Value!.State;

        CommandResult<TurnaroundSnapshot> rejected = await _turnaround.RestCrewAsync(player, CrewRestService.TurnaroundWatches);
        GameState after = (await _store.LoadAsync(player)).Value!.State;

        Assert.IsFalse(rejected.IsSuccess);
        Assert.AreEqual(CommandErrorCodes.OfferExpirationRisk, rejected.Error?.Code);
        Assert.AreEqual(before.CommandSequence, after.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before), GameStateCanonicalizer.Serialize(after));
    }

    [TestMethod]
    public async Task PlutoBranchCompletesWithTransformedCeresOfferCrewMemoriesAndTcaStanding()
    {
        const string player = "pluto-branch";
        await CompleteFirstContractAsync(player);
        await _turnaround.ServiceLienAsync(player);
        await _turnaround.RepairAsync(player, RepairService.Certified);
        await _turnaround.RestCrewAsync(player, CrewRestService.FullLayover);
        TurnaroundSnapshot selected = (await _turnaround.SelectContractAsync(player, new ContractId("tca-pluto-migration-support"))).Value!;
        await _turnaround.AuthorizeDepartureAsync(player);
        await _journey.BeginVoyageAsync(player);
        JourneySnapshot contact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        JourneySnapshot resolved = (await _journey.ResolveCurrentEncounterAsync(player, EncounterResponse.MigrationMedicalAssist)).Value!;
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ArriveAsync(player);
        JourneySnapshot completed = (await _journey.DeliverCargoAsync(player)).Value!;
        GameState state = (await _store.LoadAsync(player)).Value!.State;

        Assert.AreEqual("Pluto Gateway", selected.AuthorizedDestination ?? selected.Offers.Single(item => item.Status == "Accepted").Destination);
        Assert.AreEqual("pluto-migration-medical-emergency", contact.Encounter?.Id);
        Assert.AreEqual(4, resolved.Route.DelayHours);
        Assert.AreEqual(JourneyPhase.Turnaround, completed.Phase);
        Assert.AreEqual("Completed", completed.Contract.Status);
        Assert.IsFalse(completed.LastOutcome.Contains("pluto-gateway", StringComparison.Ordinal));
        Assert.HasCount(2, state.AllJourneyHistory);
        Assert.IsNotNull(state.AllJourneyHistory.Single(item => item.VoyageNumber == 2).DestinationManifest);
        Assert.AreEqual(ContractStatus.Transformed, state.AllContracts.Single(item => item.Id.Value == "kuiper-ceres-repair-freight").Status);
        Assert.AreEqual(ContractStatus.Completed, state.AllContracts.Single(item => item.Id.Value == "tca-pluto-migration-support").Status);
        Assert.AreEqual("scc-sirius-industrial-forecast", state.AllContracts.Single(item => item.Status == ContractStatus.Offered).Id.Value);
        Assert.IsTrue(state.Crew.Single(item => item.Id.Value == "tomas-vale").Memories!.Any(item => item.Kind == "humanitarian-response"));
        Assert.IsGreaterThan(0, state.AllFactionStandings.Single(item => item.FactionId == FactionIds.TerranContinuityAuthority).Standing);
    }

    [TestMethod]
    public async Task CeresBranchCompletesWithDeferredLienGrayExposureAndIlyaOutcome()
    {
        const string player = "ceres-branch";
        await CompleteFirstContractAsync(player);
        TurnaroundSnapshot debt = (await _turnaround.DeferLienAsync(player)).Value!;
        await _turnaround.RepairAsync(player, RepairService.IlyaFieldService);
        await _turnaround.RestCrewAsync(player, CrewRestService.TurnaroundWatches);
        await _turnaround.SelectContractAsync(player, new ContractId("kuiper-ceres-repair-freight"));
        await _turnaround.AuthorizeDepartureAsync(player);
        await _journey.BeginVoyageAsync(player);
        JourneySnapshot contact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        JourneySnapshot resolved = (await _journey.ResolveCurrentEncounterAsync(player, EncounterResponse.DebrisIlyaRepair)).Value!;
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ArriveAsync(player);
        JourneySnapshot completed = (await _journey.DeliverCargoAsync(player)).Value!;
        GameState state = (await _store.LoadAsync(player)).Value!.State;

        Assert.AreEqual(73_500, debt.Lien.Principal);
        Assert.AreEqual("ceres-debris-coolant-breach", contact.Encounter?.Id);
        Assert.AreEqual(3, resolved.Route.DelayHours);
        Assert.AreEqual(JourneyPhase.Turnaround, completed.Phase);
        Assert.IsFalse(completed.LastOutcome.Contains("ceres-freehold-anchorage", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(2, completed.LegalExposure);
        Assert.IsGreaterThan(0, state.AllFactionStandings.Single(item => item.FactionId == FactionIds.KuiperSyndicates).Standing);
        Assert.IsGreaterThan(58, state.Crew.Single(item => item.Id.Value == "ilya-sato").Loyalty);
        Assert.IsLessThan(66, state.Crew.Single(item => item.Id.Value == "tomas-vale").Loyalty);
    }

    [TestMethod]
    public async Task AlternativeEncounterResponsesPersistRelationshipAndMechanicalConsequences()
    {
        const string plutoPlayer = "pluto-conserve";
        await PrepareSecondVoyageAsync(plutoPlayer, "tca-pluto-migration-support", RepairService.Certified);
        await _journey.BeginVoyageAsync(plutoPlayer);
        await _journey.AdvanceVoyageAsync(plutoPlayer);
        GameState plutoBefore = (await _store.LoadAsync(plutoPlayer)).Value!.State;
        JourneySnapshot conserved = (await _journey.ResolveCurrentEncounterAsync(plutoPlayer, EncounterResponse.MigrationConserveSupplies)).Value!;
        GameState plutoAfter = (await _store.LoadAsync(plutoPlayer)).Value!.State;

        Assert.AreEqual(plutoBefore.CommercialTrust - 1, conserved.Finances.CommercialTrust);
        Assert.AreEqual(plutoBefore.Crew.Single(item => item.Id.Value == "tomas-vale").Loyalty - 4, plutoAfter.Crew.Single(item => item.Id.Value == "tomas-vale").Loyalty);
        Assert.AreEqual(plutoBefore.AllFactionStandings.Single(item => item.FactionId == FactionIds.TerranContinuityAuthority).Standing - 2, plutoAfter.AllFactionStandings.Single(item => item.FactionId == FactionIds.TerranContinuityAuthority).Standing);
        Assert.IsTrue(plutoAfter.Crew.Single(item => item.Id.Value == "tomas-vale").Memories!.Any(item => item.Kind == "humanitarian-refusal"));

        const string ceresPlayer = "ceres-evasion";
        await PrepareSecondVoyageAsync(ceresPlayer, "kuiper-ceres-repair-freight", RepairService.Deferred);
        await _journey.BeginVoyageAsync(ceresPlayer);
        await _journey.AdvanceVoyageAsync(ceresPlayer);
        GameState ceresBefore = (await _store.LoadAsync(ceresPlayer)).Value!.State;
        await _journey.ResolveCurrentEncounterAsync(ceresPlayer, EncounterResponse.DebrisMaraEvasion);
        GameState ceresAfter = (await _store.LoadAsync(ceresPlayer)).Value!.State;

        Assert.AreEqual(ceresBefore.Ship.FuelPercent - 4, ceresAfter.Ship.FuelPercent);
        Assert.AreEqual(ceresBefore.Ship.DriveWearPercent + 4, ceresAfter.Ship.DriveWearPercent);
        Assert.AreEqual(ceresBefore.Ship.HullWearPercent + 1, ceresAfter.Ship.HullWearPercent);
        Assert.IsTrue(ceresAfter.Crew.Single(item => item.Id.Value == "mara-venn").Memories!.Any(item => item.Kind == "evasive-response"));
    }

    [TestMethod]
    public async Task RejectedTurnaroundCommandsKeepStableCodesAndCanonicalState()
    {
        const string player = "stable-errors";
        await CompleteFirstContractAsync(player);

        await AssertAtomicRejectionAsync(player, () => _turnaround.AuthorizeDepartureAsync(player), CommandErrorCodes.ContractSelectionRequired);
        await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Money = new Credits(100) }), default);
        await AssertAtomicRejectionAsync(player, () => _turnaround.ServiceLienAsync(player), CommandErrorCodes.InsufficientCredits);
        await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Time = state.Turnaround!.OffersExpireAt.AddHours(1) }), default);
        await AssertAtomicRejectionAsync(player, () => _turnaround.SelectContractAsync(player, new ContractId("tca-pluto-migration-support")), CommandErrorCodes.OfferExpired);

        const string capacityPlayer = "capacity-error";
        await CompleteFirstContractAsync(capacityPlayer);
        await _sessions.MutateAsync(capacityPlayer, state => CommandResult<GameState>.Success(state with
        {
            Cargo = [.. state.Cargo, new CargoLineState(new CommodityId("actuator-assemblies"), new Tonnes(60), false, false)],
        }), default);
        await AssertAtomicRejectionAsync(capacityPlayer, () => _turnaround.SelectContractAsync(capacityPlayer, new ContractId("tca-pluto-migration-support")), CommandErrorCodes.InsufficientCapacity);
    }

    [TestMethod]
    public async Task LateSecondDeliveryAppliesPenaltyAndStillPersistsDestinationHistory()
    {
        const string player = "late-ceres";
        await PrepareSecondVoyageAsync(player, "kuiper-ceres-repair-freight", RepairService.IlyaFieldService);
        await _journey.BeginVoyageAsync(player);
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ResolveCurrentEncounterAsync(player, EncounterResponse.DebrisIlyaRepair);
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ArriveAsync(player);
        await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Time = state.Contract.Deadline.AddHours(1) }), default);
        GameState before = (await _store.LoadAsync(player)).Value!.State;

        JourneySnapshot failed = (await _journey.DeliverCargoAsync(player)).Value!;
        GameState after = (await _store.LoadAsync(player)).Value!.State;

        Assert.AreEqual("Failed", failed.Contract.Status);
        Assert.AreEqual(JourneyPhase.Turnaround, failed.Phase);
        Assert.AreEqual(before.Money.Value - 5_000, after.Money.Value);
        Assert.AreEqual(before.AllFactionStandings.Single(item => item.FactionId == FactionIds.KuiperSyndicates).Standing - 4, after.AllFactionStandings.Single(item => item.FactionId == FactionIds.KuiperSyndicates).Standing);
        Assert.AreEqual(ContractStatus.Failed, after.AllJourneyHistory.Single(item => item.VoyageNumber == 2).DestinationManifest?.SettlementStatus);
    }

    [TestMethod]
    public async Task BothSecondRoutesAreDeterministicForIdenticalAcceptedCommandSequences()
    {
        foreach (string destination in new[] { "tca-pluto-migration-support", "kuiper-ceres-repair-freight" })
        {
            string first = $"det-a-{destination}";
            string second = $"det-b-{destination}";
            await CompleteBranchAsync(first, destination);
            await CompleteBranchAsync(second, destination);
            GameState a = (await _store.LoadAsync(first)).Value!.State;
            GameState b = (await _store.LoadAsync(second)).Value!.State;
            Assert.AreEqual(GameStateCanonicalizer.Serialize(a), GameStateCanonicalizer.Serialize(b), destination);
        }
    }

    [TestMethod]
    public async Task SchemaTwoMigrationPreservesAuthorityAndDerivesSchemaFiveState()
    {
        const string player = "schema-two";
        await _station.StartNewGameAsync(player, "Migration Test", false);
        await _station.AcknowledgeBriefingAsync(player);
        GameState current = (await _store.LoadAsync(player)).Value!.State;
        GameState schema2 = current with
        {
            SchemaVersion = 2,
            Contracts = null,
            StationMarkets = null,
            Lien = null,
            Maintenance = null,
            FactionStandings = null,
            Turnaround = null,
            JourneyHistory = null,
            StationVisits = null,
            Crew = current.Crew.Select(item => item with { Memories = null }).ToArray(),
        };
        string canonical = GameStateCanonicalizer.Serialize(schema2);
        string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        GameSaveEnvelope envelope = new(GameSaveEnvelope.CurrentGameVersion, "vertical-slice-v1", 2, schema2.Seed, schema2.CommandSequence, checksum, schema2);
        await _store.SaveAsync(player, envelope, true);

        TurnaroundSnapshot? ignored = null;
        StationOperationsSnapshot migrated = (await _station.ResumeGameAsync(player)).Value!;
        GameState rewritten = (await _store.LoadAsync(player)).Value!.State;

        Assert.IsNull(ignored);
        Assert.AreEqual(5, migrated.SchemaVersion);
        Assert.AreEqual("vertical-slice-v4", migrated.ContentPackVersion);
        Assert.AreEqual(current.CommandSequence, rewritten.CommandSequence);
        Assert.AreEqual(current.Time, rewritten.Time);
        Assert.AreEqual(current.Money, rewritten.Money);
        Assert.AreEqual(current.CargoLoaded, rewritten.CargoLoaded);
        Assert.AreEqual(new Credits(72_000), rewritten.Lien?.Principal);
        Assert.HasCount(4, rewritten.Crew);
        Assert.IsTrue(rewritten.Crew.All(item => item.Memories is not null));
    }

    private async Task CompleteBranchAsync(string player, string contractId)
    {
        await CompleteFirstContractAsync(player);
        await _turnaround.ServiceLienAsync(player);
        await _turnaround.RepairAsync(player, RepairService.Certified);
        await _turnaround.RestCrewAsync(player, CrewRestService.FullLayover);
        await _turnaround.SelectContractAsync(player, new ContractId(contractId));
        await _turnaround.AuthorizeDepartureAsync(player);
        await _journey.BeginVoyageAsync(player);
        JourneySnapshot contact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        EncounterResponse response = contractId.StartsWith("tca", StringComparison.Ordinal) ? EncounterResponse.MigrationMedicalAssist : EncounterResponse.DebrisIlyaRepair;
        Assert.AreEqual(response, contact.Encounter!.Responses.Single(item => item.Response == response).Response);
        await _journey.ResolveCurrentEncounterAsync(player, response);
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ArriveAsync(player);
        await _journey.DeliverCargoAsync(player);
    }

    private async Task PrepareSecondVoyageAsync(string player, string contractId, RepairService repair)
    {
        await CompleteFirstContractAsync(player);
        await _turnaround.ServiceLienAsync(player);
        await _turnaround.RepairAsync(player, repair);
        await _turnaround.RestCrewAsync(player, CrewRestService.FullLayover);
        await _turnaround.SelectContractAsync(player, new ContractId(contractId));
        await _turnaround.AuthorizeDepartureAsync(player);
    }

    private async Task AssertAtomicRejectionAsync(string player, Func<ValueTask<CommandResult<TurnaroundSnapshot>>> command, string errorCode)
    {
        GameState before = (await _store.LoadAsync(player)).Value!.State;
        CommandResult<TurnaroundSnapshot> result = await command();
        GameState after = (await _store.LoadAsync(player)).Value!.State;
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(errorCode, result.Error?.Code);
        Assert.AreEqual(before.CommandSequence, after.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before), GameStateCanonicalizer.Serialize(after));
    }

    private async Task CompleteFirstContractAsync(string player)
    {
        Assert.IsTrue((await _station.StartNewGameAsync(player, "Turnaround Test", false)).IsSuccess);
        await _station.AcknowledgeBriefingAsync(player);
        await _station.AcceptContractAsync(player);
        await _station.AssignEngineerAsync(player, EngineerAssignment.InspectDrive);
        await _station.AuthorizeDepartureAsync(player);
        await _journey.BeginVoyageAsync(player);
        JourneySnapshot contact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        await _journey.ResolveCurrentEncounterAsync(player, contact.Encounter!.Responses.First(item => item.IsAvailable).Response!.Value);
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ArriveAsync(player);
        JourneySnapshot settled = (await _journey.DeliverCargoAsync(player)).Value!;
        Assert.AreEqual(JourneyPhase.Turnaround, settled.Phase);
    }

    private static int ParseDayHours(string label)
    {
        string[] parts = label.Split('·', StringSplitOptions.TrimEntries);
        int day = int.Parse(parts[0].Replace("Day ", string.Empty, StringComparison.Ordinal));
        int hour = int.Parse(parts[1].Split(':')[0]);
        return checked(day * 24 + hour);
    }
}
