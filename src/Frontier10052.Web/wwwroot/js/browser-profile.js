const playerKey = "frontier10052:player:v1";

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

function createFallbackKey() {
    const bytes = new Uint8Array(16);
    window.crypto.getRandomValues(bytes);
    return [...bytes].map(value => value.toString(16).padStart(2, "0")).join("");
}
