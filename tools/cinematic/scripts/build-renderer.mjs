import { build } from "esbuild";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const toolRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = resolve(toolRoot, "../..");

await build({
    entryPoints: [join(toolRoot, "src/renderer.js")],
    outfile: join(repoRoot, "src/Frontier10052.Web/wwwroot/js/cinematic-renderer.js"),
    bundle: true,
    format: "esm",
    platform: "browser",
    target: ["es2022"],
    minify: true,
    legalComments: "eof",
    sourcemap: false,
});
