export const QUALITY_PROFILES = Object.freeze({
    High: Object.freeze({ dpr: 2, stars: 1800, dust: 420, asteroids: 42, traffic: 18 }),
    Balanced: Object.freeze({ dpr: 1.5, stars: 1100, dust: 240, asteroids: 24, traffic: 10 }),
    "Low bandwidth": Object.freeze({ dpr: 1, stars: 0, dust: 0, asteroids: 0, traffic: 0, fallback: true }),
});

export function clamp(value, minimum = 0, maximum = 1) {
    return Math.min(maximum, Math.max(minimum, value));
}

export function qualityProfile(requested, hardware = {}) {
    let quality = requested || "Automatic";
    if (quality === "Automatic") {
        const capableDesktop = (hardware.width ?? 0) >= 1180
            && (hardware.memory ?? 0) >= 8
            && (hardware.cores ?? 0) >= 8
            && !hardware.touch;
        quality = capableDesktop ? "High" : "Balanced";
    }
    return { name: quality, ...(QUALITY_PROFILES[quality] ?? QUALITY_PROFILES.Balanced) };
}

export function seenCueKey(cue, gameId) {
    return `frontier10052:cinematic:seen:${gameId}:${cue.commandSequence}:${cue.cueId}`;
}

export function normalizedProgress(trackTop, trackHeight, viewportHeight) {
    const travel = Math.max(1, trackHeight - viewportHeight);
    return clamp(-trackTop / travel);
}

export function stagePose(stage, progress, reducedMotion = false) {
    const p = reducedMotion ? (progress < 0.5 ? 0 : 1) : clamp(progress);
    const smooth = p * p * (3 - 2 * p);
    const pose = {
        camera: [8 - smooth * 6, 2.4 + Math.sin(smooth * Math.PI) * 0.8, 10 - smooth * 4],
        target: [0, 0, 0],
        ship: [-2.8 + smooth * 4.8, -0.2 + smooth * 0.35, 1.5 - smooth * 1.8],
        station: [3.8 - smooth * 1.8, -0.6, -2.5 + smooth * 0.8],
        drive: 0.25 + smooth * 0.75,
        lattice: 0,
    };

    switch (stage) {
        case "departure-preview":
            pose.camera = [7.5 - smooth * 1.1, 2.4, 10.5 - smooth * 0.8];
            pose.ship = [-1.8, -0.2, 1.1];
            pose.station = [2.6, -0.4, -2.6];
            pose.drive = 0.15;
            break;
        case "clamp-release-undock":
            pose.ship = [-1.6 + smooth * 4.1, -0.2 + smooth * 0.8, 0.8 + smooth * 0.4];
            pose.station = [2.7 - smooth * 1.2, -0.5, -2.4 - smooth * 1.1];
            break;
        case "gravity-boundary-burn":
            pose.camera = [9 - smooth * 4.5, 1.2, 11 - smooth * 7.5];
            pose.ship = [-2 + smooth * 6.5, 0, 0.2];
            pose.station = [5 - smooth * 2, -1.4, -6 - smooth * 3];
            pose.drive = 0.6 + smooth * 1.8;
            break;
        case "encounter-arrival":
            pose.camera = [6.2, 3.1 - smooth * 1.2, 9.5 - smooth * 1.6];
            pose.ship = [-1.7 + smooth * 1.2, 0.1, 0.7];
            break;
        case "response-outcome":
        case "transfer-montage":
            pose.camera = [6.8 - smooth * 5.2, 1.4 + smooth * 0.8, 8.4 - smooth * 1.5];
            pose.ship = [-1.5 + smooth * 3.2, 0.15 * Math.sin(smooth * Math.PI * 2), 0.4];
            pose.drive = 0.8 + smooth;
            break;
        case "delayed-labor-warning":
            pose.camera = [5.8 - smooth * 1.2, 2.2, 8.5 - smooth];
            pose.ship = [-0.9, 0, 0.4];
            pose.drive = 0.45;
            break;
        case "pinch-lattice-drift":
            pose.camera = [Math.cos(smooth * Math.PI * 0.8) * 7, 2.5 + Math.sin(smooth * Math.PI) * 1.1, Math.sin(smooth * Math.PI * 0.8) * 7 + 4];
            pose.ship = [0, Math.sin(smooth * Math.PI * 4) * 0.18, 0];
            pose.lattice = 0.35 + Math.sin(smooth * Math.PI) * 0.65;
            break;
        case "destination-approach":
            pose.camera = [8 - smooth * 3.2, 2.8 - smooth, 11 - smooth * 3.5];
            pose.ship = [-2.8 + smooth * 3.8, 0.2, 1.2 - smooth * 1.4];
            pose.station = [4.2 - smooth * 2.2, -0.5, -3 + smooth * 1.6];
            break;
        case "docking-handoff":
            pose.camera = [5.5 - smooth * 1.8, 1.3, 8 - smooth * 2.5];
            pose.ship = [-2.8 + smooth * 3.1, -0.1, 0.5 - smooth * 0.7];
            pose.station = [2.5 - smooth * 0.8, -0.35, -2.1 + smooth * 0.7];
            pose.drive = 0.65 * (1 - smooth);
            break;
    }
    return pose;
}
