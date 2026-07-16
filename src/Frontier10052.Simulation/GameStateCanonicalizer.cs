using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontier10052.Simulation;

public static class GameStateCanonicalizer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, Options);
    }

    public static JsonSerializerOptions CreateSerializerOptions(bool writeIndented = false)
    {
        JsonSerializerOptions options = new(Options) { WriteIndented = writeIndented };
        return options;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
