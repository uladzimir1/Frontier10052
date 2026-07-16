# Frontier 10052

AAA-aspirational hard-science-fiction trading, exploration, crew, and civilization game set in the year 10052.

## Current implementation slice

- Cinematic solar-to-Andromeda landing journey based on the selected Visual 1 direction
- Living-station scroll story and interactive one-minute concept trailer
- Docked-ship launcher with commander, crew, cargo, news, patch, and settings states
- Archival crew dossiers with generated documentary portraits, stable crew identity, and reduced-motion-aware scan and depth effects across launcher, operations, travel, arrivals, and Sirius decisions
- Deterministic Earth-to-Mars first-contract onboarding with crew briefing, contract cargo, conflicting market reports, optional trading, engineer assignment, and departure authorization
- Mars turnaround with an inherited lien, refueling, provenance-aware repairs, crew rest, and mutually exclusive Pluto/Ceres contracts
- Deterministic route-specific crises and destination settlement with persistent crew memories, faction standing, legal exposure, debt, maintenance, and journey history
- First interstellar crossing from the actual Ceres or Pluto settlement, with metric-drive pinch reserve, provenance-bearing information cargo, five ordered route checkpoints, a corporate labor conflict, Sirius customs, and persistent settlement
- Sirius Meridian aftermath where the forecast becomes a frozen 12-of-36 actuator shortage, Noor and Tomas contest the response, Wayfarer allocates every unit, and two non-accepting future leads persist
- Browser-owned local journeys backed by schema-5, checksummed, atomic server-side JSON saves with deterministic schema-1 through schema-4 migration
- Responsive editorial routes for the universe, factions, map, ships, colonies, crew, lore, roadmap, and community
- .NET 10 Blazor Web App foundation

## Game documentation

The production vision, gameplay systems, architecture, roadmap, and website/game UX are documented in [`doc/README.md`](doc/README.md).

Key documents:

- [`doc/AAA_GAME_VISION.md`](doc/AAA_GAME_VISION.md)
- [`doc/GAMEPLAY_SYSTEMS.md`](doc/GAMEPLAY_SYSTEMS.md)
- [`doc/TECHNICAL_ARCHITECTURE.md`](doc/TECHNICAL_ARCHITECTURE.md)
- [`doc/PRODUCTION_ROADMAP.md`](doc/PRODUCTION_ROADMAP.md)
- [`doc/WEBSITE_AND_GAME_UX.md`](doc/WEBSITE_AND_GAME_UX.md)

## Run locally

```bash
dotnet run --project src/Frontier10052.Web
```

Then open the HTTPS URL printed by ASP.NET Core.

The browser stores only the anonymous selector key `frontier10052:player:v1`; authoritative journey state remains server-side. Set `Frontier10052__SavesDirectory` to choose the save directory. Local runs default to the ignored `src/Frontier10052.Web/App_Data/saves` directory, while the runtime container uses its owned `/data` path.

## Solution boundaries

The implementation follows the dependency direction established in the architecture documents:

```text
Web -> Gameplay -> Simulation -> Domain
Content --------------------------> Domain
Infrastructure -> Gameplay and Domain contracts
```

The automated test projects cover typed domain values, deterministic pricing and state, content validation, station operations, persistence, corrupt-save recovery, all 12 Sirius origin/message/mechanical combinations, schema-1 through schema-4 migration, customs, settlement, crew-conflict pressure, every surfaced actuator allocation, repair provenance, future-lead visibility, rejected-command atomicity, and canonical determinism. Run them locally with:

```bash
dotnet test Frontier10052.slnx
```

## Containers

The repository uses one OCI `Containerfile` with compatible Podman and Docker targets. Podman is the default local engine.

Run the full test suite in a clean container:

```bash
./scripts/container.sh
```

The engine-name shorthand runs that same test target explicitly through either engine:

```bash
./scripts/container.sh podman
./scripts/container.sh docker
```

Build and run the web application at `http://localhost:8080`:

```bash
./scripts/container.sh run
```

Docker can run the same application target with `./scripts/container.sh run docker`; Podman remains the default. Set `FRONTIER10052_PORT` to change the host port or `CONTAINER_ENGINE` to override the default engine.
