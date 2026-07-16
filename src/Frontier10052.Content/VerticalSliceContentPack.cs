using Frontier10052.Domain;

namespace Frontier10052.Content;

public sealed record StationDefinition(StationId Id, string NameKey, string SystemName, string FacilityKey);

public sealed record ShipDefinition(
    ShipId Id,
    string NameKey,
    string HullKey,
    StationId StationId,
    Tonnes CargoCapacity,
    int FuelPercent,
    int DriveWearPercent);

public sealed record CrewDefinition(
    CrewId Id,
    string NameKey,
    string RoleKey,
    string BriefingKey,
    ShipId ShipId,
    int Loyalty,
    int Fatigue,
    bool Available);

public sealed record CommodityDefinition(
    CommodityId Id,
    string NameKey,
    string DescriptionKey,
    string LegalityKey,
    Credits BasePrice,
    Tonnes InitialStock,
    Tonnes TargetStock,
    long EstimatedMarsProfitLow,
    long EstimatedMarsProfitHigh,
    bool Purchasable);

public sealed record ContractDefinition(
    ContractId Id,
    string TitleKey,
    string IssuerKey,
    StationId OriginStationId,
    StationId DestinationStationId,
    CommodityId CommodityId,
    Tonnes Quantity,
    long DeadlineHours,
    Credits Reward,
    Credits FailurePenalty,
    string ConsequenceKey,
    string IssuerFactionId = "",
    bool IsTurnaroundOffer = false,
    int LegalExposureOnAccept = 0);

public sealed record MarketReportDefinition(
    string Id,
    string HeadlineKey,
    string DetailKey,
    string SourceKey,
    int ObservedHoursAgo,
    int ConfidencePercent,
    string LegalityKey,
    bool Verified);

public sealed record RouteDefinition(
    RouteId Id,
    string NameKey,
    StationId OriginStationId,
    StationId DestinationStationId,
    int DurationHours,
    int FuelCostPercent,
    int DriveWearPercent,
    int EncounterAtHour,
    IReadOnlyList<EncounterId> EncounterPool,
    string ProfileKey);

public sealed record EncounterOptionDefinition(string ResponseId, CrewId? RequiredCrewId);

public sealed record EncounterDefinition(
    EncounterId Id,
    string TitleKey,
    string DetailKey,
    string SourceKey,
    IReadOnlyList<EncounterOptionDefinition> Options);

public sealed record MarsPriceDefinition(CommodityId CommodityId, long RealizedMargin);
public sealed record StationMarketDefinition(StationId StationId, IReadOnlyList<CommodityId> TradedCommodities);

public sealed record VerticalSliceContentPack(
    string Version,
    IReadOnlyList<StationDefinition> Stations,
    ShipDefinition Ship,
    IReadOnlyList<CrewDefinition> Crew,
    IReadOnlyList<CommodityDefinition> Commodities,
    IReadOnlyList<ContractDefinition> Contracts,
    IReadOnlyList<MarketReportDefinition> Reports,
    IReadOnlyList<RouteDefinition> Routes,
    IReadOnlyList<EncounterDefinition> Encounters,
    IReadOnlyList<MarsPriceDefinition> MarsPrices,
    IReadOnlyList<StationMarketDefinition> StationMarkets,
    IReadOnlyDictionary<string, string> Localization)
{
    public const string PackVersion = "vertical-slice-v2";
    public const string Schema2PackVersion = "vertical-slice-v1";
    public const string LegacyPackVersion = "vertical-slice-v0";

    public ContractDefinition Contract => Contracts[0];
    public RouteDefinition Route => Routes[0];

    public string Text(string key) => Localization.TryGetValue(key, out string? text) ? text : $"[{key}]";

    public static VerticalSliceContentPack Create()
    {
        StationId earth = new("earth-heritage-station");
        StationId mars = new("mars-industrial-port");
        StationId ceres = new("ceres-freehold-anchorage");
        StationId pluto = new("pluto-gateway");
        ShipId wayfarer = new("wayfarer");

        CommodityDefinition[] commodities =
        [
            new(new CommodityId("medical-membranes"), "commodity.medical.name", "commodity.medical.description", "legality.sealed-medical", new Credits(1_800), new Tonnes(18), new Tonnes(18), 0, 0, false),
            new(new CommodityId("ceramic-drive-bearings"), "commodity.bearings.name", "commodity.bearings.description", "legality.standard", new Credits(1_100), new Tonnes(14), new Tonnes(24), 100, 280, true),
            new(new CommodityId("coolant-couplings"), "commodity.coolant.name", "commodity.coolant.description", "legality.standard", new Credits(620), new Tonnes(40), new Tonnes(36), 140, 310, true),
            new(new CommodityId("nutrient-substrate"), "commodity.nutrient.name", "commodity.nutrient.description", "legality.bio-certified", new Credits(240), new Tonnes(60), new Tonnes(50), 60, 160, true),
            new(new CommodityId("actuator-assemblies"), "commodity.actuator.name", "commodity.actuator.description", "legality.standard", new Credits(1_280), new Tonnes(26), new Tonnes(34), -180, 940, true),
            new(new CommodityId("heritage-data-substrates"), "commodity.heritage.name", "commodity.heritage.description", "legality.archive-license", new Credits(2_100), new Tonnes(8), new Tonnes(15), 220, 700, true),
        ];

        ContractDefinition[] contracts =
        [
            new(
                new ContractId("mars-clinic-membranes"), "contract.first.title", "contract.first.issuer", earth, mars,
                new CommodityId("medical-membranes"), new Tonnes(18), 52, new Credits(12_000), new Credits(4_500),
                "contract.first.consequence"),
            new(
                new ContractId("tca-pluto-migration-support"), "contract.pluto.title", "contract.pluto.issuer", mars, pluto,
                new CommodityId("nutrient-substrate"), new Tonnes(22), 112, new Credits(19_000), new Credits(7_500),
                "contract.pluto.consequence", "tca", true),
            new(
                new ContractId("kuiper-ceres-repair-freight"), "contract.ceres.title", "contract.ceres.issuer", mars, ceres,
                new CommodityId("ceramic-drive-bearings"), new Tonnes(16), 52, new Credits(14_500), new Credits(5_000),
                "contract.ceres.consequence", "kuiper-syndicates", true, 1),
        ];

        RouteDefinition[] routes =
        [
            new(new RouteId("earth-mars-relief-corridor"), "route.earth-mars.name", earth, mars, 38, 12, 3, 18,
                [new EncounterId("sol-transit-inspection"), new EncounterId("drive-coolant-failure"), new EncounterId("ceres-lane-pirate-demand")], "route.earth-mars.profile"),
            new(new RouteId("mars-ceres-repair-lane"), "route.mars-ceres.name", mars, ceres, 32, 10, 3, 16,
                [new EncounterId("ceres-debris-coolant-breach")], "route.mars-ceres.profile"),
            new(new RouteId("mars-pluto-migration-corridor"), "route.mars-pluto.name", mars, pluto, 86, 28, 7, 43,
                [new EncounterId("pluto-migration-medical-emergency")], "route.mars-pluto.profile"),
        ];

        CrewId ilya = new("ilya-sato");
        CrewId mara = new("mara-venn");
        CrewId noor = new("noor-okafor");
        CrewId tomas = new("tomas-vale");
        EncounterDefinition[] encounters =
        [
            new(new EncounterId("sol-transit-inspection"), "encounter.inspection.title", "encounter.inspection.detail", "encounter.inspection.source",
                [new("InspectionStandardCompliance", null), new("InspectionMedicalPriority", noor)]),
            new(new EncounterId("drive-coolant-failure"), "encounter.mechanical.title", "encounter.mechanical.detail", "encounter.mechanical.source",
                [new("MechanicalFieldRepair", ilya), new("MechanicalReducedBurn", mara)]),
            new(new EncounterId("ceres-lane-pirate-demand"), "encounter.pirate.title", "encounter.pirate.detail", "encounter.pirate.source",
                [new("PiratePayDemand", null), new("PirateDumpSpeculativeCargo", null), new("PirateHardBurn", mara)]),
            new(new EncounterId("pluto-migration-medical-emergency"), "encounter.pluto-medical.title", "encounter.pluto-medical.detail", "encounter.pluto-medical.source",
                [new("MigrationMedicalAssist", tomas), new("MigrationConserveSupplies", null)]),
            new(new EncounterId("ceres-debris-coolant-breach"), "encounter.ceres-debris.title", "encounter.ceres-debris.detail", "encounter.ceres-debris.source",
                [new("DebrisIlyaRepair", ilya), new("DebrisMaraEvasion", mara)]),
        ];

        Dictionary<string, string> text = new(StringComparer.Ordinal)
        {
            ["station.earth.name"] = "Earth Heritage Station",
            ["station.earth.facility"] = "Sol Heritage Zone · Orbital exchange",
            ["station.mars.name"] = "Mars Industrial Port",
            ["station.mars.facility"] = "Valles freight district · Berth network",
            ["station.ceres.name"] = "Ceres Freehold Anchorage",
            ["station.ceres.facility"] = "Freehold repair ring · Independent customs",
            ["station.pluto.name"] = "Pluto Gateway",
            ["station.pluto.facility"] = "Outer migration terminus · Relief custody",
            ["ship.wayfarer.name"] = "Wayfarer",
            ["ship.wayfarer.hull"] = "Tern-class modular freighter",
            ["crew.mara.name"] = "Mara Venn",
            ["crew.mara.role"] = "Pilot",
            ["crew.mara.briefing"] = "Mara shapes the fuel profile and keeps an evasive reserve available when geometry turns hostile.",
            ["crew.ilya.name"] = "Ilya Sato",
            ["crew.ilya.role"] = "Engineer",
            ["crew.ilya.briefing"] = "Ilya knows every deferred repair and can trade certified certainty for a faster field service.",
            ["crew.noor.name"] = "Noor Okafor",
            ["crew.noor.role"] = "Security",
            ["crew.noor.briefing"] = "Noor protects manifest confidence, custody, and the legal distance between gray freight and contraband.",
            ["crew.tomas.name"] = "Tomas Vale",
            ["crew.tomas.role"] = "Medic",
            ["crew.tomas.briefing"] = "Tomas carries the medical authority—and fatigue cost—of answering humanitarian calls in transit.",
            ["commodity.medical.name"] = "Sealed medical membranes",
            ["commodity.medical.description"] = "Sterile replacement membranes under issuer seal; contract cargo only.",
            ["commodity.bearings.name"] = "Ceramic drive bearings",
            ["commodity.bearings.description"] = "High-temperature bearings for tug and courier drive rebuilds.",
            ["commodity.coolant.name"] = "Coolant couplings",
            ["commodity.coolant.description"] = "Standardized thermal-loop couplings with steady port demand.",
            ["commodity.nutrient.name"] = "Regulated nutrient substrate",
            ["commodity.nutrient.description"] = "Certified growth medium for closed-cycle food systems and migration support.",
            ["commodity.actuator.name"] = "Actuator assemblies",
            ["commodity.actuator.description"] = "Industrial motion packages exposed to the disputed Mars shortage.",
            ["commodity.heritage.name"] = "Heritage data substrates",
            ["commodity.heritage.description"] = "Archive-grade media requiring a registered cultural export license.",
            ["legality.standard"] = "Legal · standard manifest",
            ["legality.sealed-medical"] = "Legal · sealed medical chain of custody",
            ["legality.bio-certified"] = "Legal · regulated bio-certification",
            ["legality.archive-license"] = "Controlled · heritage export license required",
            ["contract.first.title"] = "Mars Clinic Membrane Transfer",
            ["contract.first.issuer"] = "Sol Mutual Relief Office",
            ["contract.first.consequence"] = "Late or broken seals trigger a 4,500-credit claim and restrict future medical work.",
            ["contract.pluto.title"] = "Pluto Migration Substrate Relief",
            ["contract.pluto.issuer"] = "Terran Continuity Authority",
            ["contract.pluto.consequence"] = "A lawful relief manifest; failure triggers a 7,500-credit claim and damages TCA migration trust.",
            ["contract.ceres.title"] = "Ceres Drive-Rebuild Freight",
            ["contract.ceres.issuer"] = "Kuiper Belt intermediary",
            ["contract.ceres.consequence"] = "Essential repair freight routed through a gray intermediary; Noor can limit, but not erase, legal exposure.",
            ["report.shortage.headline"] = "Mars actuator shortage verified",
            ["report.shortage.detail"] = "Maintenance consortium purchase orders show an unresolved port-wide actuator deficit.",
            ["report.shortage.source"] = "Mars Industrial Maintenance Consortium",
            ["report.convoy.headline"] = "Inbound convoy may erase the shortage",
            ["report.convoy.detail"] = "A courier manifest lists actuator pallets aboard a convoy due before Wayfarer arrives.",
            ["report.convoy.source"] = "Independent courier manifest",
            ["report.legality"] = "Legal commercial intelligence",
            ["route.earth-mars.name"] = "Earth–Mars relief corridor",
            ["route.earth-mars.profile"] = "Mara's filed relief vector preserves the twelve-point fuel envelope.",
            ["route.mars-ceres.name"] = "Mars–Ceres repair lane",
            ["route.mars-ceres.profile"] = "Mara's close-system profile: 32 hours, 10% fuel, 3% drive wear.",
            ["route.mars-pluto.name"] = "Mars–Pluto migration corridor",
            ["route.mars-pluto.profile"] = "Mara's outer-system profile: 86 hours, 28% fuel, 7% drive wear.",
            ["encounter.inspection.title"] = "Continuity patrol inspection",
            ["encounter.inspection.detail"] = "A Terran Continuity cutter requests Wayfarer's manifest and custody seals.",
            ["encounter.inspection.source"] = "TCA patrol Sable Nine",
            ["encounter.mechanical.title"] = "Drive coolant pressure loss",
            ["encounter.mechanical.detail"] = "The worn metric-drive cooling loop falls below its safe pressure envelope.",
            ["encounter.mechanical.source"] = "Wayfarer engineering telemetry",
            ["encounter.pirate.title"] = "Ceres-lane pirate demand",
            ["encounter.pirate.detail"] = "An unregistered pursuit craft demands payment or cargo before the patrol boundary.",
            ["encounter.pirate.source"] = "Unknown pursuit transponder",
            ["encounter.pluto-medical.title"] = "Migration medical emergency",
            ["encounter.pluto-medical.detail"] = "A migration tender requests Tomas and four hours of medical support before its life-support window closes.",
            ["encounter.pluto-medical.source"] = "TCA migration tender Pilgrim Seven",
            ["encounter.ceres-debris.title"] = "Debris strike and coolant breach",
            ["encounter.ceres-debris.detail"] = "A debris fan ruptures an auxiliary coolant run; Ilya can repair it or Mara can evade the densest field.",
            ["encounter.ceres-debris.source"] = "Wayfarer collision and coolant telemetry",
        };

        return new VerticalSliceContentPack(
            PackVersion,
            [
                new StationDefinition(earth, "station.earth.name", "Sol", "station.earth.facility"),
                new StationDefinition(mars, "station.mars.name", "Sol", "station.mars.facility"),
                new StationDefinition(ceres, "station.ceres.name", "Sol", "station.ceres.facility"),
                new StationDefinition(pluto, "station.pluto.name", "Sol", "station.pluto.facility"),
            ],
            new ShipDefinition(wayfarer, "ship.wayfarer.name", "ship.wayfarer.hull", earth, new Tonnes(72), 84, 17),
            [
                new CrewDefinition(mara, "crew.mara.name", "crew.mara.role", "crew.mara.briefing", wayfarer, 63, 8, true),
                new CrewDefinition(ilya, "crew.ilya.name", "crew.ilya.role", "crew.ilya.briefing", wayfarer, 58, 14, true),
                new CrewDefinition(noor, "crew.noor.name", "crew.noor.role", "crew.noor.briefing", wayfarer, 61, 11, true),
                new CrewDefinition(tomas, "crew.tomas.name", "crew.tomas.role", "crew.tomas.briefing", wayfarer, 66, 9, true),
            ],
            commodities,
            contracts,
            [
                new MarketReportDefinition("mars-actuator-shortage", "report.shortage.headline", "report.shortage.detail", "report.shortage.source", 29, 82, "report.legality", true),
                new MarketReportDefinition("mars-convoy-arrival", "report.convoy.headline", "report.convoy.detail", "report.convoy.source", 6, 38, "report.legality", false),
            ],
            routes,
            encounters,
            [
                new MarsPriceDefinition(new CommodityId("ceramic-drive-bearings"), 190),
                new MarsPriceDefinition(new CommodityId("coolant-couplings"), 240),
                new MarsPriceDefinition(new CommodityId("nutrient-substrate"), 100),
                new MarsPriceDefinition(new CommodityId("actuator-assemblies"), -120),
                new MarsPriceDefinition(new CommodityId("heritage-data-substrates"), 420),
            ],
            [
                new StationMarketDefinition(earth, commodities.Where(item => item.Purchasable).Select(item => item.Id).ToArray()),
                new StationMarketDefinition(mars, commodities.Where(item => item.Purchasable).Select(item => item.Id).ToArray()),
                new StationMarketDefinition(ceres, []),
                new StationMarketDefinition(pluto, []),
            ],
            text);
    }
}
