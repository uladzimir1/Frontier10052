const homeControllers = new WeakMap();
const launcherControllers = new WeakMap();

export function initializeHome(root) {
    disposeHome(root);

    const controller = new AbortController();
    const { signal } = controller;
    const introKey = "frontier10052:intro:v1";
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    let revealTimer;
    let trailerFrame = 0;
    let trailerElapsed = 0;
    let trailerStartedAt = 0;
    let trailerAnimation;
    let trailerPlaying = false;

    const reveal = (remember = true) => {
        window.clearTimeout(revealTimer);
        root.classList.remove("intro-playing", "intro-returning");
        root.classList.add("intro-revealed");
        if (remember) {
            window.localStorage.setItem(introKey, "seen");
        }
    };

    const playIntro = () => {
        root.classList.remove("intro-revealed", "intro-returning");
        root.classList.add("intro-playing");
        revealTimer = window.setTimeout(() => reveal(true), 9000);
    };

    if (reducedMotion) {
        root.classList.add("intro-reduced", "intro-revealed");
    } else if (window.localStorage.getItem(introKey) === "seen") {
        root.classList.add("intro-returning");
        revealTimer = window.setTimeout(() => reveal(false), 1300);
    } else {
        playIntro();
    }

    root.querySelector("[data-skip-intro]")?.addEventListener("click", () => reveal(true), { signal });
    root.querySelector("[data-replay-intro]")?.addEventListener("click", playIntro, { signal });

    const livingImage = root.querySelector("[data-living-image]");
    const livingJourney = root.querySelector("[data-living-journey]");
    let scrollTicking = false;

    const updateJourney = () => {
        scrollTicking = false;
        if (!livingImage || !livingJourney || reducedMotion) return;
        const rect = livingJourney.getBoundingClientRect();
        const travel = Math.max(1, rect.height - window.innerHeight);
        const progress = Math.min(1, Math.max(0, -rect.top / travel));
        const scale = 1.09 - progress * 0.09;
        const shift = -2.5 * progress;
        livingImage.style.transform = `scale(${scale}) translate3d(${shift}%, 0, 0)`;
    };

    const requestJourneyUpdate = () => {
        if (scrollTicking) return;
        scrollTicking = true;
        window.requestAnimationFrame(updateJourney);
    };

    window.addEventListener("scroll", requestJourneyUpdate, { passive: true, signal });
    window.addEventListener("resize", requestJourneyUpdate, { passive: true, signal });
    requestJourneyUpdate();

    const trailer = root.querySelector("[data-trailer]");
    const frames = [...root.querySelectorAll("[data-trailer-frame]")];
    const caption = root.querySelector("[data-trailer-caption]");
    const progress = root.querySelector("[data-trailer-progress]");
    const playLabel = root.querySelector("[data-trailer-toggle]");
    const trailerDuration = 60000;

    const renderTrailer = elapsed => {
        const normalized = Math.min(1, elapsed / trailerDuration);
        trailerFrame = Math.min(frames.length - 1, Math.floor(normalized * frames.length));
        frames.forEach((frame, index) => frame.classList.toggle("active", index === trailerFrame));
        if (caption && frames[trailerFrame]) caption.textContent = frames[trailerFrame].dataset.caption ?? "";
        if (progress) progress.style.width = `${normalized * 100}%`;
    };

    const trailerLoop = now => {
        if (!trailerPlaying) return;
        trailerElapsed = Math.min(trailerDuration, trailerElapsed + now - trailerStartedAt);
        trailerStartedAt = now;
        renderTrailer(trailerElapsed);
        if (trailerElapsed >= trailerDuration) {
            trailerPlaying = false;
            if (playLabel) playLabel.textContent = "Replay";
            return;
        }
        trailerAnimation = window.requestAnimationFrame(trailerLoop);
    };

    const playTrailer = () => {
        if (trailerElapsed >= trailerDuration) trailerElapsed = 0;
        trailerPlaying = true;
        trailerStartedAt = performance.now();
        if (playLabel) playLabel.textContent = "Pause";
        trailerAnimation = window.requestAnimationFrame(trailerLoop);
    };

    const pauseTrailer = () => {
        trailerPlaying = false;
        window.cancelAnimationFrame(trailerAnimation);
        if (playLabel) playLabel.textContent = "Play";
    };

    root.querySelectorAll("[data-open-trailer]").forEach(button => {
        button.addEventListener("click", () => {
            trailer?.showModal();
            trailerElapsed = 0;
            renderTrailer(0);
            playTrailer();
        }, { signal });
    });

    root.querySelector("[data-close-trailer]")?.addEventListener("click", () => trailer?.close(), { signal });
    playLabel?.addEventListener("click", () => trailerPlaying ? pauseTrailer() : playTrailer(), { signal });
    trailer?.addEventListener("close", pauseTrailer, { signal });
    trailer?.addEventListener("cancel", pauseTrailer, { signal });

    bindSoundControl(root, signal);
    homeControllers.set(root, () => {
        controller.abort();
        window.clearTimeout(revealTimer);
        window.cancelAnimationFrame(trailerAnimation);
        stopAmbient(root);
    });
}

export function disposeHome(root) {
    homeControllers.get(root)?.();
    homeControllers.delete(root);
}

export function initializeLauncher(root) {
    disposeLauncher(root);
    const controller = new AbortController();
    bindSoundControl(root, controller.signal);
    launcherControllers.set(root, () => {
        controller.abort();
        stopAmbient(root);
    });
}

export function disposeLauncher(root) {
    launcherControllers.get(root)?.();
    launcherControllers.delete(root);
}

function bindSoundControl(root, signal) {
    const button = root.querySelector("[data-sound-toggle]");
    if (!button) return;
    const stored = window.localStorage.getItem("frontier10052:sound") === "on";
    button.textContent = stored ? "Sound on" : "Sound off";
    button.setAttribute("aria-pressed", stored ? "true" : "false");

    button.addEventListener("click", async () => {
        const isOn = button.getAttribute("aria-pressed") === "true";
        if (isOn) {
            stopAmbient(root);
            window.localStorage.setItem("frontier10052:sound", "off");
            button.textContent = "Sound off";
            button.setAttribute("aria-pressed", "false");
        } else {
            await startAmbient(root);
            window.localStorage.setItem("frontier10052:sound", "on");
            button.textContent = "Sound on";
            button.setAttribute("aria-pressed", "true");
        }
    }, { signal });
}

async function startAmbient(root) {
    stopAmbient(root);
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    const context = new AudioContext();
    const gain = context.createGain();
    const filter = context.createBiquadFilter();
    const low = context.createOscillator();
    const high = context.createOscillator();
    gain.gain.value = 0.012;
    filter.type = "lowpass";
    filter.frequency.value = 180;
    low.frequency.value = 48;
    high.frequency.value = 73;
    low.type = "sine";
    high.type = "triangle";
    low.connect(filter);
    high.connect(filter);
    filter.connect(gain);
    gain.connect(context.destination);
    low.start();
    high.start();
    root._frontierAudio = { context, low, high };
}

function stopAmbient(root) {
    const audio = root._frontierAudio;
    if (!audio) return;
    audio.low.stop();
    audio.high.stop();
    audio.context.close();
    delete root._frontierAudio;
}
