using Frontier10052.Content;
using Frontier10052.Domain;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class ContentValidationTests
{
    [TestMethod]
    public void VerticalSliceV2IsCompleteAndValid()
    {
        VerticalSliceContentPack pack = VerticalSliceContentPack.Create();
        ContentValidationResult result = ContentPackValidator.Validate(pack);

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual("vertical-slice-v2", pack.Version);
        Assert.HasCount(4, pack.Stations);
        Assert.HasCount(4, pack.Crew);
        Assert.HasCount(6, pack.Commodities);
        Assert.AreEqual(new Tonnes(72), pack.Ship.CargoCapacity);
        Assert.AreEqual(84, pack.Ship.FuelPercent);
        Assert.AreEqual(17, pack.Ship.DriveWearPercent);
        Assert.AreEqual(new Tonnes(18), pack.Contract.Quantity);
        Assert.AreEqual(52, pack.Contract.DeadlineHours);
        Assert.AreEqual(new Credits(12_000), pack.Contract.Reward);
        Assert.AreEqual(new Credits(4_500), pack.Contract.FailurePenalty);
        Assert.AreEqual(38, pack.Route.DurationHours);
        Assert.AreEqual(18, pack.Route.EncounterAtHour);
        Assert.HasCount(3, pack.Contracts);
        Assert.HasCount(3, pack.Routes);
        Assert.HasCount(5, pack.Encounters);
        Assert.HasCount(5, pack.MarsPrices);
    }

    [TestMethod]
    public void ValidatorRejectsDuplicatesMissingReferencesImpossibleCapacityAndMissingText()
    {
        VerticalSliceContentPack valid = VerticalSliceContentPack.Create();
        Dictionary<string, string> missingText = new(valid.Localization, StringComparer.Ordinal);
        missingText.Remove("crew.ilya.name");
        VerticalSliceContentPack invalid = valid with
        {
            Stations = [.. valid.Stations, valid.Stations[0]],
            Contracts = [valid.Contracts[0] with { OriginStationId = new StationId("missing-origin"), Quantity = new Tonnes(73) }, .. valid.Contracts.Skip(1)],
            Localization = missingText,
        };

        ContentValidationResult result = ContentPackValidator.Validate(invalid);
        string[] codes = result.Errors.Select(error => error.Code).ToArray();

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(codes, "duplicate.station");
        CollectionAssert.Contains(codes, "reference.contract-origin");
        CollectionAssert.Contains(codes, "contract.capacity");
        CollectionAssert.Contains(codes, "localization.missing");
    }

    [TestMethod]
    public void ValidatorRejectsZeroContractQuantityAndInvertedProfitRange()
    {
        VerticalSliceContentPack valid = VerticalSliceContentPack.Create();
        CommodityDefinition inverted = valid.Commodities[1] with { EstimatedMarsProfitLow = 500, EstimatedMarsProfitHigh = 100 };
        VerticalSliceContentPack invalid = valid with
        {
            Contracts = [valid.Contracts[0] with { Quantity = new Tonnes(0) }, .. valid.Contracts.Skip(1)],
            Commodities = [valid.Commodities[0], inverted, .. valid.Commodities.Skip(2)],
        };

        ContentValidationResult result = ContentPackValidator.Validate(invalid);
        string[] codes = result.Errors.Select(error => error.Code).ToArray();

        CollectionAssert.Contains(codes, "contract.quantity");
        CollectionAssert.Contains(codes, "commodity.profit-range");
    }
}
