# Frontier 10052 — Vertical Slice Backlog

## Backlog rules

- Work top to bottom unless a dependency is explicitly marked parallel.
- Every story must produce observable player value or reduce a named production risk.
- A story is not complete without tests, error handling, and documentation updates.
- New abstractions require at least two real callers or a clear boundary need.
- Keep the first playable loop operable after every merged increment.

## Epic 0 — Repository foundation

### VS-001 Create solution projects

**Outcome:** The documented architecture exists as buildable projects.

**Acceptance criteria**

- Domain, Simulation, Gameplay, Content, Infrastructure, and test projects exist.
- Project references enforce the documented dependency direction.
- The existing Web project references Gameplay contracts only.
- `dotnet build` succeeds on a clean checkout.

### VS-002 Add engineering defaults

**Acceptance criteria**

- Nullable reference types are enabled.
- An `.editorconfig` is committed.
- Warnings are treated as errors in production projects.
- Central package management is enabled or explicitly rejected in an ADR.
- A deterministic test culture is documented in the contributor guide.

### VS-003 Add CI

**Acceptance criteria**

- Pull requests run restore, format verification, build, and tests.
- The Web project is published in Release configuration.
- Test results are visible in GitHub Actions.

## Epic 1 — Domain primitives

### VS-010 Add typed identifiers and value objects

Implement IDs for game, station, system, ship, crew, commodity, faction, contract, route, and encounter. Implement money, mass, cargo quantity, fuel, distance, and game time.

**Acceptance criteria**

- Invalid negative values are rejected where prohibited.
- Money arithmetic has explicit currency and rounding behavior.
- Value objects serialize predictably.
- Unit tests cover equality, comparison, arithmetic, and invalid construction.

### VS-011 Define errors and command results

**Acceptance criteria**

- Expected gameplay failures do not use exceptions for control flow.
- Errors have stable codes and player-safe messages/localization keys.
- Unexpected failures remain observable through logs and diagnostics.

## Epic 2 — Content pipeline

### VS-020 Define versioned content schemas

**Acceptance criteria**

- Schemas exist for systems, stations, routes, commodities, industries, ships, crew, factions, contracts, encounters, and localization.
- Every authored object has a stable ID and schema version.
- Content can be loaded without referencing the Web project.

### VS-021 Implement content validation

**Acceptance criteria**

- Duplicate IDs, missing references, invalid ranges, unreachable routes, cargo inconsistencies, and missing localization keys fail validation.
- Validation can run from tests and a command-line tool.
- CI blocks invalid content.

### VS-022 Add `vertical-slice-v0` seed pack

**Acceptance criteria**

- Sol, Earth Heritage Station, Mars Industrial Port, one route, one ship, four crew, six commodities, two factions/interests, three contracts, and three encounters load successfully.
- The seed pack contains no UI-hardcoded data dependencies.

## Epic 3 — Deterministic simulation foundation

### VS-030 Add game clock and deterministic random source

**Acceptance criteria**

- Production code never calls ambient random or wall-clock APIs inside simulation rules.
- A seed can reproduce encounter selection and market variation.
- Simulation time can advance in tests without waiting in real time.

### VS-031 Add world state and command journal

**Acceptance criteria**

- World state has an explicit schema/version.
- Each accepted state-changing command receives a monotonic sequence number.
- A canonical state representation can be compared in replay tests.

## Epic 4 — Economy and cargo

### VS-040 Implement station stocks and pricing

**Acceptance criteria**

- Stations produce and consume configured commodities.
- Price derives from base value, stock pressure, and configured modifiers.
- The same seed and time produce the same market state.
- Prices cannot become negative or non-finite.

### VS-041 Implement buy and sell use cases

**Acceptance criteria**

- Money, station stock, ship cargo, and capacity update atomically.
- Insufficient money, stock, and capacity return typed failures.
- Transactions are integration tested.

### VS-042 Add observed market reports

**Acceptance criteria**

- A report records observed time, source, confidence, and expiry/decay.
- The UI can distinguish live local prices from stale remote information.
- Remote prices do not update magically.

## Epic 5 — Ship, crew, and contracts

### VS-050 Implement ship resources and wear

**Acceptance criteria**

- Ship state includes cargo capacity, fuel, hull, drive wear, and repair state.
- Travel consumes fuel and increases wear deterministically.
- Invalid departure is blocked with actionable errors.

### VS-051 Implement four crew roles

**Acceptance criteria**

- Pilot, engineer, security, and medic each affect at least one real calculation or encounter option.
- Crew state includes loyalty, fatigue, and availability.
- Crew effects are visible in outcome explanations.

### VS-052 Implement delivery contracts

**Acceptance criteria**

- Contracts have origin, destination, cargo, quantity, deadline, reward, penalty, issuer, and status.
- Accepting reserves or requires cargo according to template rules.
- Completion and failure modify money and reputation.

## Epic 6 — Travel and encounter loop

### VS-060 Implement route planning and departure

**Acceptance criteria**

- A valid route is required.
- Fuel and estimated arrival are displayed before confirmation.
- Departure changes ship location and travel state atomically.

### VS-061 Implement travel advancement

**Acceptance criteria**

- Travel progresses using simulation time, not animation completion.
- Encounters can interrupt progression at deterministic points.
- Arrival is impossible before route requirements are satisfied.

### VS-062 Implement three encounter templates

**Acceptance criteria**

- Inspection, mechanical failure, and pirate demand each expose at least two valid responses.
- Available responses depend on ship, cargo, crew, faction, or money state.
- Outcomes are deterministic for the same seed and command sequence.

### VS-063 Implement docking and contract completion

**Acceptance criteria**

- Docking sets the authoritative location and makes station services available.
- Delivery validates cargo and destination.
- Completion persists reward, reputation, cargo removal, wear, and world time.

## Epic 7 — Save, load, and replay

### VS-070 Add versioned snapshot saves

**Acceptance criteria**

- New game, save, load, overwrite, and corrupt-save error paths exist.
- The save envelope records game version, content pack, schema version, seed, and command sequence.
- Save writes are atomic.

### VS-071 Add replay diagnostics

**Acceptance criteria**

- A test can recreate a game from seed plus accepted commands.
- Replay reaches the same canonical state as the saved snapshot.
- Failed replay inputs can be exported without private account data.

## Epic 8 — Minimal playable UI

### VS-080 Add new-game and resume flow

**Acceptance criteria**

- A player can start the fixed vertical-slice seed and resume the latest save.
- Loading and validation failures show recoverable UI.

### VS-081 Add station dashboard

Display current location, ship summary, crew, contracts, market, cargo, money, reports, and save action.

### VS-082 Add market and contract screens

**Acceptance criteria**

- Buy/sell quantities are validated before submission.
- The player sees why an action is unavailable.
- Contract consequences and deadlines are clear.

### VS-083 Add route and travel screens

**Acceptance criteria**

- Route cost, duration, fuel, risk, and information freshness are visible.
- Travel advancement and encounter resolution use application commands.
- Animation cannot directly alter authoritative state.

### VS-084 Add end-to-end smoke test

**Acceptance criteria**

- Playwright starts a new game, accepts a contract, buys cargo, travels, resolves an encounter, docks, completes delivery, saves, reloads, and verifies the result.

## Epic 9 — Rendering spike (parallel after Epic 3)

### VS-090 Evaluate browser renderer

Create equivalent representative scenes in the two leading candidates and record:

- first-load size;
- frame pacing;
- WebGPU/WebGL fallback;
- asset streaming;
- .NET interop cost;
- input handling;
- accessibility coexistence;
- mobile/low-tier degradation;
- maintainability and licensing.

**Exit criterion:** Accept an ADR selecting a renderer or rejecting browser real-time 3D for the first release.

### VS-091 Add renderer boundary

**Acceptance criteria**

- The renderer consumes immutable presentation snapshots and sends explicit input commands.
- It cannot access persistence or mutate domain objects.
- A no-renderer/headless client remains supported.

## Milestone gates

### Foundation complete

VS-001 through VS-031 are merged and CI is green.

### Headless first playable

VS-040 through VS-071 are merged and one deterministic journey passes end to end.

### UI first playable

VS-080 through VS-084 are merged and a new player can finish the loop without developer tools.

### Flight prototype ready

VS-090 and VS-091 are complete, performance evidence is recorded, and simulation/render boundaries remain intact.
