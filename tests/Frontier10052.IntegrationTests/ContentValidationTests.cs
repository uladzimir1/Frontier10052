using Frontier10052.Content;
using Frontier10052.Domain;

namespace Frontier10052.IntegrationTests;

[TestClass]
public sealed class ContentValidationTests
{
    [TestMethod]
    public void VerticalSliceV4IsCompleteAndValid()
    {
        VerticalSliceContentPack pack = VerticalSliceContentPack.Create();
        ContentValidationResult result = ContentPackValidator.Validate(pack);

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual("vertical-slice-v4", pack.Version);
        Assert.HasCount(5, pack.Stations);
        Assert.HasCount(4, pack.Crew);
        Assert.HasCount(7, pack.Commodities);
        Assert.AreEqual(new Tonnes(72), pack.Ship.CargoCapacity);
        Assert.AreEqual(84, pack.Ship.FuelPercent);
        Assert.AreEqual(17, pack.Ship.DriveWearPercent);
        Assert.AreEqual(new Tonnes(18), pack.Contract.Quantity);
        Assert.AreEqual(52, pack.Contract.DeadlineHours);
        Assert.AreEqual(new Credits(12_000), pack.Contract.Reward);
        Assert.AreEqual(new Credits(4_500), pack.Contract.FailurePenalty);
        Assert.AreEqual(38, pack.Route.DurationHours);
        Assert.AreEqual(18, pack.Route.EncounterAtHour);
        Assert.HasCount(4, pack.Contracts);
        Assert.HasCount(5, pack.Routes);
        Assert.HasCount(5, pack.Encounters);
        Assert.HasCount(5, pack.MarsPrices);
        Assert.HasCount(1, pack.Information);
        Assert.HasCount(3, pack.StationServices);
        Assert.HasCount(1, pack.StationEvents);
        Assert.HasCount(2, pack.OutboundLeads);
        Assert.IsTrue(pack.Routes.Where(route => route.DestinationStationId.Value == "sirius-meridian-exchange")
            .All(route => route.Checkpoints?.Count == 5 && route.EncounterPool.Count == 0));
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

    [TestMethod]
    public void ValidatorRejectsSiriusDuplicateReferencesOrderingPinchAndFeasibility()
    {
        VerticalSliceContentPack valid = VerticalSliceContentPack.Create();
        ContractDefinition siriusContract = valid.Contracts.Single(item => item.Id.Value == "scc-sirius-industrial-forecast");
        RouteDefinition siriusRoute = valid.Routes.Single(item => item.Id.Value == "ceres-sirius-intelligence-run");
        VerticalSliceContentPack invalid = valid with
        {
            Information = [.. valid.Information, valid.Information[0]],
            Contracts = valid.Contracts.Select(item => item.Id == siriusContract.Id
                ? item with { InformationId = new InformationId("missing-information") }
                : item).ToArray(),
            Routes = valid.Routes.Select(item => item.Id == siriusRoute.Id
                ? item with
                {
                    DurationHours = 220,
                    PinchCost = 0,
                    Checkpoints = [.. item.Checkpoints!.Reverse()],
                }
                : item).ToArray(),
        };

        ContentValidationResult result = ContentPackValidator.Validate(invalid);
        string[] codes = result.Errors.Select(error => error.Code).ToArray();

        CollectionAssert.Contains(codes, "duplicate.information");
        CollectionAssert.Contains(codes, "reference.contract-information");
        CollectionAssert.Contains(codes, "route.checkpoint-order");
        CollectionAssert.Contains(codes, "route.checkpoint-bounds");
        CollectionAssert.Contains(codes, "route.checkpoint-pinch");
        CollectionAssert.Contains(codes, "route.deadline");
    }

    [TestMethod]
    public void ValidatorRejectsAftermathTotalsCoverageReferencesAndMissingLeadText()
    {
        VerticalSliceContentPack valid = VerticalSliceContentPack.Create();
        StationEventDefinition stationEvent = valid.StationEvents.Single();
        Dictionary<string, string> text = new(valid.Localization, StringComparer.Ordinal);
        text.Remove("lead.procyon.title");
        VerticalSliceContentPack invalid = valid with
        {
            StationEvents =
            [
                stationEvent with
                {
                    CommodityId = new CommodityId("missing-actuators"),
                    AvailableUnits = 11,
                    CrewResponses = ["CaptainsOrder"],
                    AllocationResponses = ["AuditedSplit"],
                },
            ],
            OutboundLeads = [valid.OutboundLeads[0] with { EventId = new StationEventId("missing-event"), UnlocksFor = "MissingAllocation" }],
            Localization = text,
        };

        string[] codes = ContentPackValidator.Validate(invalid).Errors.Select(item => item.Code).ToArray();
        CollectionAssert.Contains(codes, "reference.station-event-commodity");
        CollectionAssert.Contains(codes, "station-event.actuator-total");
        CollectionAssert.Contains(codes, "station-event.crew-responses");
        CollectionAssert.Contains(codes, "station-event.allocation-responses");
        CollectionAssert.Contains(codes, "reference.outbound-lead-event");
        CollectionAssert.Contains(codes, "outbound-lead.unlock");
        CollectionAssert.Contains(codes, "outbound-lead.count");
        CollectionAssert.Contains(codes, "localization.missing");
    }
}
