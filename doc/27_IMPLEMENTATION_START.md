# Frontier 10052 — Implementation Start Plan

## Purpose

This document converts the production vision into the first executable engineering milestone. It is intentionally narrower than the full vertical slice: the goal is to establish the boundaries, tooling, data contracts, and one end-to-end playable loop on which later systems can safely grow.

## 1. First executable milestone

Build a deterministic, headless trade journey that can also be driven from the Blazor UI:

1. Create a new game from a known seed.
2. Load Sol, two stations, one ship, four crew members, and a small commodity catalog from validated content files.
3. Accept a delivery contract.
4. Buy cargo at Station A.
5. Advance travel through explicit simulation commands.
6. Resolve one seeded encounter.
7. Dock at Station B.
8. Deliver or sell the cargo.
9. Persist the resulting money, inventory, ship wear, crew state, reputation, and world time.
10. Reload the save and reproduce the same state.

This milestone does not require real-time 3D flight. A simple route screen and travel timeline are sufficient. The renderer should be prototyped in parallel, but it must not block the simulation loop.

## 2. Initial solution structure

Create projects in this order:

```text
src/
  Frontier10052.Domain/
  Frontier10052.Simulation/
  Frontier10052.Gameplay/
  Frontier10052.Content/
  Frontier10052.Infrastructure/
  Frontier10052.Web/
tests/
  Frontier10052.Domain.Tests/
  Frontier10052.Simulation.Tests/
  Frontier10052.IntegrationTests/
```

### Responsibilities

- `Domain`: entities, value objects, domain errors, invariants, commands, results, and interfaces with no infrastructure dependencies.
- `Simulation`: deterministic world clock, markets, travel, encounters, crew effects, ship wear, contracts, law, and reputation.
- `Gameplay`: application use cases, orchestration, DTOs, save/load flows, and transaction boundaries.
- `Content`: schemas, JSON loading, reference resolution, localization keys, validation, and seed packs.
- `Infrastructure`: PostgreSQL/EF Core, file saves, clocks, random sources, telemetry adapters, and object storage adapters.
- `Web`: launcher, menus, market, contract, route, crew, ship, save, and diagnostics UI.

Dependency direction:

```text
Web -> Gameplay -> Simulation -> Domain
Infrastructure -> Gameplay/Domain contracts
Content -> Domain contracts
```

No Razor component may mutate domain state directly.

## 3. Required architectural decisions

Record these as ADRs before expanding implementation:

1. Blazor hosting mode and authentication boundary.
2. Browser rendering prototype: Babylon.js or Three.js/WebGPU evaluation.
3. Local-save format versus PostgreSQL-backed account saves.
4. Deterministic random-number strategy and seed ownership.
5. Simulation tick and time-scale rules.
6. JSON content schema/versioning approach.
7. Event log versus current-state persistence boundary.
8. Money, mass, distance, time, and quantity numeric representations.

Until an ADR is accepted, use the simplest reversible implementation.

## 4. Core technical conventions

- Target `.NET 10` with nullable reference types enabled.
- Treat warnings as errors in production projects.
- Use UTC internally; expose a game-world timestamp type rather than raw `DateTime` in domain rules.
- Use decimal or fixed-point value objects for money and commodity quantities; never use floating point for financial calculations.
- Assign stable string IDs to authored content and strongly typed IDs to runtime entities.
- All state-changing use cases return a typed result containing success data or domain errors.
- All simulation randomness comes from an injected deterministic random source.
- Commands must be serializable so a defect can be reproduced from seed plus command history.
- Persist explicit schema versions for saves and content.
- Avoid MediatR, event sourcing, distributed messaging, Redis, and microservices until the first loop proves a need.

## 5. Seed content required for milestone one

Create a `content/vertical-slice-v0/` pack containing:

- Sol system.
- Earth Heritage Station and Mars Industrial Port.
- One fixed route between them.
- One aging merchant ship definition.
- Four crew members: pilot, engineer, security, medic.
- Six commodities: food, water, medicine, machine parts, fuel cells, and data cores.
- Two industries per station.
- Three contract templates.
- One customs faction and one opposing commercial or criminal interest.
- Three encounter templates: inspection, mechanical failure, and pirate demand.
- Localization keys for all player-visible text.

All references must be validated at build or test time.

## 6. First use cases

Implement these application services before adding broad UI:

```text
NewGame
GetGameSummary
GetStationMarket
AcceptContract
BuyCommodity
SellCommodity
PlanRoute
DepartStation
AdvanceTravel
ResolveEncounter
DockAtStation
CompleteContract
SaveGame
LoadGame
```

Each use case must have an integration test using the same seed content pack.

## 7. Persistence baseline

For the first milestone, store a complete versioned save snapshot plus an append-only command journal for diagnostics.

Minimum save envelope:

```json
{
  "schemaVersion": 1,
  "gameVersion": "0.1.0",
  "contentPackId": "vertical-slice-v0",
  "seed": 10052,
  "savedAtUtc": "...",
  "worldState": {},
  "playerState": {},
  "commandSequence": 42
}
```

The snapshot is authoritative for loading. The command journal is used for debugging and deterministic replay tests; it is not yet a full event-sourced architecture.

## 8. Testing gates

The first milestone is complete only when CI proves:

- the solution builds from a clean checkout;
- content validation fails on broken IDs, negative values, duplicate IDs, and missing localization keys;
- the same seed and command sequence produce byte-equivalent canonical state;
- buying cannot create negative money or cargo capacity;
- travel cannot complete without enough fuel and a valid route;
- delivery changes money, cargo, contract state, reputation, and world time correctly;
- save/load preserves all authoritative state;
- a headless end-to-end test completes the full trade journey;
- a Playwright smoke test starts a new game and reaches the market screen.

## 9. CI baseline

Add a GitHub Actions workflow that runs on pull requests and `main`:

1. `dotnet restore`
2. `dotnet format --verify-no-changes`
3. `dotnet build -c Release --no-restore`
4. `dotnet test -c Release --no-build`
5. content validation
6. publish the web project
7. Playwright smoke tests when browser assets are available

Store failed deterministic replay input as a test artifact.

## 10. Definition of ready for real-time flight

Do not couple the renderer to unfinished simulation logic. Begin the full flight prototype after:

- route and travel state are exposed through stable gameplay DTOs;
- ship resources and damage have explicit contracts;
- input commands can be recorded;
- simulation and render clocks are separate;
- a renderer spike demonstrates acceptable frame pacing on the agreed reference Mac and a mid-range Windows machine.

## 11. Immediate implementation order

1. Create projects and enforce dependency boundaries.
2. Add value objects and IDs.
3. Add content schemas, loader, validator, and seed pack.
4. Add deterministic clock and random source.
5. Implement world, market, cargo, ship, crew, contract, and route state.
6. Implement the first use cases.
7. Add snapshot persistence and replay journal.
8. Add headless end-to-end tests.
9. Add minimal Blazor screens for the loop.
10. Run a renderer spike behind an interface without moving rules into JavaScript.

Anything not required by this order belongs in the backlog, not in the first implementation milestone.
