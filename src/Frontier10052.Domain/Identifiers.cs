using System.Text.Json.Serialization;

namespace Frontier10052.Domain;

public readonly record struct GameId
{
    [JsonConstructor]
    public GameId(string value) => Value = Identifier.Require(value, nameof(GameId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct StationId
{
    [JsonConstructor]
    public StationId(string value) => Value = Identifier.Require(value, nameof(StationId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct ShipId
{
    [JsonConstructor]
    public ShipId(string value) => Value = Identifier.Require(value, nameof(ShipId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct CrewId
{
    [JsonConstructor]
    public CrewId(string value) => Value = Identifier.Require(value, nameof(CrewId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct CommodityId
{
    [JsonConstructor]
    public CommodityId(string value) => Value = Identifier.Require(value, nameof(CommodityId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct ContractId
{
    [JsonConstructor]
    public ContractId(string value) => Value = Identifier.Require(value, nameof(ContractId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct RouteId
{
    [JsonConstructor]
    public RouteId(string value) => Value = Identifier.Require(value, nameof(RouteId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct EncounterId
{
    [JsonConstructor]
    public EncounterId(string value) => Value = Identifier.Require(value, nameof(EncounterId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct InformationId
{
    [JsonConstructor]
    public InformationId(string value) => Value = Identifier.Require(value, nameof(InformationId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct RouteCheckpointId
{
    [JsonConstructor]
    public RouteCheckpointId(string value) => Value = Identifier.Require(value, nameof(RouteCheckpointId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct StationEventId
{
    [JsonConstructor]
    public StationEventId(string value) => Value = Identifier.Require(value, nameof(StationEventId));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct ContractLeadId
{
    [JsonConstructor]
    public ContractLeadId(string value) => Value = Identifier.Require(value, nameof(ContractLeadId));
    public string Value { get; }
    public override string ToString() => Value;
}

internal static class Identifier
{
    public static string Require(string value, string typeName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{typeName} cannot be empty.", nameof(value));
        }

        string normalized = value.Trim();
        if (normalized.Length > 96)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{typeName} cannot exceed 96 characters.");
        }

        return normalized;
    }
}
