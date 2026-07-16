const playerKey = "frontier10052:player:v1";
const cinematicPreferencesKey = "frontier10052:cinematic:preferences:v1";

export function getOrCreatePlayerKey() {
    let value = window.localStorage.getItem(playerKey);
    if (value) return value;

    value = window.crypto?.randomUUID?.() ?? createFallbackKey();
    window.localStorage.setItem(playerKey, value);
    return value;
}

export function clearPlayerKey() {
    window.localStorage.removeItem(playerKey);
}

export function getCinematicPreferences() {
    let stored = {};
    try {
        stored = JSON.parse(window.localStorage.getItem(cinematicPreferencesKey) ?? "{}");
    } catch {
        stored = {};
    }

    return normalizePreferences(stored);
}

export function saveCinematicPreferences(preferences) {
    const normalized = normalizePreferences(preferences ?? {});
    window.localStorage.setItem(cinematicPreferencesKey, JSON.stringify(normalized));
    return normalized;
}

export function focusById(id) {
    document.getElementById(id)?.focus({ preventScroll: true });
}

function createFallbackKey() {
    const bytes = new Uint8Array(16);
    window.crypto.getRandomValues(bytes);
    return [...bytes].map(value => value.toString(16).padStart(2, "0")).join("");
}

function normalizePreferences(preferences) {
    const qualities = ["Automatic", "High", "Balanced", "Low bandwidth"];
    return {
        quality: qualities.includes(preferences.quality) ? preferences.quality : "Automatic",
        reducedCameraMotion: typeof preferences.reducedCameraMotion === "boolean"
            ? preferences.reducedCameraMotion
            : window.matchMedia("(prefers-reduced-motion: reduce)").matches,
        highContrast: preferences.highContrast === true,
        captions: preferences.captions !== false,
        speakerLabels: preferences.speakerLabels !== false,
    };
}
