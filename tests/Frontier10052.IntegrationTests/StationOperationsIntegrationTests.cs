using Frontier10052.Domain;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Infrastructure;
using Frontier10052.Simulation;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class StationOperationsIntegrationTests
{
    private string _saveDirectory = null!;
    private JsonGameSaveStore _store = null!;
    private StationOperationsService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _saveDirectory = Path.Combine(Path.GetTempPath(), "Frontier10052.Tests", Guid.NewGuid().ToString("N"));
        _store = new JsonGameSaveStore(_saveDirectory);
        _service = new StationOperationsService(new GameSessionCoordinator(_store));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, recursive: true);
    }

    [TestMethod]
    public async Task StartingBalanceAndSeedAreDeterministic()
    {
        StationOperationsSnapshot first = await StartAsync("player-a", "Aster Vale");
        StationOperationsSnapshot second = await StartAsync("player-b", "Aster Vale");

        Assert.AreEqual(10052, first.Seed);
        Assert.AreEqual(0, first.CommandSequence);
        Assert.AreEqual(28_000, first.Finances.AvailableCredits);
        Assert.AreEqual(72, first.Ship.CargoCapacity);
        Assert.AreEqual(84, first.Ship.FuelPercent);
        Assert.AreEqual(17, first.Ship.DriveWearPercent);

        GameSaveEnvelope firstSave = (await _store.LoadAsync("player-a")).Value!;
        GameSaveEnvelope secondSave = (await _store.LoadAsync("player-b")).Value!;
        Assert.AreEqual(GameStateCanonicalizer.Serialize(firstSave.State), GameStateCanonicalizer.Serialize(secondSave.State));
    }

    [TestMethod]
    public async Task NewCommanderRequiresExplicitOverwriteConfirmation()
    {
        StationOperationsSnapshot initial = await StartAsync("owner", "First Call");

        CommandResult<StationOperationsSnapshot> rejected = await _service.StartNewGameAsync("owner", "Second Call", overwriteConfirmed: false);
        StationOperationsSnapshot unchanged = (await _service.ResumeGameAsync("owner")).Value!;
        CommandResult<StationOperationsSnapshot> replaced = await _service.StartNewGameAsync("owner", "Second Call", overwriteConfirmed: true);

        Assert.IsFalse(rejected.IsSuccess);
        Assert.AreEqual(CommandErrorCodes.ActiveGameExists, rejected.Error?.Code);
        Assert.AreEqual(initial.Commander.DisplayName, unchanged.Commander.DisplayName);
        Assert.IsTrue(replaced.IsSuccess);
        Assert.AreEqual("Cmdr. Second Call", replaced.Value?.Commander.DisplayName);
        Assert.AreEqual(0, replaced.Value?.CommandSequence);
    }

    [TestMethod]
    public async Task ContractAcceptanceLoadsSealedCargoWithoutChargingCredits()
    {
        await StartAsync("contract", "Mara Test");

        CommandResult<StationOperationsSnapshot> accepted = await _service.AcceptContractAsync("contract");

        Assert.IsTrue(accepted.IsSuccess);
        Assert.IsTrue(accepted.Value?.Contract.Accepted);
        Assert.AreEqual(18, accepted.Value?.Ship.CargoLoaded);
        Assert.AreEqual(28_000, accepted.Value?.Finances.AvailableCredits);
        CargoPresentation cargo = accepted.Value!.Cargo.Single();
        Assert.IsTrue(cargo.IsContractCargo);
        Assert.IsTrue(cargo.Sealed);
        Assert.AreEqual(18, cargo.Quantity);
        Assert.AreEqual(1, accepted.Value.CommandSequence);
    }

    [TestMethod]
    public async Task PurchasingValidatesQuantityStockCapacityAndMoneyWithoutPartialMutation()
    {
        await StartAsync("failures", "No Partial");
        await _service.AcceptContractAsync("failures");
        GameSaveEnvelope before = (await _store.LoadAsync("failures")).Value!;

        CommandResult<StationOperationsSnapshot> invalid = await _service.PurchaseCargoAsync("failures", new CommodityId("coolant-couplings"), new Tonnes(0));
        CommandResult<StationOperationsSnapshot> stock = await _service.PurchaseCargoAsync("failures", new CommodityId("coolant-couplings"), new Tonnes(41));
        CommandResult<StationOperationsSnapshot> capacity = await _service.PurchaseCargoAsync("failures", new CommodityId("nutrient-substrate"), new Tonnes(55));
        CommandResult<StationOperationsSnapshot> money = await _service.PurchaseCargoAsync("failures", new CommodityId("actuator-assemblies"), new Tonnes(26));
        GameSaveEnvelope after = (await _store.LoadAsync("failures")).Value!;

        Assert.AreEqual(CommandErrorCodes.InvalidQuantity, invalid.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.InsufficientStock, stock.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.InsufficientCapacity, capacity.Error?.Code);
        Assert.AreEqual(CommandErrorCodes.InsufficientCredits, money.Error?.Code);
        Assert.AreEqual(before.CommandSequence, after.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before.State), GameStateCanonicalizer.Serialize(after.State));
    }

    [TestMethod]
    public async Task ValidPurchaseUpdatesMoneyStockCargoAndCapacityAtomically()
    {
        await StartAsync("buyer", "Coolant Buyer");
        StationOperationsSnapshot accepted = (await _service.AcceptContractAsync("buyer")).Value!;
        MarketItemPresentation coolantBefore = accepted.Market.Single(item => item.Id == "coolant-couplings");

        StationOperationsSnapshot purchased = (await _service.PurchaseCargoAsync("buyer", new CommodityId("coolant-couplings"), new Tonnes(3))).Value!;
        MarketItemPresentation coolantAfter = purchased.Market.Single(item => item.Id == "coolant-couplings");

        Assert.AreEqual(21, purchased.Ship.CargoLoaded);
        Assert.AreEqual(51, purchased.Ship.CargoAvailable);
        Assert.AreEqual(37, coolantAfter.Stock);
        Assert.AreEqual(28_000 - (coolantBefore.UnitPrice * 3), purchased.Finances.AvailableCredits);
        Assert.AreEqual(3, purchased.Cargo.Single(item => item.CommodityId == "coolant-couplings").Quantity);
        Assert.AreEqual(2, purchased.CommandSequence);
    }

    [TestMethod]
    public async Task EngineerChoicesHaveDifferentConsequences()
    {
        await StartAsync("analyst", "Data First");
        await StartAsync("inspector", "Safety First");

        StationOperationsSnapshot analyzed = (await _service.AssignEngineerAsync("analyst", EngineerAssignment.AnalyzeCourierManifest)).Value!;
        StationOperationsSnapshot inspected = (await _service.AssignEngineerAsync("inspector", EngineerAssignment.InspectDrive)).Value!;

        Assert.AreEqual(67, analyzed.Reports.Single(report => report.Id == "mars-convoy-arrival").ConfidencePercent);
        StringAssert.Contains(analyzed.Engineer.ReliabilityRisk, "not been inspected");
        Assert.AreEqual(38, inspected.Reports.Single(report => report.Id == "mars-convoy-arrival").ConfidencePercent);
        StringAssert.StartsWith(inspected.Engineer.ReliabilityRisk, "Low");
    }

    [TestMethod]
    public async Task OptionalNoTradeDeparturePersistsCompleteManifestWithoutAdvancingTimeOrLocation()
    {
        StationOperationsSnapshot start = await StartAsync("departure", "Direct Relief");
        await _service.AcknowledgeBriefingAsync("departure");
        await _service.AcceptContractAsync("departure");
        await _service.AssignEngineerAsync("departure", EngineerAssignment.InspectDrive);

        StationOperationsSnapshot authorized = (await _service.AuthorizeDepartureAsync("departure")).Value!;
        GameState persisted = (await _store.LoadAsync("departure")).Value!.State;
        StationOperationsSnapshot reloaded = (await _service.ResumeGameAsync("departure")).Value!;
        GameState persistedAfterReload = (await _store.LoadAsync("departure")).Value!.State;

        Assert.IsTrue(authorized.Departure.Authorized);
        Assert.AreEqual(4, authorized.CommandSequence);
        Assert.AreEqual(start.GameTimeLabel, authorized.GameTimeLabel);
        Assert.AreEqual(start.LocationName, authorized.LocationName);
        Assert.AreEqual(18, authorized.Departure.Manifest?.Cargo.Single().Quantity);
        Assert.AreEqual(28_000, authorized.Departure.Manifest?.RemainingCredits);
        Assert.AreEqual("No speculative market position recorded", authorized.Departure.Manifest?.ReportDecision);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(persisted), GameStateCanonicalizer.Serialize(persistedAfterReload));
        Assert.AreEqual(authorized.CommandSequence, reloaded.CommandSequence);
        Assert.AreEqual(authorized.Departure.Manifest?.CommandSequence, reloaded.Departure.Manifest?.CommandSequence);
        CollectionAssert.AreEqual(authorized.Cargo.ToArray(), reloaded.Cargo.ToArray());
    }

    [TestMethod]
    public async Task IdenticalAcceptedCommandSequencesProduceIdenticalCanonicalState()
    {
        await StartAsync("replay-a", "Replay Commander");
        await StartAsync("replay-b", "Replay Commander");

        foreach (string player in new[] { "replay-a", "replay-b" })
        {
            await _service.AcknowledgeBriefingAsync(player);
            await _service.AcceptContractAsync(player);
            await _service.RecordReportDecisionAsync(player, ReportDecision.CoolantAlternative);
            await _service.PurchaseCargoAsync(player, new CommodityId("coolant-couplings"), new Tonnes(4));
            await _service.AssignEngineerAsync(player, EngineerAssignment.AnalyzeCourierManifest);
            await _service.AuthorizeDepartureAsync(player);
        }

        GameState first = (await _store.LoadAsync("replay-a")).Value!.State;
        GameState second = (await _store.LoadAsync("replay-b")).Value!.State;
        Assert.AreEqual(6, first.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(first), GameStateCanonicalizer.Serialize(second));
    }

    [TestMethod]
    public async Task SaveEnvelopeMetadataMatchesStateAndAtomicWritesLeaveNoTemporaryFiles()
    {
        await StartAsync("atomic", "Atomic Save");
        await _service.AcknowledgeBriefingAsync("atomic");
        GameSaveEnvelope envelope = (await _store.LoadAsync("atomic")).Value!;

        Assert.AreEqual(GameSaveEnvelope.CurrentGameVersion, envelope.GameVersion);
        Assert.AreEqual("vertical-slice-v4", envelope.ContentPackVersion);
        Assert.AreEqual(GameState.CurrentSchemaVersion, envelope.StateSchemaVersion);
        Assert.AreEqual(10052, envelope.Seed);
        Assert.AreEqual(envelope.State.CommandSequence, envelope.CommandSequence);
        Assert.AreEqual(64, envelope.StateChecksum.Length);
        Assert.IsEmpty(Directory.GetFiles(_saveDirectory, "*.tmp", SearchOption.TopDirectoryOnly));
        Assert.HasCount(1, Directory.GetFiles(_saveDirectory, "journey-*.json", SearchOption.TopDirectoryOnly));
    }

    [TestMethod]
    public async Task CorruptSaveIsQuarantinedAndPlayerCanRecoverWithNewJourney()
    {
        await StartAsync("corrupt", "Before Damage");
        string path = _store.GetSavePath("corrupt");
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        CommandResult<StationOperationsSnapshot> result = await _service.ResumeGameAsync("corrupt");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(CommandErrorCodes.SaveCorrupt, result.Error?.Code);
        Assert.IsFalse(File.Exists(path));
        Assert.HasCount(1, Directory.GetFiles(_saveDirectory, "*.corrupt-*.json", SearchOption.TopDirectoryOnly));

        CommandResult<StationOperationsSnapshot> recovered = await _service.StartNewGameAsync("corrupt", "Recovered", overwriteConfirmed: false);
        Assert.IsTrue(recovered.IsSuccess);
    }

    private async Task<StationOperationsSnapshot> StartAsync(string playerKey, string callsign)
    {
        CommandResult<StationOperationsSnapshot> result = await _service.StartNewGameAsync(playerKey, callsign, overwriteConfirmed: false);
        Assert.IsTrue(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
