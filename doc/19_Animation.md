# Frontier 10052 — Animation

## Pillars

Animation communicates mass, preparation, competence, fatigue, and consequence. Movement must support gameplay readability while preserving cinematic quality.

## Ships

Ships rotate before applying major thrust. Maneuvering jets fire from plausible positions. Acceleration, drift, braking, docking alignment, and damaged control response reflect mass and system state. Large stations move with scale and restraint.

## Docking and mechanisms

Docking includes approach guidance, alignment, capture, seal verification, pressure equalization, cargo connection, and release. Airlocks, landing gear, radiators, cargo doors, turrets, and repair mechanisms have readable states.

## Characters

Locomotion supports varied gravity, suits, injury, fatigue, carried mass, confined corridors, and emergency movement. Crew work emphasizes believable procedures: tool use, bracing, diagnostics, medical intervention, cargo handling, watch handover, and repairs.

## Performance acting

Dialogue animation needs attentive listening, disagreement, hesitation, cultural gestures, stress, exhaustion, and professional composure. Avoid constant exaggerated motion.

## Combat and damage

Characters brace, seal suits, fight fires, stabilize casualties, and react to decompression or system loss. Ship impacts propagate through camera, props, crew balance, lighting, and machinery.

## Procedural layers

Use inverse kinematics, look-at, hand placement, foot placement, recoil, balance, interaction alignment, and additive injury or fatigue. Procedural systems supplement authored motion rather than conceal poor base animation.

## Performance

Define animation LOD, update budgets, network-ready state representation, deterministic event triggers, and reduced-motion alternatives. Critical gameplay indicators cannot depend exclusively on subtle animation.

## Implemented travel timelines

Travel animation is presentation-only and samples a normalized 0–1 timeline in
JavaScript. Blazor receives ready, paused, completed, fallback, and error states;
per-frame camera, ship, station, engine, traffic, dust, and lattice motion never
crosses the interop boundary.

The camera track follows the cue stage: berth orbit, collar clearance, metric
drive rear, side tracking, contact wide, hull close, radio observation, lattice
orbit, station observation, or capture collar. Ship movement preserves the core
mass language by rotating clear before thrust and separating approach,
alignment, capture, seal, and handoff.

High, Balanced, and Low profiles cap device pixel ratio at 2, 1.5, and 1.
Automatic begins at Balanced, promotes only on capable desktop hardware, and
drops from High after sustained over-budget frame time. Rendering pauses when
the scene is offscreen or the tab is hidden. Scene changes dispose models,
geometries, materials, textures, audio, and render lists while retaining the
page's one canvas context; leaving `/travel` also disposes observers, renderer,
and context resources.

Reduced camera motion selects static authored cuts and disables automatic
scrolling. Low bandwidth skips GLB creation entirely. A lost WebGL context pauses
the timeline, exposes the current fallback plate, and permits one controlled
restoration attempt.
