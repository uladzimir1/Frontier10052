using Frontier10052.Content;
using Frontier10052.Domain;
using Frontier10052.Simulation;

namespace Frontier10052.Gameplay.Operations;

internal static class VerticalSliceGameFactory
{
    public const int Seed = 10052;
    private static readonly GameTime StartTime = new(7_200);

    public static GameState Create(VerticalSliceContentPack pack, string callsign)
    {
        IReadOnlyList<MarketListingState> listings = pack.Commodities.Select(commodity =>
            new MarketListingState(
                commodity.Id,
                MarketPricing.CalculateUnitPrice(commodity.BasePrice, commodity.InitialStock, commodity.TargetStock),
                commodity.InitialStock,
                commodity.Purchasable)).ToArray();

        DestinationMarketState destinationMarket = CreateDestinationMarket(pack, listings);

        IReadOnlyList<MarketReportState> reports = pack.Reports.Select(report =>
            new MarketReportState(
                report.Id,
                new GameTime(StartTime.HoursSinceStart - report.ObservedHoursAgo),
                report.ConfidencePercent,
                report.Verified,
                false)).ToArray();

        IReadOnlyList<CrewMemberState> crew = pack.Crew.Select(member =>
            new CrewMemberState(member.Id, member.Loyalty, member.Fatigue, member.Available, "Awaiting departure assignment", [])).ToArray();

        ContractDefinition contract = pack.Contract;
        string gameSlug = new string(callsign.ToLowerInvariant().Where(char.IsLetterOrDigit).Take(24).ToArray());

        ContractState initialContract = new(
            contract.Id,
            contract.OriginStationId,
            contract.DestinationStationId,
            contract.CommodityId,
            contract.Quantity,
            StartTime.AddHours(contract.DeadlineHours),
            contract.Reward,
            contract.FailurePenalty,
            ContractStatus.Offered,
            contract.IssuerFactionId,
            contract.IsTurnaroundOffer,
            StartTime);

        IReadOnlyList<StationMarketState> stationMarkets = pack.StationMarkets.Select(definition =>
            definition.StationId == pack.Ship.StationId
                ? new StationMarketState(definition.StationId, listings)
                : new StationMarketState(definition.StationId, [])).ToArray();

        return new GameState(
            GameState.CurrentSchemaVersion,
            Seed,
            0,
            new GameId($"game-{Seed}-{gameSlug}"),
            StartTime,
            new CommanderState(callsign.Trim()),
            new ShipState(pack.Ship.Id, pack.Ship.StationId, pack.Ship.CargoCapacity, pack.Ship.FuelPercent, pack.Ship.DriveWearPercent),
            crew,
            new StationMarketState(pack.Ship.StationId, listings),
            reports,
            initialContract,
            [],
            new Credits(28_000),
            false,
            null,
            null,
            "Ilya is available for one pre-departure task.",
            "Elevated · 17% drive wear has not been inspected for this departure",
            false,
            null,
            new JourneyState(JourneyPhase.DockedAtOrigin, pack.Ship.StationId, null, null, destinationMarket, [], "Wayfarer is berthed at Earth Heritage Station.", 1, contract.Id),
            0,
            [initialContract],
            stationMarkets,
            new LienState(new Credits(72_000), null, []),
            new MaintenanceState(92, 83, false, []),
            [new FactionStandingState(FactionIds.TerranContinuityAuthority, 0), new FactionStandingState(FactionIds.KuiperSyndicates, 0)],
            0,
            null,
            [],
            [new StationVisitState(pack.Ship.StationId, StartTime, null)],
            100);
    }

    public static DestinationMarketState CreateDestinationMarket(VerticalSliceContentPack pack, IReadOnlyList<MarketListingState> earthListings)
    {
        StationId mars = pack.Route.DestinationStationId;
        IReadOnlyList<DestinationMarketListingState> listings = pack.MarsPrices.Select(price =>
        {
            Credits earthAsk = earthListings.Single(item => item.CommodityId == price.CommodityId).AskPrice;
            long bid = checked(earthAsk.Value + price.RealizedMargin);
            if (bid < 0) throw new InvalidOperationException($"Mars bid for {price.CommodityId} cannot be negative.");
            return new DestinationMarketListingState(price.CommodityId, new Credits(bid), price.RealizedMargin);
        }).ToArray();
        return new DestinationMarketState(mars, listings);
    }
}
