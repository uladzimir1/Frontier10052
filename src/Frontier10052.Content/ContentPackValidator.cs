using Frontier10052.Domain;

namespace Frontier10052.Content;

public sealed record ContentValidationError(string Code, string Message);

public sealed record ContentValidationResult(IReadOnlyList<ContentValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class ContentPackValidator
{
    public static ContentValidationResult Validate(VerticalSliceContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        List<ContentValidationError> errors = [];

        AddDuplicates(pack.Stations.Select(item => item.Id.Value), "station", errors);
        AddDuplicates(pack.Crew.Select(item => item.Id.Value), "crew", errors);
        AddDuplicates(pack.Commodities.Select(item => item.Id.Value), "commodity", errors);
        AddDuplicates(pack.Contracts.Select(item => item.Id.Value), "contract", errors);
        AddDuplicates(pack.Reports.Select(item => item.Id), "report", errors);
        AddDuplicates(pack.Routes.Select(item => item.Id.Value), "route", errors);
        AddDuplicates(pack.Encounters.Select(item => item.Id.Value), "encounter", errors);
        AddDuplicates(pack.MarsPrices.Select(item => item.CommodityId.Value), "mars-price", errors);
        AddDuplicates(pack.StationMarkets.Select(item => item.StationId.Value), "station-market", errors);

        HashSet<StationId> stationIds = pack.Stations.Select(item => item.Id).ToHashSet();
        HashSet<CommodityId> commodityIds = pack.Commodities.Select(item => item.Id).ToHashSet();
        HashSet<CrewId> crewIds = pack.Crew.Select(item => item.Id).ToHashSet();
        HashSet<EncounterId> encounterIds = pack.Encounters.Select(item => item.Id).ToHashSet();

        Require(stationIds.Contains(pack.Ship.StationId), "reference.ship-station", "The ship references a station that is not in the pack.", errors);
        Require(pack.Crew.All(item => item.ShipId == pack.Ship.Id), "reference.crew-ship", "A crew member references a different or missing ship.", errors);
        Require(pack.Ship.CargoCapacity.Value > 0, "ship.capacity", "Ship capacity must be positive.", errors);
        Require(pack.Ship.FuelPercent is >= 0 and <= 100, "ship.fuel", "Ship fuel must be between zero and 100 percent.", errors);
        Require(pack.Ship.DriveWearPercent is >= 0 and <= 100, "ship.wear", "Ship drive wear must be between zero and 100 percent.", errors);

        foreach (ContractDefinition contract in pack.Contracts)
        {
            Require(stationIds.Contains(contract.OriginStationId), "reference.contract-origin", $"Contract {contract.Id} origin is missing.", errors);
            Require(stationIds.Contains(contract.DestinationStationId), "reference.contract-destination", $"Contract {contract.Id} destination is missing.", errors);
            Require(contract.OriginStationId != contract.DestinationStationId, "contract.same-station", $"Contract {contract.Id} origin and destination must differ.", errors);
            Require(commodityIds.Contains(contract.CommodityId), "reference.contract-commodity", $"Contract {contract.Id} commodity is missing.", errors);
            Require(contract.Quantity.Value > 0, "contract.quantity", $"Contract {contract.Id} quantity must be positive.", errors);
            Require(contract.DeadlineHours > 0, "contract.deadline", $"Contract {contract.Id} deadline must be positive.", errors);
            Require(contract.Quantity <= pack.Ship.CargoCapacity, "contract.capacity", $"Contract {contract.Id} cargo cannot fit in the authored ship.", errors);
            Require(contract.LegalExposureOnAccept >= 0, "contract.legal-exposure", $"Contract {contract.Id} has negative legal exposure.", errors);
            Require(pack.Routes.Any(route => route.OriginStationId == contract.OriginStationId && route.DestinationStationId == contract.DestinationStationId), "reference.contract-route", $"Contract {contract.Id} has no matching route.", errors);
        }

        foreach (RouteDefinition route in pack.Routes)
        {
            Require(stationIds.Contains(route.OriginStationId) && stationIds.Contains(route.DestinationStationId), "reference.route-station", $"Route {route.Id} references a missing station.", errors);
            Require(route.OriginStationId != route.DestinationStationId, "route.same-station", $"Route {route.Id} must connect two stations.", errors);
            Require(route.DurationHours > 0 && route.EncounterAtHour > 0 && route.EncounterAtHour < route.DurationHours, "route.timing", $"Route {route.Id} encounter must occur inside its positive duration.", errors);
            Require(route.FuelCostPercent > 0 && route.FuelCostPercent <= 100, "route.fuel", $"Route {route.Id} has an impossible fuel requirement.", errors);
            Require(route.DriveWearPercent >= 0 && route.DriveWearPercent <= 100, "route.wear", $"Route {route.Id} has invalid drive wear.", errors);
            Require(route.EncounterPool.Count > 0, "route.encounter-pool", $"Route {route.Id} has no encounter pool.", errors);
            Require(route.EncounterPool.All(encounterIds.Contains), "reference.route-encounter", $"Route {route.Id} references a missing encounter.", errors);

            ContractDefinition? contract = pack.Contracts.SingleOrDefault(item => item.OriginStationId == route.OriginStationId && item.DestinationStationId == route.DestinationStationId);
            if (contract is not null)
            {
                int longestDelay = route.Id.Value switch
                {
                    "earth-mars-relief-corridor" => 7,
                    "mars-pluto-migration-corridor" => 4,
                    _ => 6,
                };
                Require(route.DurationHours + longestDelay <= contract.DeadlineHours, "route.deadline", $"Route {route.Id} cannot recover inside contract {contract.Id}'s deadline.", errors);
            }
        }

        foreach (EncounterDefinition encounter in pack.Encounters)
        {
            Require(encounter.Options.Count > 0, "encounter.options", $"Encounter {encounter.Id} has no authored options.", errors);
            AddDuplicates(encounter.Options.Select(item => item.ResponseId), "encounter-option", errors);
            foreach (EncounterOptionDefinition option in encounter.Options)
            {
                Require(option.RequiredCrewId is null || crewIds.Contains(option.RequiredCrewId.Value), "reference.encounter-crew", $"Encounter {encounter.Id} option {option.ResponseId} requires missing crew.", errors);
            }
        }

        Require(pack.StationMarkets.All(item => stationIds.Contains(item.StationId)), "reference.station-market", "A station market references a missing station.", errors);
        Require(pack.StationMarkets.All(item => item.TradedCommodities.All(commodityIds.Contains)), "reference.station-market-commodity", "A station market references a missing commodity.", errors);
        Require(stationIds.All(id => pack.StationMarkets.Any(market => market.StationId == id)), "station-market.missing", "Every station needs a market collection, including an intentionally empty one.", errors);
        Require(pack.MarsPrices.All(item => commodityIds.Contains(item.CommodityId)), "reference.mars-price", "A Mars price references a missing commodity.", errors);
        Require(pack.Commodities.Where(item => item.Purchasable).All(item => pack.MarsPrices.Any(price => price.CommodityId == item.Id)), "mars-price.missing", "Every Earth-purchasable commodity needs a Mars bid.", errors);

        foreach (MarsPriceDefinition price in pack.MarsPrices)
        {
            CommodityDefinition? commodity = pack.Commodities.SingleOrDefault(item => item.Id == price.CommodityId);
            if (commodity is not null)
            {
                Require(price.RealizedMargin >= commodity.EstimatedMarsProfitLow && price.RealizedMargin <= commodity.EstimatedMarsProfitHigh, "mars-price.range", $"The realized Mars margin for {price.CommodityId} falls outside its authored estimate.", errors);
            }
        }

        foreach (CommodityDefinition commodity in pack.Commodities)
        {
            Require(commodity.TargetStock.Value > 0, "commodity.target-stock", $"Commodity {commodity.Id} has no target stock.", errors);
            Require(commodity.EstimatedMarsProfitLow <= commodity.EstimatedMarsProfitHigh, "commodity.profit-range", $"Commodity {commodity.Id} has an inverted profit range.", errors);
        }

        foreach (MarketReportDefinition report in pack.Reports)
        {
            Require(report.ObservedHoursAgo >= 0, "report.age", $"Report {report.Id} has a negative age.", errors);
            Require(report.ConfidencePercent is >= 0 and <= 100, "report.confidence", $"Report {report.Id} has invalid confidence.", errors);
        }

        IEnumerable<string> localizationKeys =
            pack.Stations.SelectMany(item => new[] { item.NameKey, item.FacilityKey })
                .Concat(new[] { pack.Ship.NameKey, pack.Ship.HullKey })
                .Concat(pack.Contracts.SelectMany(item => new[] { item.TitleKey, item.IssuerKey, item.ConsequenceKey }))
                .Concat(pack.Crew.SelectMany(item => new[] { item.NameKey, item.RoleKey, item.BriefingKey }))
                .Concat(pack.Commodities.SelectMany(item => new[] { item.NameKey, item.DescriptionKey, item.LegalityKey }))
                .Concat(pack.Reports.SelectMany(item => new[] { item.HeadlineKey, item.DetailKey, item.SourceKey, item.LegalityKey }))
                .Concat(pack.Routes.SelectMany(item => new[] { item.NameKey, item.ProfileKey }))
                .Concat(pack.Encounters.SelectMany(item => new[] { item.TitleKey, item.DetailKey, item.SourceKey }));

        foreach (string key in localizationKeys.Distinct(StringComparer.Ordinal))
        {
            if (!pack.Localization.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new ContentValidationError("localization.missing", $"Localization text is missing for '{key}'."));
            }
        }

        return new ContentValidationResult(errors);
    }

    private static void AddDuplicates(IEnumerable<string> ids, string kind, ICollection<ContentValidationError> errors)
    {
        foreach (string id in ids.GroupBy(item => item, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add(new ContentValidationError($"duplicate.{kind}", $"Duplicate {kind} ID '{id}'."));
        }
    }

    private static void Require(bool condition, string code, string message, ICollection<ContentValidationError> errors)
    {
        if (!condition) errors.Add(new ContentValidationError(code, message));
    }
}
