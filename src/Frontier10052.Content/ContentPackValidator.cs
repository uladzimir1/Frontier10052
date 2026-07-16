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
        AddDuplicates(pack.Information.Select(item => item.Id.Value), "information", errors);
        AddDuplicates(pack.Contracts.Select(item => item.Id.Value), "contract", errors);
        AddDuplicates(pack.Reports.Select(item => item.Id), "report", errors);
        AddDuplicates(pack.Routes.Select(item => item.Id.Value), "route", errors);
        AddDuplicates(pack.Encounters.Select(item => item.Id.Value), "encounter", errors);
        AddDuplicates(pack.MarsPrices.Select(item => item.CommodityId.Value), "mars-price", errors);
        AddDuplicates(pack.StationMarkets.Select(item => item.StationId.Value), "station-market", errors);
        AddDuplicates(pack.StationServices.Select(item => item.StationId.Value), "station-service", errors);
        AddDuplicates(pack.Routes.SelectMany(item => item.Checkpoints ?? []).Select(item => item.Id.Value), "route-checkpoint", errors);

        HashSet<StationId> stationIds = pack.Stations.Select(item => item.Id).ToHashSet();
        HashSet<CommodityId> commodityIds = pack.Commodities.Select(item => item.Id).ToHashSet();
        HashSet<InformationId> informationIds = pack.Information.Select(item => item.Id).ToHashSet();
        HashSet<CrewId> crewIds = pack.Crew.Select(item => item.Id).ToHashSet();
        HashSet<EncounterId> encounterIds = pack.Encounters.Select(item => item.Id).ToHashSet();

        Require(stationIds.Contains(pack.Ship.StationId), "reference.ship-station", "The ship references a station that is not in the pack.", errors);
        Require(pack.Crew.All(item => item.ShipId == pack.Ship.Id), "reference.crew-ship", "A crew member references a different or missing ship.", errors);
        Require(pack.Ship.CargoCapacity.Value > 0, "ship.capacity", "Ship capacity must be positive.", errors);
        Require(pack.Ship.FuelPercent is >= 0 and <= 100, "ship.fuel", "Ship fuel must be between zero and 100 percent.", errors);
        Require(pack.Ship.DriveWearPercent is >= 0 and <= 100, "ship.wear", "Ship drive wear must be between zero and 100 percent.", errors);

        foreach (ContractDefinition contract in pack.Contracts)
        {
            Require(contract.AllOriginStationIds.All(stationIds.Contains), "reference.contract-origin", $"Contract {contract.Id} origin is missing.", errors);
            Require(stationIds.Contains(contract.DestinationStationId), "reference.contract-destination", $"Contract {contract.Id} destination is missing.", errors);
            Require(contract.AllOriginStationIds.All(origin => origin != contract.DestinationStationId), "contract.same-station", $"Contract {contract.Id} origin and destination must differ.", errors);
            Require(contract.DeadlineHours > 0, "contract.deadline", $"Contract {contract.Id} deadline must be positive.", errors);
            Require(contract.LegalExposureOnAccept >= 0, "contract.legal-exposure", $"Contract {contract.Id} has negative legal exposure.", errors);
            Require(contract.AcceptanceWindowHours >= 0 && contract.AcceptanceWindowHours <= contract.DeadlineHours, "contract.acceptance-window", $"Contract {contract.Id} has an invalid acceptance window.", errors);
            Require(contract.AllOriginStationIds.All(origin => pack.Routes.Any(route => route.OriginStationId == origin && route.DestinationStationId == contract.DestinationStationId)), "reference.contract-route", $"Contract {contract.Id} has no matching route for every origin.", errors);

            if (contract.ObjectiveKind == ContractObjectiveDefinitionKind.Cargo)
            {
                Require(commodityIds.Contains(contract.CommodityId), "reference.contract-commodity", $"Contract {contract.Id} commodity is missing.", errors);
                Require(contract.Quantity.Value > 0, "contract.quantity", $"Contract {contract.Id} quantity must be positive.", errors);
                Require(contract.Quantity <= pack.Ship.CargoCapacity, "contract.capacity", $"Contract {contract.Id} cargo cannot fit in the authored ship.", errors);
                Require(contract.InformationId is null, "contract.objective", $"Cargo contract {contract.Id} cannot reference information.", errors);
            }
            else
            {
                Require(contract.InformationId is not null && informationIds.Contains(contract.InformationId.Value), "reference.contract-information", $"Information contract {contract.Id} references missing information.", errors);
                Require(contract.Quantity.Value == 0, "contract.information-mass", $"Information contract {contract.Id} must not reserve cargo mass.", errors);
            }
        }

        foreach (RouteDefinition route in pack.Routes)
        {
            Require(stationIds.Contains(route.OriginStationId) && stationIds.Contains(route.DestinationStationId), "reference.route-station", $"Route {route.Id} references a missing station.", errors);
            Require(route.OriginStationId != route.DestinationStationId, "route.same-station", $"Route {route.Id} must connect two stations.", errors);
            Require(route.DurationHours > 0, "route.timing", $"Route {route.Id} must have a positive duration.", errors);
            Require(route.FuelCostPercent > 0 && route.FuelCostPercent <= 100, "route.fuel", $"Route {route.Id} has an impossible fuel requirement.", errors);
            Require(route.DriveWearPercent >= 0 && route.DriveWearPercent <= 100, "route.wear", $"Route {route.Id} has invalid drive wear.", errors);
            Require(route.PinchCost is >= 0 and <= 100, "route.pinch", $"Route {route.Id} has an impossible pinch requirement.", errors);

            IReadOnlyList<RouteCheckpointDefinition> checkpoints = route.Checkpoints ?? [];
            if (checkpoints.Count == 0)
            {
                Require(route.EncounterAtHour > 0 && route.EncounterAtHour < route.DurationHours, "route.timing", $"Route {route.Id} encounter must occur inside its positive duration.", errors);
                Require(route.EncounterPool.Count > 0, "route.encounter-pool", $"Legacy route {route.Id} has no encounter pool.", errors);
                Require(route.EncounterPool.All(encounterIds.Contains), "reference.route-encounter", $"Route {route.Id} references a missing encounter.", errors);
                Require(route.PinchCost == 0, "route.legacy-pinch", $"Legacy route {route.Id} cannot consume pinch.", errors);
            }
            else
            {
                Require(route.EncounterPool.Count == 0, "route.checkpoint-encounters", $"Checkpoint route {route.Id} cannot also use legacy encounters.", errors);
                Require(checkpoints.Count == 5, "route.checkpoint-count", $"Route {route.Id} must define all five ordered Sirius checkpoints.", errors);
                Require(checkpoints.Select(item => item.ScheduledHour).SequenceEqual(checkpoints.Select(item => item.ScheduledHour).Order()), "route.checkpoint-order", $"Route {route.Id} checkpoints are not ordered.", errors);
                Require(checkpoints.Zip(checkpoints.Skip(1)).All(pair => pair.First.ScheduledHour < pair.Second.ScheduledHour), "route.checkpoint-order", $"Route {route.Id} checkpoint hours must be unique.", errors);
                Require(checkpoints[0].Kind == RouteCheckpointDefinitionKind.Undock && checkpoints[^1].Kind == RouteCheckpointDefinitionKind.Approach && checkpoints[^1].ScheduledHour == route.DurationHours, "route.checkpoint-bounds", $"Route {route.Id} must begin with undock and end on its approach duration.", errors);
                Require(route.PinchCost > 0, "route.checkpoint-pinch", $"Checkpoint route {route.Id} requires a positive pinch commitment.", errors);
            }

            ContractDefinition? contract = pack.Contracts.SingleOrDefault(item => item.AllOriginStationIds.Contains(route.OriginStationId) && item.DestinationStationId == route.DestinationStationId);
            if (contract is not null)
            {
                int longestDelay = route.Id.Value switch
                {
                    "earth-mars-relief-corridor" => 7,
                    "mars-pluto-migration-corridor" => 4,
                    "mars-ceres-repair-lane" => 6,
                    _ => 26,
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
        Require(pack.StationServices.All(item => stationIds.Contains(item.StationId)), "reference.station-service", "A station service collection references a missing station.", errors);
        foreach (StationServiceDefinition service in pack.StationServices)
        {
            Require(service.FuelUnitCost > 0 && service.PinchUnitCost >= 0 && service.CertifiedRepairCost > 0 && service.FieldRepairCost > 0, "station-service.values", $"Station services for {service.StationId} contain invalid prices.", errors);
        }
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

        foreach (InformationDefinition item in pack.Information)
        {
            Require(item.ConfidencePercent is >= 0 and <= 100, "information.confidence", $"Information {item.Id} has invalid confidence.", errors);
        }

        IEnumerable<string> localizationKeys =
            pack.Stations.SelectMany(item => new[] { item.NameKey, item.FacilityKey })
                .Concat(new[] { pack.Ship.NameKey, pack.Ship.HullKey })
                .Concat(pack.Contracts.SelectMany(item => new[] { item.TitleKey, item.IssuerKey, item.ConsequenceKey }))
                .Concat(pack.Crew.SelectMany(item => new[] { item.NameKey, item.RoleKey, item.BriefingKey }))
                .Concat(pack.Commodities.SelectMany(item => new[] { item.NameKey, item.DescriptionKey, item.LegalityKey }))
                .Concat(pack.Information.SelectMany(item => new[] { item.TitleKey, item.SourceKey, item.ProvenanceKey }))
                .Concat(pack.Reports.SelectMany(item => new[] { item.HeadlineKey, item.DetailKey, item.SourceKey, item.LegalityKey }))
                .Concat(pack.Routes.SelectMany(item => new[] { item.NameKey, item.ProfileKey }))
                .Concat(pack.Routes.SelectMany(item => item.Checkpoints ?? []).SelectMany(item => new[] { item.TitleKey, item.DetailKey }))
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
