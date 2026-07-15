# Frontier 10052 — Technical Architecture

## 1. Architectural goals

- Ship a polished web-first game experience on .NET 10.
- Keep simulation, content, UI, and rendering independently testable.
- Support a single-player vertical slice first without blocking future multiplayer.
- Make world state deterministic or reproducible where practical.
- Stream large content efficiently and degrade gracefully across hardware.
- Treat content tooling, telemetry, accessibility, and automated testing as production systems.

## 2. Recommended solution shape

```text
src/
  Frontier10052.Web/              # Website, launcher, account shell, Blazor UI
  Frontier10052.Client/           # Game client orchestration and presentation
  Frontier10052.Gameplay/         # Use cases and gameplay services
  Frontier10052.Simulation/       # Economy, travel, factions, events, crew
  Frontier10052.Domain/           # Entities, value objects, rules, contracts
  Frontier10052.Content/          # Content schemas, localization, validation
  Frontier10052.Infrastructure/   # Persistence, files, networking, telemetry
  Frontier10052.Server/           # Optional authoritative APIs and sessions
  Frontier10052.Tools/            # Editors, importers, generators, validators
tests/
  Frontier10052.Domain.Tests/
  Frontier10052.Simulation.Tests/
  Frontier10052.IntegrationTests/
  Frontier10052.PlaywrightTests/
doc/
assets/
```

Projects can be introduced gradually. The important boundary is that simulation rules must not live inside Razor components or rendering code.

## 3. Client and rendering strategy

The public site and launcher remain a .NET 10 Blazor Web App. The playable client should use a dedicated real-time rendering layer embedded into the product shell.

Recommended evaluation path:

1. WebGPU-capable browser renderer for ships, stations, planets, effects, and local flight.
2. A typed JavaScript/TypeScript interop boundary controlled by .NET application services.
3. Shared domain and simulation code in .NET.
4. Progressive asset streaming and quality tiers.
5. Optional future native client reusing simulation, services, content, and backend contracts.

Do not force high-frequency rendering through Blazor component updates. Blazor should own application chrome, menus, inventory, market, crew, contracts, codex, accessibility surfaces, and launcher flows. The renderer should own frame-critical visuals and input sampling.

## 4. Simulation model

Use a fixed simulation tick for world systems and a separate render loop.

Simulation domains:

- celestial and route graph;
- travel and arrival scheduling;
- commodity production, consumption, stocks, and prices;
- physical movement of news and warrants;
- faction strategy and regional events;
- NPC and courier routes;
- crew state and relationships;
- ship wear, damage, repair, and resources;
- contracts, obligations, legal state, and reputation.

Run distant systems at lower fidelity using scheduled events and aggregate models. Increase fidelity when the player approaches or when a major event requires detailed resolution.

## 5. Time and information

Every authoritative fact that can become stale should include:

- observed-at time;
- source location;
- source identity;
- confidence;
- provenance chain;
- sensitivity and legal classification;
- expiry or value-decay model.

News propagation must be implemented as transported records attached to ships, couriers, Gate manifests, or local broadcasts. Never update all star systems instantly after a remote event.

## 6. Persistence

Recommended stack:

- PostgreSQL for durable structured state;
- JSONB for versioned content instances and flexible event payloads;
- object storage for assets, screenshots, save exports, and generated media;
- Redis only when needed for session state, short-lived caches, or multiplayer coordination;
- append-only event records for major world changes and player consequences.

Single-player should support local or account-linked saves. Save formats need explicit schema versions and migrations from the beginning.

## 7. Content pipeline

All authored content should use validated schemas:

- star systems and celestial bodies;
- stations and settlements;
- factions and jurisdictions;
- commodities and industries;
- ships, modules, damage states, and audio profiles;
- crew and NPC archetypes;
- dialogue and narrative conditions;
- contracts and dynamic events;
- rumors and information items;
- salvage sites and encounter templates.

Build a validation tool that checks references, localization keys, illegal canon combinations, unreachable content, missing assets, economy anomalies, and broken narrative conditions.

## 8. AI usage

Generative AI may support:

- reactive NPC wording;
- rumor variation;
- captain logs;
- summaries of known events;
- localization assistance;
- content-authoring tools.

It must not be the authoritative game master. Rules, rewards, legal outcomes, economy, canon, safety boundaries, and persistent facts remain deterministic and validated.

NPC generation should be grounded in structured state and constrained outputs. Cache or pre-generate noncritical text where possible to control latency and cost. The game must remain playable when an external model is unavailable.

## 9. Multiplayer path

Design single-player commands as explicit requests against an authoritative simulation boundary. Later multiplayer can move selected simulations server-side.

Potential stages:

1. Account profiles, cloud saves, and shared telemetry.
2. Asynchronous shared discoveries and market reports.
3. Cooperative private crew sessions.
4. Authoritative shared regions and events.
5. Larger persistent-world features only after proven demand.

Avoid promising a galaxy-wide MMO during initial production.

## 10. Security and anti-cheat

- Never trust client-submitted inventory, money, position, or contract completion in multiplayer modes.
- Sign or validate save transfers where online rewards are involved.
- Rate-limit APIs and protect expensive AI endpoints.
- Separate public content from secrets and operational configuration.
- Use server-side authorization for accounts, purchases, shared worlds, and moderation.
- Instrument suspicious economy mutations without collecting unnecessary personal data.

## 11. Performance budgets

Set budgets early for:

- initial site load;
- launcher-to-game transition;
- time to interactive;
- frame time and frame pacing;
- memory use;
- GPU texture budget;
- network payloads;
- simulation tick duration;
- save duration;
- asset streaming stalls.

The vertical slice must run acceptably on a defined mid-range target device before additional visual scope is approved.

## 12. Testing strategy

### Unit tests

- pricing and inventory rules;
- travel time, fuel, and decay;
- information propagation;
- law and reputation transitions;
- crew modifiers;
- damage and repair dependencies.

### Simulation tests

- run thousands of accelerated days;
- detect runaway inflation, dead markets, impossible routes, permanent shortages, and event storms;
- verify that news cannot arrive without a carrier;
- verify reproducibility from a seed and command sequence.

### Integration tests

- persistence and migrations;
- content loading and validation;
- account and save flows;
- server authorization;
- AI fallback behavior.

### End-to-end tests

- website to launcher to new game;
- first contract;
- market purchase and sale;
- travel and arrival;
- save, reload, and resume;
- accessibility navigation.

## 13. Observability

Capture privacy-respecting telemetry for crashes, loading stalls, frame pacing, abandoned flows, economy anomalies, failed contracts, control remapping, and accessibility usage.

Use telemetry to remove friction and defects, not to create manipulative retention systems.

## 14. Definition of architectural readiness

The foundation is ready for content scale when:

- the simulation can run headlessly;
- a deterministic seed can reproduce a reported issue;
- content validates in CI;
- the UI consumes application contracts rather than domain internals;
- saves migrate across versions;
- the renderer can be replaced or upgraded without rewriting game rules;
- the first complete journey can be automated in an end-to-end test.