# Cinematic asset pipeline

This pinned Node pipeline owns the presentation-only assets used by `/travel`.
It generates the Tern-class Wayfarer, the five modular station kits, static WebP
scene plates, the locally bundled Three.js renderer, and a deterministic
validation report.

```bash
npm ci
npm test
npm run build
```

`npm run build` writes deployable files below
`src/Frontier10052.Web/wwwroot/assets/cinematic` and
`src/Frontier10052.Web/wwwroot/js/cinematic-renderer.js`. Generated assets are
committed so the .NET container build does not require Node.

The validator checks route, environment, and camera-track coverage; required
ship and station components; readable GLBs; material and texture counts;
licenses; payload limits; and the estimated visible draw-call budget. Runtime
quality selection and fallback behavior are tested separately in the browser.
