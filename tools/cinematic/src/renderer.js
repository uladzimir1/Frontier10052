import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { normalizedProgress, qualityProfile, seenCueKey, stagePose } from "./timeline.js";

const controllers = new WeakMap();
let catalogPromise;

export async function initialize(root, dotNetReference, options = {}) {
    dispose(root);
    const controller = new CinematicController(root, dotNetReference, options);
    controllers.set(root, controller);
    await controller.initialize();
}

export async function setCue(root, cue, context = {}) {
    await requireController(root).setCue(cue, context);
}

export function play(root) { requireController(root).play(); }
export function pause(root) { requireController(root).pause(true); }
export function skip(root) { requireController(root).skip(); }
export function replay(root) { requireController(root).replay(); }
export async function setQuality(root, quality) { await requireController(root).setQuality(quality); }

export function dispose(root) {
    controllers.get(root)?.dispose();
    controllers.delete(root);
}

export function simulateContextLoss(root) {
    const controller = requireController(root);
    controller.renderer?.forceContextLoss();
}

function requireController(root) {
    const controller = controllers.get(root);
    if (!controller) throw new Error("Cinematic renderer is not initialized for this element.");
    return controller;
}

class CinematicController {
    constructor(root, dotNetReference, options) {
        this.root = root;
        this.dotNet = dotNetReference;
        this.options = options;
        this.gameId = options.gameId ?? "unknown-game";
        this.preferences = options.preferences ?? {};
        this.canvas = root.querySelector("[data-cinematic-canvas]");
        this.fallback = root.querySelector("[data-cinematic-fallback]");
        this.caption = root.querySelector("[data-cinematic-caption]");
        this.progressBar = root.querySelector("[data-cinematic-progress]");
        this.progressText = root.querySelector("[data-cinematic-progress-text]");
        this.stateLabel = root.querySelector("[data-cinematic-state]");
        this.playButton = root.querySelector("[data-cinematic-play]");
        this.controller = new AbortController();
        this.progress = 0;
        this.playing = false;
        this.visible = true;
        this.disposed = false;
        this.contextRestoreAttempted = false;
        this.frameSamples = [];
        this.lastFrame = 0;
        this.suppressScrollUpdates = false;
    }

    async initialize() {
        const { signal } = this.controller;
        this.scrollOwner = findScrollOwner(this.root);
        this.catalog = await loadCatalog();
        const update = () => this.updateFromScroll();
        const interrupt = () => this.playing && this.pause(true);
        const target = this.scrollOwner === window ? window : this.scrollOwner;
        target.addEventListener("scroll", update, { passive: true, signal });
        target.addEventListener("wheel", interrupt, { passive: true, signal });
        target.addEventListener("touchstart", interrupt, { passive: true, signal });
        window.addEventListener("resize", () => { this.resize(); update(); }, { passive: true, signal });
        window.addEventListener("keydown", event => {
            if (["ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End", " "].includes(event.key)) interrupt();
        }, { signal });
        document.addEventListener("visibilitychange", () => {
            if (document.hidden) this.pause(false);
            else this.renderOnce();
        }, { signal });
        this.canvas?.addEventListener("webglcontextlost", event => this.onContextLost(event), { signal });
        this.canvas?.addEventListener("webglcontextrestored", () => this.onContextRestored(), { signal });
        this.root.querySelector("[data-cinematic-play]")?.addEventListener("click", () => this.playing ? this.pause(true) : this.play(), { signal });
        this.root.querySelector("[data-cinematic-skip]")?.addEventListener("click", () => this.skip(), { signal });
        this.root.querySelector("[data-cinematic-replay]")?.addEventListener("click", () => this.replay(), { signal });
        this.intersection = new IntersectionObserver(entries => {
            this.visible = entries[0]?.isIntersecting ?? true;
            if (this.visible) this.renderOnce();
        }, { root: this.scrollOwner === window ? null : this.scrollOwner, threshold: 0.01 });
        this.intersection.observe(this.root);
    }

    async setCue(cue, context = {}) {
        this.suppressScrollUpdates = true;
        this.pause(false);
        this.disposeScene(false);
        this.cue = cue;
        this.gameId = context.gameId ?? this.gameId;
        this.preferences = { ...this.preferences, ...(context.preferences ?? {}) };
        this.quality = qualityProfile(this.preferences.quality, hardwareProfile());
        this.reducedMotion = Boolean(this.preferences.reducedCameraMotion || window.matchMedia("(prefers-reduced-motion: reduce)").matches);
        this.root.style.setProperty("--cinematic-scroll-vh", String(cue.scrollLengthVh));
        this.root.dataset.cue = cue.cueId;
        this.root.dataset.stage = cue.stage;
        this.root.dataset.environment = cue.environment;
        this.caption.textContent = cue.caption;
        this.caption.hidden = this.preferences.captions === false;
        this.fallback.src = cue.fallbackPlate;
        this.fallback.alt = "";

        const seen = window.localStorage.getItem(seenCueKey(cue, this.gameId)) === "seen";
        const webgl2 = Boolean(this.canvas?.getContext("webgl2", { failIfMajorPerformanceCaveat: true }));
        const forcedStatic = new URLSearchParams(window.location.search).get("cinematic") === "static";
        this.fallbackMode = Boolean(this.quality.fallback || this.reducedMotion || forcedStatic || !webgl2);
        this.root.classList.toggle("cinematic-fallback-active", this.fallbackMode);
        this.root.classList.toggle("cinematic-reduced", this.reducedMotion);

        try {
            if (!this.fallbackMode) await this.createScene();
            this.setProgress(seen ? 1 : 0, false);
            this.scrollToProgress(seen ? 1 : 0);
            if (this.fallbackMode) this.emit("fallback");
            else this.emit(seen ? "completed" : "ready");
        } catch (error) {
            console.error("Frontier cinematic renderer failed; using static plate.", error);
            this.fallbackMode = true;
            this.root.classList.add("cinematic-fallback-active");
            this.setProgress(seen ? 1 : 0, false);
            this.scrollToProgress(seen ? 1 : 0);
            this.emit("error");
        } finally {
            requestAnimationFrame(() => requestAnimationFrame(() => {
                this.suppressScrollUpdates = false;
            }));
        }
    }

    async createScene() {
        const environment = this.catalog.environments[this.cue.environment];
        if (!environment) throw new Error(`Unknown cinematic environment: ${this.cue.environment}`);
        const route = this.catalog.routes[this.cue.routeId];
        if (!route) throw new Error(`Unknown cinematic route: ${this.cue.routeId}`);

        this.renderer ??= new THREE.WebGLRenderer({ canvas: this.canvas, antialias: this.quality.name === "High", alpha: false, powerPreference: "high-performance" });
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = this.cue.environment === "sirius" ? 1.25 : 1.05;
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, this.quality.dpr));
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(this.cue.environment === "mars" ? 0x090302 : 0x010407);
        this.scene.fog = new THREE.FogExp2(this.scene.background, this.cue.environment === "mars" ? 0.016 : 0.011);
        this.camera = new THREE.PerspectiveCamera(42, 1, 0.1, 700);
        this.world = new THREE.Group();
        this.scene.add(this.world);

        const ambient = new THREE.HemisphereLight(environment.atmosphere, 0x050607, this.cue.environment === "sirius" ? 2.1 : 1.15);
        const key = new THREE.DirectionalLight(environment.light, this.cue.environment === "sirius" ? 5.8 : 3.2);
        key.position.set(-8, 7, 10);
        this.scene.add(ambient, key);
        this.createPlanet(environment, route);
        this.createStars(environment);
        this.createNebula(environment);
        this.createDust(environment);
        this.createTraffic(environment);
        this.createLattice(environment);

        const loader = new GLTFLoader();
        const [shipGltf, stationGltf] = await Promise.all([loader.loadAsync(this.catalog.ship), loader.loadAsync(environment.station)]);
        if (this.disposed || !this.scene) return;
        this.ship = shipGltf.scene;
        this.ship.name = "Wayfarer";
        this.ship.scale.setScalar(0.58);
        this.ship.rotation.set(0.08, -0.28, 0.04);
        this.station = stationGltf.scene;
        this.station.name = `${this.cue.environment}-station`;
        this.station.scale.setScalar(this.cue.environment === "pluto" ? 0.62 : 0.72);
        this.station.rotation.set(0.2, -0.44, -0.12);
        this.world.add(this.ship, this.station);
        this.createExhaust(environment);
        this.driveMaterials = [];
        this.ship.traverse(object => {
            if (object.isMesh && object.name.toLowerCase().includes("metric-drive")) {
                for (const material of Array.isArray(object.material) ? object.material : [object.material]) this.driveMaterials.push(material);
            }
        });
        this.resize();
        this.renderOnce();
    }

    createPlanet(environment, route) {
        const isDestination = ["destination-approach", "docking-handoff"].includes(this.cue.stage);
        const radius = this.cue.environment === "ceres" ? 3.1 : this.cue.environment === "pluto" ? 3.8 : 5.2;
        const detail = this.quality.name === "High" ? 64 : 36;
        const geometry = new THREE.SphereGeometry(radius, detail, Math.max(18, detail / 2));
        if (this.cue.environment === "ceres") {
            const position = geometry.attributes.position;
            for (let index = 0; index < position.count; index += 1) {
                const x = position.getX(index);
                const y = position.getY(index);
                const z = position.getZ(index);
                const distortion = 1 + Math.sin(x * 2.7 + y * 1.9 + z * 2.2) * 0.045;
                position.setXYZ(index, x * distortion, y * distortion, z * distortion);
            }
            geometry.computeVertexNormals();
        }
        const material = new THREE.MeshStandardMaterial({ color: environment.planet, roughness: 0.94, metalness: 0.02 });
        this.planet = new THREE.Mesh(geometry, material);
        this.planet.position.set(isDestination ? 7 : -8, -5.3, -17);
        this.planet.rotation.z = 0.18;
        const atmosphere = new THREE.Mesh(
            new THREE.SphereGeometry(radius * 1.035, detail, Math.max(18, detail / 2)),
            new THREE.MeshBasicMaterial({ color: environment.atmosphere, transparent: true, opacity: this.cue.environment === "ceres" ? 0.035 : 0.12, side: THREE.BackSide, blending: THREE.AdditiveBlending }));
        this.planet.add(atmosphere);
        this.world.add(this.planet);
        this.createMoons(environment, isDestination);
        if (["ceres", "pluto"].includes(this.cue.environment)) this.createAsteroids(environment);
    }

    createMoons(environment, isDestination) {
        const count = this.cue.environment === "mars" ? 2 : 1;
        const geometry = new THREE.IcosahedronGeometry(this.cue.environment === "earth" ? .62 : .38, 2);
        const material = new THREE.MeshStandardMaterial({ color: environment.accent, roughness: 1, metalness: 0 });
        this.moons = new THREE.InstancedMesh(geometry, material, count);
        const matrix = new THREE.Matrix4();
        const side = isDestination ? 1 : -1;
        for (let index = 0; index < count; index += 1) {
            matrix.makeTranslation(side * (3.2 + index * 1.4), 3.2 - index * 1.3, -11 - index * 2.4);
            this.moons.setMatrixAt(index, matrix);
        }
        this.world.add(this.moons);
    }

    createStars(environment) {
        if (!this.quality.stars) return;
        const random = seededRandom(hash(this.cue.routeId));
        const positions = new Float32Array(this.quality.stars * 3);
        const colors = new Float32Array(this.quality.stars * 3);
        const accent = new THREE.Color(environment.atmosphere);
        for (let index = 0; index < this.quality.stars; index += 1) {
            const radius = 90 + random() * 280;
            const theta = random() * Math.PI * 2;
            const phi = Math.acos(2 * random() - 1);
            positions[index * 3] = radius * Math.sin(phi) * Math.cos(theta);
            positions[index * 3 + 1] = radius * Math.cos(phi);
            positions[index * 3 + 2] = radius * Math.sin(phi) * Math.sin(theta);
            const mix = random() * .24;
            colors[index * 3] = 1 - (1 - accent.r) * mix;
            colors[index * 3 + 1] = 1 - (1 - accent.g) * mix;
            colors[index * 3 + 2] = 1 - (1 - accent.b) * mix;
        }
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
        geometry.setAttribute("color", new THREE.BufferAttribute(colors, 3));
        this.stars = new THREE.Points(geometry, new THREE.PointsMaterial({ size: .34, vertexColors: true, transparent: true, opacity: .82, sizeAttenuation: true }));
        this.world.add(this.stars);
    }

    createNebula(environment) {
        const count = this.quality.name === "High" ? 180 : 90;
        const random = seededRandom(hash(`${this.cue.routeId}:nebula`));
        const positions = new Float32Array(count * 3);
        for (let index = 0; index < count; index += 1) {
            positions[index * 3] = (random() - .5) * 85;
            positions[index * 3 + 1] = (random() - .5) * 24 + Math.sin(index * .17) * 3;
            positions[index * 3 + 2] = -34 - random() * 58;
        }
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
        this.nebula = new THREE.Points(geometry, new THREE.PointsMaterial({ color: environment.atmosphere, size: 5.4, transparent: true, opacity: .018, depthWrite: false, blending: THREE.AdditiveBlending }));
        this.world.add(this.nebula);
    }

    createExhaust(environment) {
        const geometry = new THREE.ConeGeometry(.28, 2.4, 16, 1, true);
        const material = new THREE.MeshBasicMaterial({ color: environment.atmosphere, transparent: true, opacity: .28, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide });
        this.exhaust = new THREE.Mesh(geometry, material);
        this.exhaust.name = "procedural-metric-drive-exhaust";
        this.exhaust.position.set(-3.7, 0, .05);
        this.exhaust.rotation.z = Math.PI / 2;
        this.ship.add(this.exhaust);
    }

    createDust(environment) {
        if (!this.quality.dust) return;
        const random = seededRandom(hash(this.cue.cueId));
        const positions = new Float32Array(this.quality.dust * 3);
        for (let index = 0; index < this.quality.dust; index += 1) {
            positions[index * 3] = (random() - .5) * 42;
            positions[index * 3 + 1] = (random() - .5) * 18;
            positions[index * 3 + 2] = (random() - .5) * 45;
        }
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
        this.dust = new THREE.Points(geometry, new THREE.PointsMaterial({ color: environment.accent, size: .055, transparent: true, opacity: .28 }));
        this.world.add(this.dust);
    }

    createAsteroids(environment) {
        const random = seededRandom(hash(this.cue.environment));
        const geometry = new THREE.IcosahedronGeometry(0.24, 1);
        const material = new THREE.MeshStandardMaterial({ color: environment.planet, roughness: 1 });
        this.asteroids = new THREE.InstancedMesh(geometry, material, this.quality.asteroids);
        const matrix = new THREE.Matrix4();
        for (let index = 0; index < this.quality.asteroids; index += 1) {
            const scale = .25 + random() * 1.3;
            matrix.compose(
                new THREE.Vector3((random() - .5) * 24, (random() - .5) * 10, -5 - random() * 22),
                new THREE.Quaternion().setFromEuler(new THREE.Euler(random() * 3, random() * 3, random() * 3)),
                new THREE.Vector3(scale, scale * (.6 + random() * .7), scale));
            this.asteroids.setMatrixAt(index, matrix);
        }
        this.world.add(this.asteroids);
    }

    createTraffic(environment) {
        if (!this.quality.traffic) return;
        const geometry = new THREE.BoxGeometry(.18, .06, .05);
        const material = new THREE.MeshBasicMaterial({ color: environment.accent });
        this.traffic = new THREE.InstancedMesh(geometry, material, this.quality.traffic);
        const matrix = new THREE.Matrix4();
        for (let index = 0; index < this.quality.traffic; index += 1) {
            matrix.makeTranslation(-7 + index * .72, 2.8 - (index % 4) * .55, -3 - (index % 3) * 1.3);
            this.traffic.setMatrixAt(index, matrix);
        }
        this.world.add(this.traffic);
    }

    createLattice(environment) {
        const geometry = new THREE.TorusKnotGeometry(2.2, .015, this.quality.name === "High" ? 220 : 120, 6, 3, 5);
        const material = new THREE.MeshBasicMaterial({ color: environment.atmosphere, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, wireframe: true });
        this.lattice = new THREE.Mesh(geometry, material);
        this.lattice.visible = false;
        this.world.add(this.lattice);
    }

    updateFromScroll() {
        if (!this.cue || this.playing || this.suppressScrollUpdates) return;
        const rect = this.root.getBoundingClientRect();
        const viewport = this.scrollOwner === window ? window.innerHeight : this.scrollOwner.clientHeight;
        const ownerTop = this.scrollOwner === window ? 0 : this.scrollOwner.getBoundingClientRect().top;
        const next = normalizedProgress(rect.top - ownerTop, rect.height, viewport);
        this.setProgress(next, true);
    }

    setProgress(value, allowCompletion) {
        if (!this.cue) return;
        this.progress = Math.min(1, Math.max(0, value));
        const percent = Math.round(this.progress * 100);
        this.root.style.setProperty("--cinematic-progress", String(this.progress));
        if (this.progressBar) this.progressBar.style.width = `${percent}%`;
        if (this.progressText) this.progressText.textContent = `Scene ${percent}%`;
        this.renderOnce();
        if (allowCompletion && this.progress >= .999) this.complete();
    }

    renderOnce() {
        if (!this.renderer || !this.scene || !this.camera || !this.visible || document.hidden) return;
        const started = performance.now();
        const pose = stagePose(this.cue.stage, this.progress, this.reducedMotion);
        this.camera.position.set(...pose.camera);
        this.camera.lookAt(...pose.target);
        if (this.ship) {
            this.ship.position.set(...pose.ship);
            this.ship.rotation.y = -.28 + this.progress * .34;
        }
        if (this.station) this.station.position.set(...pose.station);
        if (this.planet) this.planet.rotation.y = this.progress * .18;
        if (this.stars) this.stars.rotation.y = this.progress * .018;
        if (this.dust) this.dust.position.z = this.progress * 4;
        if (this.traffic) this.traffic.position.x = this.progress * 3.2;
        if (this.lattice) {
            this.lattice.visible = pose.lattice > .01;
            this.lattice.position.set(...pose.ship);
            this.lattice.rotation.set(this.progress * 1.7, this.progress * 2.3, this.progress * .8);
            this.lattice.material.opacity = pose.lattice * .48;
            this.lattice.scale.setScalar(.7 + pose.lattice * .5);
        }
        if (this.exhaust) {
            this.exhaust.scale.set(1, .55 + pose.drive * .45, 1);
            this.exhaust.material.opacity = .12 + pose.drive * .16;
        }
        for (const material of this.driveMaterials ?? []) material.emissiveIntensity = pose.drive;
        this.renderer.render(this.scene, this.camera);
        this.sampleFrame(performance.now() - started);
    }

    sampleFrame(frameTime) {
        if (this.quality?.name !== "High") return;
        this.frameSamples.push(frameTime);
        if (this.frameSamples.length < 120) return;
        const average = this.frameSamples.reduce((sum, value) => sum + value, 0) / this.frameSamples.length;
        this.frameSamples.length = 0;
        if (average > 22) this.setQuality("Balanced");
    }

    play() {
        if (!this.cue || this.playing) return;
        if (this.reducedMotion) {
            this.setProgress(1, true);
            return;
        }
        if (this.progress >= .999) this.setProgress(0, false);
        this.playing = true;
        this.playStarted = performance.now() - this.progress * this.cue.durationSeconds * 1000;
        if (this.playButton) this.playButton.textContent = "Pause";
        this.startAudio();
        const step = now => {
            if (!this.playing) return;
            const progress = (now - this.playStarted) / (this.cue.durationSeconds * 1000);
            this.setProgress(progress, true);
            this.scrollToProgress(this.progress);
            if (this.progress < 1) this.animation = requestAnimationFrame(step);
        };
        this.animation = requestAnimationFrame(step);
    }

    pause(notify) {
        if (!this.playing && !notify) return;
        this.playing = false;
        cancelAnimationFrame(this.animation);
        this.stopAudio();
        if (this.playButton) this.playButton.textContent = "Play";
        if (notify && this.cue && this.progress < .999) this.emit("paused");
    }

    skip() {
        if (!this.cue) return;
        this.pause(false);
        this.setProgress(1, false);
        this.scrollToProgress(1);
        this.complete();
        this.root.nextElementSibling?.querySelector("button:not(:disabled), a[href]")?.focus({ preventScroll: true });
    }

    replay() {
        if (!this.cue) return;
        this.pause(false);
        this.completedCueKey = null;
        this.setProgress(0, false);
        this.scrollToProgress(0);
        this.emit(this.fallbackMode ? "fallback" : "ready");
        this.play();
    }

    complete() {
        if (!this.cue) return;
        const completionKey = seenCueKey(this.cue, this.gameId);
        if (this.completedCueKey === completionKey) return;
        this.completedCueKey = completionKey;
        this.pause(false);
        window.localStorage.setItem(completionKey, "seen");
        this.emit("completed");
    }

    scrollToProgress(progress) {
        const viewport = this.scrollOwner === window ? window.innerHeight : this.scrollOwner.clientHeight;
        const travel = Math.max(0, this.root.offsetHeight - viewport);
        if (this.scrollOwner === window) {
            const top = window.scrollY + this.root.getBoundingClientRect().top;
            window.scrollTo({ top: top + travel * progress, behavior: "instant" });
        } else {
            const ownerRect = this.scrollOwner.getBoundingClientRect();
            const top = this.scrollOwner.scrollTop + this.root.getBoundingClientRect().top - ownerRect.top;
            this.scrollOwner.scrollTo({ top: top + travel * progress, behavior: "instant" });
        }
    }

    async setQuality(quality) {
        this.preferences.quality = quality;
        if (!this.cue) return;
        const progress = this.progress;
        await this.setCue(this.cue, { gameId: this.gameId, preferences: this.preferences });
        this.setProgress(progress, false);
    }

    resize() {
        if (!this.renderer || !this.camera) return;
        const width = Math.max(1, this.canvas.clientWidth);
        const height = Math.max(1, this.canvas.clientHeight);
        this.renderer.setSize(width, height, false);
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
    }

    onContextLost(event) {
        event.preventDefault();
        this.pause(false);
        this.fallbackMode = true;
        this.root.classList.add("cinematic-fallback-active");
        this.emit("fallback");
        if (!this.contextRestoreAttempted) {
            this.contextRestoreAttempted = true;
            this.root.dataset.cinematicRestore = "attempted";
            window.setTimeout(() => this.renderer?.forceContextRestore?.(), 300);
        }
    }

    async onContextRestored() {
        if (!this.contextRestoreAttempted || !this.cue) return;
        const progress = this.progress;
        try {
            this.disposeScene(false);
            this.fallbackMode = false;
            this.root.classList.remove("cinematic-fallback-active");
            await this.createScene();
            this.setProgress(progress, false);
            this.root.dataset.cinematicRestore = "restored";
            this.emit("ready");
        } catch {
            this.fallbackMode = true;
            this.root.classList.add("cinematic-fallback-active");
            this.emit("error");
        }
    }

    startAudio() {
        if (window.localStorage.getItem("frontier10052:sound") !== "on" || this.audio) return;
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        if (!AudioContext) return;
        const context = new AudioContext();
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.type = "sine";
        oscillator.frequency.value = this.cue.stage === "pinch-lattice-drift" ? 84 : 52;
        gain.gain.value = .009;
        oscillator.connect(gain).connect(context.destination);
        oscillator.start();
        this.audio = { context, oscillator };
    }

    stopAudio() {
        if (!this.audio) return;
        this.audio.oscillator.stop();
        this.audio.context.close();
        this.audio = null;
    }

    emit(state) {
        if (this.stateLabel) this.stateLabel.textContent = state;
        this.root.dataset.cinematicState = state;
        this.dotNet?.invokeMethodAsync("OnCinematicStateChanged", state, this.cue?.cueId ?? "").catch(() => {});
    }

    disposeScene(disposeRenderer = true) {
        this.stopAudio();
        if (this.scene) disposeObject(this.scene);
        this.renderer?.renderLists?.dispose?.();
        if (disposeRenderer) {
            this.renderer?.dispose();
            this.renderer = null;
        }
        this.scene = null;
        this.camera = null;
        this.world = null;
        this.ship = null;
        this.station = null;
        this.moons = null;
        this.nebula = null;
        this.exhaust = null;
        this.driveMaterials = [];
    }

    dispose() {
        this.disposed = true;
        this.pause(false);
        this.controller.abort();
        this.intersection?.disconnect();
        this.disposeScene();
    }
}

async function loadCatalog() {
    catalogPromise ??= fetch(new URL("../assets/cinematic/scene-catalog.json", import.meta.url))
        .then(response => {
            if (!response.ok) throw new Error(`Scene catalog failed with HTTP ${response.status}.`);
            return response.json();
        });
    return catalogPromise;
}

function findScrollOwner(element) {
    let current = element.parentElement;
    while (current) {
        const overflow = getComputedStyle(current).overflowY;
        if (["auto", "scroll"].includes(overflow) && current.scrollHeight > current.clientHeight) return current;
        current = current.parentElement;
    }
    return window;
}

function hardwareProfile() {
    return {
        width: window.innerWidth,
        memory: navigator.deviceMemory ?? 4,
        cores: navigator.hardwareConcurrency ?? 4,
        touch: navigator.maxTouchPoints > 0,
    };
}

function disposeObject(root) {
    root.traverse(object => {
        object.geometry?.dispose?.();
        const materials = Array.isArray(object.material) ? object.material : object.material ? [object.material] : [];
        for (const material of materials) {
            for (const value of Object.values(material)) if (value?.isTexture) value.dispose();
            material.dispose?.();
        }
    });
}

function hash(value) {
    let result = 2166136261;
    for (let index = 0; index < value.length; index += 1) result = Math.imul(result ^ value.charCodeAt(index), 16777619);
    return result >>> 0;
}

function seededRandom(seed) {
    let value = seed || 1;
    return () => {
        value = Math.imul(1664525, value) + 1013904223 >>> 0;
        return value / 4294967296;
    };
}
