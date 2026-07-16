using System.Text.Json;

namespace Frontier10052.Domain.Tests;

[TestClass]
public sealed class ValueObjectTests
{
    [TestMethod]
    public void TypedIdentifiersCompareByTypeAndValue()
    {
        GameId first = new("game-10052-test");
        GameId same = new("game-10052-test");
        GameId other = new("game-10052-other");

        Assert.AreEqual(first, same);
        Assert.AreNotEqual(first, other);
        Assert.AreEqual("game-10052-test", first.ToString());
    }

    [TestMethod]
    public void TypedIdentifiersRejectMissingAndOversizedValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new StationId("   "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CommodityId(new string('x', 97)));
    }

    [TestMethod]
    public void CreditsAndTonnesUseCheckedNonNegativeArithmetic()
    {
        Credits cash = new(28_000);
        Credits unitPrice = new(620);
        Tonnes quantity = new(3);

        Assert.AreEqual(new Credits(1_860), unitPrice * quantity);
        Assert.AreEqual(new Credits(26_140), cash - (unitPrice * quantity));
        Assert.AreEqual(new Tonnes(21), new Tonnes(18) + quantity);
        Assert.IsTrue(cash > unitPrice);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Credits(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Tonnes(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => unitPrice - cash);
    }

    [TestMethod]
    public void GameTimeAdvancesOnlyByExplicitNonNegativeHours()
    {
        GameTime start = new(7_200);

        Assert.AreEqual(new GameTime(7_252), start.AddHours(52));
        Assert.AreEqual(7_200, start.HoursSinceStart);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => start.AddHours(-1));
    }

    [TestMethod]
    public void ValueObjectsSerializeWithStableNamedValues()
    {
        string json = JsonSerializer.Serialize(new { Game = new GameId("game-1"), Money = new Credits(42), Cargo = new Tonnes(7), Time = new GameTime(12) });

        StringAssert.Contains(json, "\"Value\":\"game-1\"");
        StringAssert.Contains(json, "\"Value\":42");
        StringAssert.Contains(json, "\"HoursSinceStart\":12");
    }

    [TestMethod]
    public void CommandFailuresExposeStablePlayerSafeCodes()
    {
        CommandResult<int> result = CommandResult<int>.Failure(CommandErrorCodes.InsufficientCapacity, "Only four tonnes remain.");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("cargo.insufficient-capacity", result.Error?.Code);
        Assert.AreEqual("Only four tonnes remain.", result.Error?.Message);
    }

    [TestMethod]
    public void RouteAndEncounterIdentifiersAreTypedValidatedAndValueEqual()
    {
        RouteId route = new("earth-mars");
        EncounterId encounter = new("inspection");

        Assert.AreEqual(new RouteId("earth-mars"), route);
        Assert.AreEqual(new EncounterId("inspection"), encounter);
        Assert.ThrowsExactly<ArgumentException>(() => new RouteId(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new EncounterId(string.Empty));
    }
}
