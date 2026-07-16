namespace Frontier10052.Web.Components;

public sealed record CinematicPreferences(
    string Quality,
    bool ReducedCameraMotion,
    bool HighContrast,
    bool Captions,
    bool SpeakerLabels)
{
    public static CinematicPreferences Default { get; } = new("Automatic", false, false, true, true);
}
