# Frontier 10052 — Cinematics

## Philosophy

Cinematics reinforce ownership of the ship and the scale of travel without repeatedly taking control away. Most travel cinematics are short, skippable, state-aware transitions that flow directly into gameplay.

## Sequence library

- undocking from a station;
- leaving planetary orbit;
- flying past large stations;
- accelerating toward another planet;
- approaching a moon;
- entering an asteroid belt or debris field;
- escaping pirates or police pursuit;
- docking;
- Gate activation;
- long-distance interstellar jump;
- arrival in a new system;
- emergency landing;
- damaged ship drifting;
- entering Andromeda through a major Gate.

## Camera language

Exterior chase, distant cinematic, cockpit, hull-close, engine-focused rear, side tracking, station observation, planetary flyby, rotating orbit, and wide establishing shots. Camera selection depends on event, ship condition, environment, and prior repetition.

## Effects

Realistic engine glow, maneuvering thrusters, hull reflections, dust, debris, planetary light, restrained motion blur, subtle camera shake, atmospheric entry, weapon fire, defensive impacts, engine damage, jump distortion, docking lights, and mechanical movement.

## Story cinematics

Major scenes support player choices, alternate crew presence, ship state, equipment, injuries, and prior relationships. In-engine staging is preferred where it preserves continuity and lowers content duplication.

## Control and accessibility

Cinematics are skippable after state is safely committed. Provide pause, subtitles, speaker labels, reduced camera motion, flash reduction, and a replayable journal for important communications.

## Current travel renderer

`/travel` derives a `CinematicPresentation` from the authoritative journey
snapshot. The cue identifies its route, stage, environment, event or checkpoint,
command sequence, timing, fallback plate, and continuation path. It is output-only:
Play, Pause, Skip, Replay, scroll progress, and scene completion cannot issue a
gameplay command or mutate a save.

One normalized 0–1 timeline drives both manual scrolling and Play mode. Preview,
undock, and approach scenes use 12 seconds over 300vh; departure burn, lattice,
and docking use 14 seconds over 340vh; transit, contact, message, and outcome use
9 seconds over 240vh. Wheel, touch, and navigation-key input pauses Play
immediately. Docking starts only after arrival has committed, and `/operations`
navigation waits for scene completion or Skip.

The authored cue library covers the five current routes, five legacy encounters,
both five-checkpoint Sirius variants, every encounter/checkpoint response, and
the committed docking handoff. Resolved checkpoint metadata remains available
for replay without re-executing its command.

The renderer is a local Three.js 0.185.1 WebGL2 bundle. The Tern-class Wayfarer
and modular station kits are GLBs; planets, stars, atmospheres, dust, traffic,
asteroids, exhaust, and the pinch lattice are procedural. Earth, Mars, Ceres,
Pluto, and Sirius each have a distinct light, body, station, and fallback plate.

Unsupported WebGL2, low-bandwidth, reduced-motion, context-loss, and forced
static modes use authored WebP cuts. Reduced-motion mode performs no camera
travel, parallax, shake, flashing, or automatic scrolling. The canvas is
decorative and `aria-hidden`; captions, speaker labels, transcripts, progress,
and all decisions remain semantic HTML.

Presentation preferences live under
`frontier10052:cinematic:preferences:v1`. Seen state is keyed by game ID,
command sequence, and cue ID, so an unseen reload restarts the scene while a
seen cue opens at its final frame with Replay available.

## Pipeline

Storyboard → animatic → gameplay-state contract → camera and blocking → animation/VFX/audio → branching validation → performance and accessibility review.

The implemented asset path lives in `tools/cinematic`. Pinned Node dependencies
generate the GLBs and plates, bundle the renderer, and validate coverage,
licenses, camera tracks, asset references, readable model structure, draw calls,
and file budgets before output is committed beneath `wwwroot/assets/cinematic`.
