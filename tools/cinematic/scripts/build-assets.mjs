import { Accessor, Document, NodeIO } from "@gltf-transform/core";
import { dedup, prune } from "@gltf-transform/functions";
import { copyFile, mkdir, rm, writeFile } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const toolRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = resolve(toolRoot, "../..");
const assetRoot = join(repoRoot, "src/Frontier10052.Web/wwwroot/assets/cinematic");
const modelRoot = join(assetRoot, "models");
const plateRoot = join(assetRoot, "plates");
const buildRoot = join(toolRoot, ".build");

await Promise.all([mkdir(modelRoot, { recursive: true }), mkdir(plateRoot, { recursive: true }), mkdir(buildRoot, { recursive: true })]);

const io = new NodeIO();
await buildWayfarer(join(modelRoot, "wayfarer-tern.glb"));
for (const environment of ["earth", "mars", "ceres", "pluto", "sirius"]) {
    await buildStation(environment, join(modelRoot, `station-${environment}.glb`));
    await buildPlate(environment, join(plateRoot, `${environment}.webp`));
}

await copyFile(join(toolRoot, "assets/scene-catalog.json"), join(assetRoot, "scene-catalog.json"));
await copyFile(join(toolRoot, "assets/ASSET_LICENSES.md"), join(assetRoot, "ASSET_LICENSES.md"));
await rm(buildRoot, { recursive: true, force: true });

async function buildWayfarer(path) {
    const kit = createKit("Wayfarer Tern-class modular freighter");
    const { addBox, addCylinder, material } = kit;
    const graphite = material("worn-graphite", [0.09, 0.105, 0.11, 1], 0.78, 0.42);
    const ivory = material("aged-ivory", [0.58, 0.56, 0.49, 1], 0.72, 0.28);
    const retrofit = material("retrofit-panels", [0.23, 0.28, 0.29, 1], 0.84, 0.2);
    const radiator = material("radiator-black", [0.025, 0.035, 0.04, 1], 0.58, 0.65);
    const amber = material("amber-service-light", [0.24, 0.095, 0.025, 1], 0.32, 0.4, [1.0, 0.25, 0.03]);
    const drive = material("blue-white-metric-drive", [0.16, 0.42, 0.6, 1], 0.2, 0.54, [0.2, 1.0, 1.8]);

    addBox("cargo-spine", [7.8, 0.52, 0.72], [0, 0, 0], graphite);
    for (let index = 0; index < 6; index += 1) {
        const x = -2.4 + index * 0.95;
        const side = index % 2 === 0 ? 1 : -1;
        addBox(`cargo-module-${index + 1}`, [0.82, 1.02, 1.15], [x, -0.04, side * 0.76], index % 3 === 0 ? ivory : graphite);
        addBox(`retrofit-panel-${index + 1}`, [0.5, 0.08, 0.72], [x + 0.08, 0.57, side * 0.78], retrofit);
    }
    addCylinder("offset-habitation-module", [0.86, 2.25, 0.86], [-0.2, 1.05, -0.72], ivory, "x");
    addCylinder("forward-docking-collar", [0.72, 0.48, 0.72], [-4.15, 0, 0], retrofit, "x");
    addCylinder("metric-drive-housing", [1.18, 1.24, 1.18], [4.15, 0, 0], graphite, "x");
    addCylinder("metric-drive-emitter", [0.72, 0.2, 0.72], [4.83, 0, 0], drive, "x");
    addBox("radiator-port-forward", [2.2, 0.06, 1.25], [1.65, 0.78, 1.34], radiator);
    addBox("radiator-port-aft", [2.0, 0.06, 1.1], [-1.2, 0.72, 1.3], radiator);
    addBox("radiator-starboard-forward", [2.2, 0.06, 1.25], [1.65, -0.78, -1.34], radiator);
    addBox("radiator-starboard-aft", [2.0, 0.06, 1.1], [-1.2, -0.72, -1.3], radiator);
    for (let index = 0; index < 7; index += 1) {
        addBox(`service-light-${index + 1}`, [0.08, 0.1, 0.1], [-3 + index, 0.46, 0.42], amber);
    }
    addBox("keel-warning-panel", [2.8, 0.1, 0.22], [0.6, -0.42, 0], ivory);

    await kit.write(path);
}

async function buildStation(environment, path) {
    const kit = createKit(`${environment} modular berth station`);
    const { addBox, addCylinder, material } = kit;
    const palette = {
        earth: [[0.28, 0.33, 0.36, 1], [0.64, 0.66, 0.63, 1], [0.2, 0.65, 0.82]],
        mars: [[0.27, 0.18, 0.13, 1], [0.55, 0.32, 0.18, 1], [1.0, 0.28, 0.05]],
        ceres: [[0.18, 0.23, 0.23, 1], [0.39, 0.43, 0.4, 1], [0.25, 0.85, 0.68]],
        pluto: [[0.14, 0.18, 0.22, 1], [0.38, 0.43, 0.46, 1], [0.2, 0.55, 1.0]],
        sirius: [[0.36, 0.4, 0.43, 1], [0.72, 0.74, 0.7, 1], [0.65, 0.9, 1.25]],
    }[environment];
    const structure = material(`${environment}-structure`, palette[0], 0.68, 0.58);
    const panel = material(`${environment}-panel`, palette[1], environment === "sirius" ? 0.35 : 0.76, 0.32);
    const signal = material(`${environment}-traffic-light`, [0.12, 0.16, 0.14, 1], 0.25, 0.25, palette[2]);
    const debris = material(`${environment}-debris`, [0.08, 0.09, 0.095, 1], 0.95, 0.05);
    const spread = environment === "pluto" ? 1.35 : environment === "sirius" ? 0.88 : 1;
    const symmetry = environment === "sirius" ? 1 : environment === "ceres" ? 0.72 : 0.9;

    addBox("truss-main", [9.5 * spread, 0.22, 0.22], [0, 0, 0], structure);
    addBox("truss-cross", [0.24, 5.6 * symmetry, 0.24], [0.4, 0, -0.1], structure);
    addBox("gantry-command", [2.6, 0.48, 1.05], [-1.6, 1.3 * symmetry, -0.3], panel);
    addCylinder("gate-primary", [2.4, 0.32, 2.4], [3.5 * spread, 0, 0], structure, "x");
    addCylinder("docking-collar-alpha", [0.78, 0.45, 0.78], [-4.8 * spread, 1.3 * symmetry, 0.15], panel, "x");
    addCylinder("docking-collar-beta", [0.7, 0.42, 0.7], [-4.2 * spread, -1.35 * symmetry, -0.2], panel, "x");
    addBox("berth-arm-alpha", [4.6 * spread, 0.18, 0.2], [-2.7 * spread, 1.25 * symmetry, 0.15], structure);
    addBox("berth-arm-beta", [4.1 * spread, 0.18, 0.2], [-2.25 * spread, -1.3 * symmetry, -0.15], structure);
    for (let index = 0; index < 6; index += 1) {
        const x = -1.7 * spread + index * 0.65 * spread;
        const y = (index % 2 === 0 ? 1 : -1) * (environment === "ceres" ? 0.6 + index * 0.09 : 0.74);
        addBox(`cargo-module-${index + 1}`, [0.55, 0.62, 0.86], [x, y, -0.45 + (index % 3) * 0.4], index % 2 ? panel : structure);
    }
    for (let index = 0; index < 5; index += 1) {
        addBox(`traffic-light-${index + 1}`, [0.08, 0.12, 0.08], [-3.5 * spread + index * 1.55, 1.38 * symmetry, 0.18], signal);
    }
    for (let index = 0; index < 7; index += 1) {
        const offset = environment === "ceres" ? (index % 2 ? 0.45 : -0.5) : 0;
        addBox(`debris-component-${index + 1}`, [0.18 + (index % 3) * 0.08, 0.08, 0.12], [-1.8 + index * 0.7, -2.2 + offset, -1.2 - index * 0.18], debris);
    }
    if (environment === "earth") addCylinder("monumental-memory-ring", [3.7, 0.18, 3.7], [1.1, 0, -1.4], panel, "x");
    if (environment === "mars") addBox("foundry-crane", [0.4, 3.6, 0.5], [2.1, 1.4, -1.1], structure);
    if (environment === "ceres") addBox("freehold-repair-patch", [1.8, 0.12, 1.1], [0.2, -0.55, 0.8], panel);
    if (environment === "pluto") addBox("migration-freight-spine", [12.5, 0.35, 0.65], [0.4, -0.9, -1.2], structure);
    if (environment === "sirius") addCylinder("compact-control-ring", [3.25, 0.16, 3.25], [0.3, 0, -1.8], panel, "x");

    await kit.write(path);
}

function createKit(sceneName) {
    const document = new Document();
    const buffer = document.createBuffer("geometry");
    const scene = document.createScene(sceneName);
    const meshes = new Map();

    function material(name, color, roughness, metallic, emissive = [0, 0, 0]) {
        return document.createMaterial(name)
            .setBaseColorFactor(color)
            .setRoughnessFactor(roughness)
            .setMetallicFactor(metallic)
            .setEmissiveFactor(emissive);
    }

    function meshFor(kind, mat) {
        const key = `${kind}:${mat.getName()}`;
        if (meshes.has(key)) return meshes.get(key);
        const geometry = kind === "box" ? boxGeometry() : cylinderGeometry(18);
        const position = document.createAccessor(`${key}-position`).setType(Accessor.Type.VEC3).setArray(geometry.positions).setBuffer(buffer);
        const normal = document.createAccessor(`${key}-normal`).setType(Accessor.Type.VEC3).setArray(geometry.normals).setBuffer(buffer);
        const indices = document.createAccessor(`${key}-indices`).setType(Accessor.Type.SCALAR).setArray(geometry.indices).setBuffer(buffer);
        const primitive = document.createPrimitive().setAttribute("POSITION", position).setAttribute("NORMAL", normal).setIndices(indices).setMaterial(mat);
        const mesh = document.createMesh(key).addPrimitive(primitive);
        meshes.set(key, mesh);
        return mesh;
    }

    function addBox(name, scale, translation, mat) {
        scene.addChild(document.createNode(name).setMesh(meshFor("box", mat)).setScale(scale).setTranslation(translation));
    }

    function addCylinder(name, scale, translation, mat, axis = "y") {
        const node = document.createNode(name).setMesh(meshFor("cylinder", mat)).setScale(scale).setTranslation(translation);
        if (axis === "x") node.setRotation([0, 0, Math.SQRT1_2, Math.SQRT1_2]);
        if (axis === "z") node.setRotation([Math.SQRT1_2, 0, 0, Math.SQRT1_2]);
        scene.addChild(node);
    }

    return {
        addBox,
        addCylinder,
        material,
        async write(path) {
            await document.transform(dedup(), prune());
            await io.write(path, document);
        },
    };
}

function boxGeometry() {
    const faces = [
        [[1, 0, 0], [[.5, -.5, -.5], [.5, .5, -.5], [.5, .5, .5], [.5, -.5, .5]]],
        [[-1, 0, 0], [[-.5, -.5, .5], [-.5, .5, .5], [-.5, .5, -.5], [-.5, -.5, -.5]]],
        [[0, 1, 0], [[-.5, .5, -.5], [-.5, .5, .5], [.5, .5, .5], [.5, .5, -.5]]],
        [[0, -1, 0], [[-.5, -.5, .5], [-.5, -.5, -.5], [.5, -.5, -.5], [.5, -.5, .5]]],
        [[0, 0, 1], [[-.5, -.5, .5], [.5, -.5, .5], [.5, .5, .5], [-.5, .5, .5]]],
        [[0, 0, -1], [[.5, -.5, -.5], [-.5, -.5, -.5], [-.5, .5, -.5], [.5, .5, -.5]]],
    ];
    const positions = [];
    const normals = [];
    const indices = [];
    for (let face = 0; face < faces.length; face += 1) {
        const [normal, corners] = faces[face];
        for (const corner of corners) {
            positions.push(...corner);
            normals.push(...normal);
        }
        const base = face * 4;
        indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
    }
    return { positions: new Float32Array(positions), normals: new Float32Array(normals), indices: new Uint16Array(indices) };
}

function cylinderGeometry(segments) {
    const positions = [];
    const normals = [];
    const indices = [];
    for (let side = 0; side < segments; side += 1) {
        const next = (side + 1) % segments;
        const a = side * Math.PI * 2 / segments;
        const b = next * Math.PI * 2 / segments;
        const base = positions.length / 3;
        positions.push(Math.cos(a) * .5, -.5, Math.sin(a) * .5, Math.cos(a) * .5, .5, Math.sin(a) * .5,
            Math.cos(b) * .5, .5, Math.sin(b) * .5, Math.cos(b) * .5, -.5, Math.sin(b) * .5);
        normals.push(Math.cos(a), 0, Math.sin(a), Math.cos(a), 0, Math.sin(a), Math.cos(b), 0, Math.sin(b), Math.cos(b), 0, Math.sin(b));
        indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
    }
    for (const top of [-1, 1]) {
        const center = positions.length / 3;
        positions.push(0, top * .5, 0);
        normals.push(0, top, 0);
        for (let index = 0; index < segments; index += 1) {
            const angle = index * Math.PI * 2 / segments;
            positions.push(Math.cos(angle) * .5, top * .5, Math.sin(angle) * .5);
            normals.push(0, top, 0);
        }
        for (let index = 0; index < segments; index += 1) {
            const current = center + 1 + index;
            const next = center + 1 + ((index + 1) % segments);
            if (top > 0) indices.push(center, next, current);
            else indices.push(center, current, next);
        }
    }
    return { positions: new Float32Array(positions), normals: new Float32Array(normals), indices: new Uint16Array(indices) };
}

async function buildPlate(environment, output) {
    const width = 1536;
    const height = 1000;
    const themes = {
        earth: { sky: [3, 11, 18], glow: [28, 74, 103], planet: [35, 111, 161], rim: [184, 229, 246], seed: 11 },
        mars: { sky: [15, 7, 5], glow: [84, 38, 20], planet: [147, 60, 36], rim: [228, 143, 87], seed: 23 },
        ceres: { sky: [4, 9, 11], glow: [28, 49, 50], planet: [103, 113, 115], rim: [173, 208, 211], seed: 37 },
        pluto: { sky: [2, 6, 12], glow: [25, 48, 71], planet: [113, 104, 98], rim: [173, 209, 228], seed: 53 },
        sirius: { sky: [7, 9, 13], glow: [62, 72, 83], planet: [85, 93, 102], rim: [242, 246, 238], seed: 71 },
    }[environment];
    const pixels = Buffer.alloc(width * height * 3);
    const glowX = environment === "sirius" ? width * .8 : width * .62;
    const glowY = height * .32;
    for (let y = 0; y < height; y += 1) {
        for (let x = 0; x < width; x += 1) {
            const offset = (y * width + x) * 3;
            const distance = Math.hypot((x - glowX) / width, (y - glowY) / height);
            const glow = Math.max(0, 1 - distance * 2.1) ** 2;
            pixels[offset] = Math.round(themes.sky[0] + themes.glow[0] * glow);
            pixels[offset + 1] = Math.round(themes.sky[1] + themes.glow[1] * glow);
            pixels[offset + 2] = Math.round(themes.sky[2] + themes.glow[2] * glow);
        }
    }
    let random = themes.seed;
    const nextRandom = () => ((random = (random * 48271) % 2147483647) / 2147483647);
    for (let index = 0; index < 1100; index += 1) {
        const x = Math.floor(nextRandom() * width);
        const y = Math.floor(nextRandom() * height * .78);
        const value = 110 + Math.floor(nextRandom() * 145);
        plot(pixels, width, height, x, y, [value, value, Math.min(255, value + 14)]);
        if (index % 19 === 0) plot(pixels, width, height, x + 1, y, [value, value, value]);
    }
    const centerX = environment === "earth" ? width * .77 : width * .8;
    const centerY = environment === "sirius" ? height * .76 : height * .67;
    const radius = environment === "earth" ? 330 : environment === "ceres" ? 230 : 285;
    for (let y = Math.max(0, Math.floor(centerY - radius - 5)); y < Math.min(height, Math.ceil(centerY + radius + 5)); y += 1) {
        for (let x = Math.max(0, Math.floor(centerX - radius - 5)); x < Math.min(width, Math.ceil(centerX + radius + 5)); x += 1) {
            const dx = (x - centerX) / radius;
            const dy = (y - centerY) / radius;
            const d = Math.hypot(dx, dy);
            if (d > 1.035) continue;
            const offset = (y * width + x) * 3;
            if (d > 1) {
                const alpha = (1.035 - d) / .035;
                for (let channel = 0; channel < 3; channel += 1) pixels[offset + channel] = Math.round(pixels[offset + channel] * (1 - alpha) + themes.rim[channel] * alpha);
                continue;
            }
            const sphere = Math.sqrt(Math.max(0, 1 - d * d));
            const light = Math.max(.08, sphere * .82 + (-dx * .42));
            const texture = 0.88 + Math.sin(x * .031 + Math.sin(y * .021) * 2) * .08;
            for (let channel = 0; channel < 3; channel += 1) pixels[offset + channel] = Math.round(themes.planet[channel] * light * texture);
        }
    }
    const stationColor = environment === "mars" ? [128, 84, 54] : environment === "sirius" ? [170, 181, 187] : [86, 101, 106];
    drawLine(pixels, width, height, 120, 700, 980, 540, 18, stationColor);
    drawLine(pixels, width, height, 310, 470, 340, 850, 13, stationColor);
    drawRect(pixels, width, height, 260, 590, 330, 70, stationColor);
    drawRect(pixels, width, height, 650, 510, 180, 55, stationColor);
    drawRect(pixels, width, height, 160, 665, 90, 20, themes.rim);

    const ppm = join(buildRoot, `${environment}.ppm`);
    await writeFile(ppm, Buffer.concat([Buffer.from(`P6\n${width} ${height}\n255\n`, "ascii"), pixels]));
    const result = spawnSync("cwebp", ["-quiet", "-q", "82", "-m", "6", ppm, "-o", output], { encoding: "utf8" });
    if (result.status !== 0) throw new Error(`cwebp failed for ${environment}: ${result.stderr || result.stdout}`);
}

function plot(pixels, width, height, x, y, color) {
    if (x < 0 || y < 0 || x >= width || y >= height) return;
    const offset = (y * width + x) * 3;
    pixels[offset] = color[0];
    pixels[offset + 1] = color[1];
    pixels[offset + 2] = color[2];
}

function drawRect(pixels, width, height, x, y, rectWidth, rectHeight, color) {
    for (let yy = y; yy < y + rectHeight; yy += 1) for (let xx = x; xx < x + rectWidth; xx += 1) plot(pixels, width, height, xx, yy, color);
}

function drawLine(pixels, width, height, x0, y0, x1, y1, thickness, color) {
    const steps = Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0));
    for (let index = 0; index <= steps; index += 1) {
        const x = Math.round(x0 + (x1 - x0) * index / steps);
        const y = Math.round(y0 + (y1 - y0) * index / steps);
        drawRect(pixels, width, height, x - Math.floor(thickness / 2), y - Math.floor(thickness / 2), thickness, thickness, color);
    }
}
