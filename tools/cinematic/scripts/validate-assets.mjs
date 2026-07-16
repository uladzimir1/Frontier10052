import { NodeIO } from "@gltf-transform/core";
import { readFile, stat, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const toolRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = resolve(toolRoot, "../..");
const webRoot = join(repoRoot, "src/Frontier10052.Web/wwwroot");
const assetRoot = join(webRoot, "assets/cinematic");
const catalog = JSON.parse(await readFile(join(assetRoot, "scene-catalog.json"), "utf8"));
const errors = [];
const files = [];
const models = [];
const io = new NodeIO();
const expectedEnvironments = ["earth", "mars", "ceres", "pluto", "sirius"];
const expectedRoutes = [
    "earth-mars-relief-corridor",
    "mars-ceres-repair-lane",
    "mars-pluto-migration-corridor",
    "ceres-sirius-intelligence-run",
    "pluto-sirius-intelligence-run",
];
const expectedStages = [
    "departure-preview", "clamp-release-undock", "gravity-boundary-burn", "transfer-montage", "encounter-arrival",
    "response-outcome", "delayed-labor-warning", "pinch-lattice-drift", "destination-approach", "docking-handoff",
];

if (catalog.version !== 1) errors.push("catalog.version must be 1");
if (!sameSet(Object.keys(catalog.environments), expectedEnvironments)) errors.push("catalog environment coverage is incomplete");
if (!sameSet(Object.keys(catalog.routes), expectedRoutes)) errors.push("catalog route coverage is incomplete");
if (!sameSet(Object.keys(catalog.cameraTracks), expectedStages)) errors.push("camera track coverage is incomplete");

for (const [routeId, environments] of Object.entries(catalog.routes)) {
    if (!Array.isArray(environments) || environments.length !== 2) errors.push(`${routeId} must reference origin and destination environments`);
    for (const environment of environments) if (!catalog.environments[environment]) errors.push(`${routeId} references missing environment ${environment}`);
}

for (const environment of expectedEnvironments) {
    const entry = catalog.environments[environment];
    await checkFile(entry.plate, 1_000_000);
    await checkGlb(entry.station, true);
}
await checkGlb(catalog.ship, false);
await checkFile("js/cinematic-renderer.js", 1_500_000);
await checkFile("assets/cinematic/ASSET_LICENSES.md", 20_000);

const totalBytes = files.reduce((sum, file) => sum + file.bytes, 0);
if (totalBytes > 8_000_000) errors.push(`initial cinematic payload ${totalBytes} bytes exceeds 8 MB`);
const shipDrawCalls = models.find(model => model.path === catalog.ship)?.primitives ?? 0;
const stationDrawCalls = Math.max(...models.filter(model => model.path !== catalog.ship).map(model => model.primitives));
// Planet, atmosphere, moons, stars, nebula, dust, asteroids, traffic, lattice, and exhaust.
const proceduralDrawCalls = 10;
const estimatedVisibleDrawCalls = shipDrawCalls + stationDrawCalls + proceduralDrawCalls;
if (estimatedVisibleDrawCalls >= 120) errors.push(`estimated visible draw calls ${estimatedVisibleDrawCalls} must remain below 120`);

const report = {
    status: errors.length ? "failed" : "passed",
    versions: { three: "0.185.1", esbuild: "0.28.1", gltfTransform: "4.4.1" },
    budgets: { totalCompressedBytes: 8_000_000, individualGlbBytes: 4_000_000, rendererBytes: 1_500_000, visibleDrawCalls: 120 },
    coverage: { environments: expectedEnvironments, routes: expectedRoutes, stages: expectedStages },
    totalBytes,
    estimatedVisibleDrawCalls,
    models,
    files,
    errors,
};
await writeFile(join(assetRoot, "asset-validation.json"), `${JSON.stringify(report, null, 2)}\n`);
if (errors.length) {
    for (const error of errors) console.error(`ERROR: ${error}`);
    process.exitCode = 1;
} else {
    console.log(`Cinematic assets validated: ${files.length} files, ${(totalBytes / 1_000_000).toFixed(2)} MB total.`);
}

async function checkFile(relativePath, maximum) {
    try {
        const info = await stat(join(webRoot, relativePath));
        files.push({ path: relativePath, bytes: info.size, budget: maximum, valid: info.size <= maximum });
        if (info.size > maximum) errors.push(`${relativePath} is ${info.size} bytes; budget is ${maximum}`);
        return info;
    } catch {
        errors.push(`${relativePath} is missing`);
        return null;
    }
}

async function checkGlb(relativePath, station) {
    const info = await checkFile(relativePath, 4_000_000);
    if (!info) return;
    try {
        const document = await io.read(join(webRoot, relativePath));
        const root = document.getRoot();
        if (root.listMeshes().length === 0) errors.push(`${relativePath} contains no meshes`);
        if (root.listMaterials().length < 3) errors.push(`${relativePath} must contain at least three authored materials`);
        models.push({
            path: relativePath,
            meshes: root.listMeshes().length,
            primitives: root.listMeshes().reduce((sum, mesh) => sum + mesh.listPrimitives().length, 0),
            materials: root.listMaterials().length,
            textures: root.listTextures().length,
        });
        const names = root.listNodes().map(node => node.getName());
        if (station) {
            for (const component of ["truss", "berth-arm", "docking-collar", "cargo-module", "gate", "traffic-light", "gantry", "debris-component"]) {
                if (!names.some(name => name.includes(component))) errors.push(`${relativePath} is missing ${component} geometry`);
            }
        } else {
            for (const component of ["cargo-spine", "offset-habitation", "radiator", "docking-collar", "retrofit-panel", "service-light", "metric-drive"]) {
                if (!names.some(name => name.includes(component))) errors.push(`${relativePath} is missing ${component} geometry`);
            }
        }
    } catch (error) {
        errors.push(`${relativePath} is not a readable GLB: ${error.message}`);
    }
}

function sameSet(left, right) {
    return left.length === right.length && left.every(value => right.includes(value));
}
