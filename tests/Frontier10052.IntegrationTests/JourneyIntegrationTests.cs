using System.Security.Cryptography;
using System.Text;
using Frontier10052.Domain;
using Frontier10052.Gameplay.Journey;
using Frontier10052.Gameplay.Operations;
using Frontier10052.Gameplay.Persistence;
using Frontier10052.Infrastructure;
using Frontier10052.Simulation;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class JourneyIntegrationTests
{
    private string _directory = null!;
    private JsonGameSaveStore _store = null!;
    private GameSessionCoordinator _sessions = null!;
    private StationOperationsService _station = null!;
    private JourneyService _journey = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Frontier10052.JourneyTests", Guid.NewGuid().ToString("N"));
        _store = new JsonGameSaveStore(_directory);
        _sessions = new GameSessionCoordinator(_store);
        _station = new StationOperationsService(_sessions);
        _journey = new JourneyService(_sessions);
    }

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [TestMethod]
    public async Task CompleteNoTradeJourneyPersistsMarsDeliveryAndReloadsExactly()
    {
        const string player = "mars-loop";
        await PrepareAsync(player, EngineerAssignment.InspectDrive);
        JourneySnapshot authorized = (await _journey.ResumeAsync(player)).Value!;
        JourneySnapshot departed = (await _journey.BeginVoyageAsync(player)).Value!;
        JourneySnapshot contact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        JourneyAction response = contact.Encounter!.Responses.First(item => item.IsAvailable);
        JourneySnapshot resolved = (await _journey.ResolveCurrentEncounterAsync(player, response.Response!.Value)).Value!;
        JourneySnapshot approach = (await _journey.AdvanceVoyageAsync(player)).Value!;
        JourneySnapshot docked = (await _journey.ArriveAsync(player)).Value!;
        JourneySnapshot completed = (await _journey.DeliverCargoAsync(player)).Value!;
        GameState persisted = (await _store.LoadAsync(player)).Value!.State;
        JourneySnapshot reloaded = (await _journey.ResumeAsync(player)).Value!;
        GameState persistedAgain = (await _store.LoadAsync(player)).Value!.State;

        Assert.AreEqual(JourneyPhase.DepartureAuthorized, authorized.Phase);
        Assert.AreEqual(72, departed.Ship.FuelPercent);
        Assert.AreEqual(20, departed.Ship.DriveWearPercent);
        Assert.AreEqual(JourneyPhase.EncounterPending, contact.Phase);
        Assert.AreEqual(JourneyPhase.InTransit, resolved.Phase);
        Assert.AreEqual(JourneyPhase.Approach, approach.Phase);
        Assert.IsGreaterThanOrEqualTo(7, approach.Contract.HoursRemaining);
        Assert.AreEqual(JourneyPhase.Docked, docked.Phase);
        Assert.AreEqual(JourneyPhase.Turnaround, completed.Phase);
        Assert.AreEqual("Completed", completed.Contract.Status);
        Assert.AreEqual(40_000, completed.Finances.Credits);
        Assert.AreEqual(2 + (response.Response == EncounterResponse.InspectionStandardCompliance ? 1 : 0), completed.Finances.CommercialTrust);
        Assert.AreEqual(completed.CommandSequence, reloaded.CommandSequence);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(persisted), GameStateCanonicalizer.Serialize(persistedAgain));
    }

    [TestMethod]
    public async Task ExplicitMarsSaleUsesRevealedPriceAndSupportsPartialQuantity()
    {
        const string player = "seller";
        await PrepareAsync(player, EngineerAssignment.AnalyzeCourierManifest, new CommodityId("coolant-couplings"), 4);
        await TravelAndDockAsync(player);
        JourneySnapshot before = (await _journey.ResumeAsync(player)).Value!;
        DestinationBidPresentation bid = before.DestinationMarket.Single(item => item.CommodityId == "coolant-couplings");
        JourneySnapshot partial = (await _journey.SellCargoAsync(player, new CommodityId("coolant-couplings"), new Tonnes(2))).Value!;
        JourneySnapshot sold = (await _journey.SellCargoAsync(player, new CommodityId("coolant-couplings"), new Tonnes(2))).Value!;

        Assert.AreEqual(2, partial.DestinationMarket.Single(item => item.CommodityId == "coolant-couplings").OwnedQuantity);
        Assert.AreEqual(0, sold.DestinationMarket.Single(item => item.CommodityId == "coolant-couplings").OwnedQuantity);
        Assert.HasCount(2, sold.Sales);
        Assert.AreEqual(before.Finances.Credits + bid.UnitPrice * 4, sold.Finances.Credits);
        Assert.AreEqual(240, bid.Margin);
    }

    [TestMethod]
    public async Task RejectedJourneyCommandDoesNotIncrementOrPartiallyMutateState()
    {
        const string player = "reject";
        await _station.StartNewGameAsync(player, "Reject Test", false);
        GameState before = (await _store.LoadAsync(player)).Value!.State;
        CommandResult<JourneySnapshot> rejected = await _journey.BeginVoyageAsync(player);
        GameState after = (await _store.LoadAsync(player)).Value!.State;

        Assert.IsFalse(rejected.IsSuccess);
        Assert.AreEqual(CommandErrorCodes.DepartureNotAuthorized, rejected.Error?.Code);
        Assert.AreEqual(GameStateCanonicalizer.Serialize(before), GameStateCanonicalizer.Serialize(after));
    }

    [TestMethod]
    public async Task SchemaOneSaveMigratesWithoutChangingSequenceTimeOrManifest()
    {
        const string player = "legacy";
        await PrepareAsync(player, EngineerAssignment.InspectDrive);
        GameState current = (await _store.LoadAsync(player)).Value!.State;
        GameState legacyState = current with { SchemaVersion = 1, Journey = null, CommercialTrust = 0 };
        string canonical = GameStateCanonicalizer.Serialize(legacyState);
        string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        GameSaveEnvelope legacy = new(GameSaveEnvelope.CurrentGameVersion, "vertical-slice-v0", 1, legacyState.Seed, legacyState.CommandSequence, checksum, legacyState);
        await _store.SaveAsync(player, legacy, true);

        JourneySnapshot migrated = (await _journey.ResumeAsync(player)).Value!;
        GameSaveEnvelope rewritten = (await _store.LoadAsync(player)).Value!;

        Assert.AreEqual(3, rewritten.StateSchemaVersion);
        Assert.AreEqual("vertical-slice-v2", rewritten.ContentPackVersion);
        Assert.AreEqual(current.CommandSequence, migrated.CommandSequence);
        Assert.AreEqual(current.Time, rewritten.State.Time);
        Assert.AreEqual(current.DepartureManifest?.AuthorizedAtCommandSequence, rewritten.State.DepartureManifest?.AuthorizedAtCommandSequence);
        CollectionAssert.AreEqual(current.DepartureManifest!.Cargo.ToArray(), rewritten.State.DepartureManifest!.Cargo.ToArray());
        Assert.AreEqual(JourneyPhase.DepartureAuthorized, migrated.Phase);
    }

    [TestMethod]
    public async Task AllEncounterResponsesHaveDeterministicRecoverableConsequences()
    {
        foreach ((string id, EncounterResponse response) in Cases())
        {
            string player = $"case-{response}";
            await PrepareAsync(player, EngineerAssignment.InspectDrive, response == EncounterResponse.PirateDumpSpeculativeCargo ? new CommodityId("coolant-couplings") : null, 1);
            await _journey.BeginVoyageAsync(player);
            await _journey.AdvanceVoyageAsync(player);
            await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Journey = state.Journey! with { Encounter = state.Journey!.Encounter! with { Id = new EncounterId(id) } } }), default);
            JourneySnapshot before = (await _journey.ResumeAsync(player)).Value!;
            CommandResult<JourneySnapshot> result = await _journey.ResolveCurrentEncounterAsync(player, response);

            Assert.IsTrue(result.IsSuccess, result.Error?.Message);
            Assert.AreEqual(JourneyPhase.InTransit, result.Value!.Phase);
            Assert.IsGreaterThan(before.CommandSequence, result.Value.CommandSequence);
            Assert.IsLessThanOrEqualTo(7, result.Value.Route.DelayHours);
        }
    }

    [TestMethod]
    public async Task DriveInspectionMakesMechanicalFieldRepairFasterAndSafer()
    {
        await PrepareAsync("repair-inspected", EngineerAssignment.InspectDrive);
        await PrepareAsync("repair-analyzed", EngineerAssignment.AnalyzeCourierManifest);
        JourneySnapshot inspected = await ForceMechanicalRepairAsync("repair-inspected");
        JourneySnapshot analyzed = await ForceMechanicalRepairAsync("repair-analyzed");

        Assert.AreEqual(2, inspected.Route.DelayHours);
        Assert.AreEqual(5, analyzed.Route.DelayHours);
        Assert.AreEqual(21, inspected.Ship.DriveWearPercent);
        Assert.AreEqual(23, analyzed.Ship.DriveWearPercent);
    }

    [TestMethod]
    public async Task FailedDeliveryAppliesPenaltyOnlyOnce()
    {
        const string player = "late-delivery";
        await PrepareAsync(player, EngineerAssignment.InspectDrive);
        await TravelAndDockAsync(player);
        await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Time = state.Contract.Deadline.AddHours(1) }), default);
        JourneySnapshot before = (await _journey.ResumeAsync(player)).Value!;
        JourneySnapshot failed = (await _journey.DeliverCargoAsync(player)).Value!;
        CommandResult<JourneySnapshot> repeated = await _journey.DeliverCargoAsync(player);

        Assert.AreEqual("Failed", failed.Contract.Status);
        Assert.AreEqual(before.Finances.Credits - 4_500, failed.Finances.Credits);
        Assert.IsFalse(repeated.IsSuccess);
        Assert.AreEqual(CommandErrorCodes.ContractAlreadySettled, repeated.Error?.Code);
        Assert.AreEqual(failed.Finances.Credits, (await _journey.ResumeAsync(player)).Value!.Finances.Credits);
    }

    private async Task PrepareAsync(string player, EngineerAssignment assignment, CommodityId? commodity = null, int quantity = 0)
    {
        Assert.IsTrue((await _station.StartNewGameAsync(player, "Mars Test", false)).IsSuccess);
        await _station.AcknowledgeBriefingAsync(player);
        await _station.AcceptContractAsync(player);
        if (commodity is not null) await _station.PurchaseCargoAsync(player, commodity.Value, new Tonnes(quantity));
        await _station.AssignEngineerAsync(player, assignment);
        await _station.AuthorizeDepartureAsync(player);
    }

    private async Task TravelAndDockAsync(string player)
    {
        await _journey.BeginVoyageAsync(player);
        JourneySnapshot contact = (await _journey.AdvanceVoyageAsync(player)).Value!;
        await _journey.ResolveCurrentEncounterAsync(player, contact.Encounter!.Responses.First(item => item.IsAvailable).Response!.Value);
        await _journey.AdvanceVoyageAsync(player);
        await _journey.ArriveAsync(player);
    }

    private async Task<JourneySnapshot> ForceMechanicalRepairAsync(string player)
    {
        await _journey.BeginVoyageAsync(player);
        await _journey.AdvanceVoyageAsync(player);
        await _sessions.MutateAsync(player, state => CommandResult<GameState>.Success(state with { Journey = state.Journey! with { Encounter = state.Journey!.Encounter! with { Id = new EncounterId("drive-coolant-failure") } } }), default);
        return (await _journey.ResolveCurrentEncounterAsync(player, EncounterResponse.MechanicalFieldRepair)).Value!;
    }

    private static IEnumerable<(string, EncounterResponse)> Cases()
    {
        yield return ("sol-transit-inspection", EncounterResponse.InspectionStandardCompliance);
        yield return ("sol-transit-inspection", EncounterResponse.InspectionMedicalPriority);
        yield return ("drive-coolant-failure", EncounterResponse.MechanicalFieldRepair);
        yield return ("drive-coolant-failure", EncounterResponse.MechanicalReducedBurn);
        yield return ("ceres-lane-pirate-demand", EncounterResponse.PiratePayDemand);
        yield return ("ceres-lane-pirate-demand", EncounterResponse.PirateDumpSpeculativeCargo);
        yield return ("ceres-lane-pirate-demand", EncounterResponse.PirateHardBurn);
    }
}
