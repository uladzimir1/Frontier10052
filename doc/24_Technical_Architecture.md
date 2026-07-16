# Frontier 10052 — Technical Architecture

## Goals

Maintainable .NET 10 architecture, deterministic or reproducible simulation, data-driven content, fast web delivery, robust saves, automated testing, observability, and an optional path to online services.

## Solution boundaries

- **Presentation:** Blazor pages/components, HUD, input, rendering integration, accessibility.
- **Application:** commands, queries, orchestration, validation, use cases.
- **Domain:** time, navigation, economy, information, ship, crew, factions, law, quests, encounters, narrative, saves.
- **Content:** schemas, loaders, localization, validation, versioning.
- **Infrastructure:** persistence, files, telemetry, networking, external AI, platform services.
- **Online services:** accounts, cloud saves, social or multiplayer features when required.

## Simulation

Use explicit world time, scheduled events, seeded randomness, immutable identifiers, and traceable commands. Long-running state changes use clocks and queues rather than UI timers. Simulation can advance at different rates while preserving causality.

## Persistence

Versioned save snapshots, migrations, content manifests, checksums, backups, and diagnostic event history. Storage models do not leak into domain rules.

## Client

The current foundation is a .NET 10 Blazor Web App. Keep browser performance, progressive loading, responsive layout, keyboard/controller input, asset streaming, and reduced-motion support central. Rendering technology may evolve behind stable gameplay interfaces.

### Cinematic presentation boundary

`JourneySnapshot`, encounter presentations, and checkpoint presentations expose
derived `CinematicPresentation` metadata. The mapper reads canonical state after
an accepted command and never writes to `GameState`, `IJourneyService`, or the
schema-5 save model. Arrival is committed before the docking cue exists.

Blazor owns semantic controls, consequences, captions, transcripts, focus, and
the eventual `/operations` navigation. A locally bundled JavaScript controller
owns the scroll timeline and all frames through the intentionally low-frequency
API `initialize`, `setCue`, `play`, `pause`, `skip`, `replay`, `setQuality`, and
`dispose`. Only ready/paused/completed/fallback/error state crosses back to
Blazor, tagged with its cue ID so callbacks from a replaced scene are ignored.

Three.js 0.185.1 renders only when WebGL2 is available. The controller caps DPR,
pauses offscreen/hidden work, interrupts autoplay on direct navigation input,
and disposes scene resources on cue/page changes. Static WebP plates are the
first-class fallback for reduced motion, low bandwidth, unavailable WebGL,
context loss, and `?cinematic=static` verification.

The pinned build pipeline is isolated in `tools/cinematic`; generated assets are
committed so the normal .NET build and OCI runtime remain Node-free. Its
deterministic validation report records exact version, coverage, model, texture,
license, payload, and estimated draw-call checks.

## Testing

Unit tests for rules, property tests for economy and routing, deterministic replay, save migration tests, content validation, integration tests, browser tests, performance budgets, and accessibility audits. Cinematic coverage tests enumerate all current routes, encounters, response outcomes, and Sirius checkpoint choices, assert cue uniqueness and timing, and compare canonical state before and after repeated presentation derivation.

## Security

Validate all commands and content, protect accounts and saves, isolate mods, minimize secrets, use secure defaults, rate-limit online APIs, and treat generative AI output as untrusted data.

## Observability

Structured logs, traces, metrics, crash reports, simulation diagnostics, content-version context, and consent-based player telemetry.
