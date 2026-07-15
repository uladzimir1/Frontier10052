# Frontier 10052 — Game Documentation

This directory is the production-facing documentation hub for **Frontier 10052**, an AAA-aspirational hard-science-fiction trading, exploration, crew, and civilization game set in the year 10052.

The game is designed as a **web-first premium experience** built on .NET 10, with a cinematic public website and launcher, a playable single-player foundation, and a future path toward shared-world multiplayer.

## Core documents

| Document | Purpose |
|---|---|
| [AAA_GAME_VISION.md](AAA_GAME_VISION.md) | Creative vision, player fantasy, gameplay pillars, experience principles, world integration, and quality bar. |
| [GAMEPLAY_SYSTEMS.md](GAMEPLAY_SYSTEMS.md) | The playable loops: travel, trade, information, crew, ships, factions, crime, exploration, survival, and progression. |
| [TECHNICAL_ARCHITECTURE.md](TECHNICAL_ARCHITECTURE.md) | Web-first architecture, .NET 10 solution shape, simulation boundaries, rendering approach, persistence, content pipeline, AI, testing, and multiplayer path. |
| [PRODUCTION_ROADMAP.md](PRODUCTION_ROADMAP.md) | Vertical slice, MVP, staged production plan, team disciplines, milestones, risks, and definition of done. |
| [WEBSITE_AND_GAME_UX.md](WEBSITE_AND_GAME_UX.md) | How the cinematic marketing site, launcher, onboarding, HUD, menus, navigation, and in-game visual language form one coherent product. |

## Implementation starter pack

Start implementation with these documents rather than attempting the entire production roadmap at once:

| Document | Purpose |
|---|---|
| [27_IMPLEMENTATION_START.md](27_IMPLEMENTATION_START.md) | First executable milestone, project responsibilities, engineering conventions, seed content, use cases, persistence baseline, testing gates, CI, and implementation order. |
| [28_VERTICAL_SLICE_BACKLOG.md](28_VERTICAL_SLICE_BACKLOG.md) | Ordered epics and stories with acceptance criteria and milestone gates for the deterministic first playable. |
| [Architecture/README.md](Architecture/README.md) | ADR process, required initial decisions, and a reusable decision-record template. |

The recommended starting sequence is:

1. Accept or revise the implementation milestone.
2. Create the documented solution projects and CI pipeline.
3. Decide numeric units, deterministic randomness, simulation time, content versioning, and save boundaries through ADRs.
4. Build validated seed content.
5. Complete the headless trade journey before coupling gameplay to real-time rendering.
6. Add the minimal Blazor UI and then the renderer spike behind a stable boundary.

## Canon sources

The production documents are derived from the established project canon:

- `HISTORY.md` and `HISTORY_COLONIAL_WARS.md`
- `GALACTIC_ATLAS.md`
- `FACTIONS.md`
- `TECHNOLOGY.md`
- `CULTURE.md`
- `HISTORICAL_FIGURES.md`

These sources define what is true in the setting. Production documents define **how the player experiences those truths**.

## Canon hierarchy

When two documents appear to disagree, use this order:

1. Master history and explicit fixed canon.
2. Technology rules and limits.
3. Galactic atlas and faction definitions.
4. Culture and historical figures.
5. Production documentation in this directory.
6. Procedural or save-specific content.

Gameplay may conceal, distort, or contradict information through unreliable NPCs, propaganda, rumors, forged records, and incomplete archives. The underlying simulation must still respect canonical physics and institutional rules.

## Product principle

> Frontier 10052 is not a theme park in space. It is a living frontier where distance, delay, trust, debt, maintenance, law, and human relationships turn every journey into a decision.

The AAA goal is not merely visual fidelity. It is the combination of:

- cinematic presentation;
- deep systemic interaction;
- distinctive authorship;
- believable world simulation;
- high-quality sound and motion;
- accessible interfaces;
- stable performance;
- memorable characters and consequences.

## Immediate implementation priority

The first production target is a polished **vertical slice** beginning in the Solar system and ending with the player completing a consequential trade-and-intelligence run between two nearby systems. It must demonstrate:

1. Website-to-game transition.
2. Ship interior and exterior presentation.
3. Undocking, orbital departure, warp preparation, transit, arrival, approach, and docking.
4. A market whose prices react to delayed information.
5. Crew members with roles, traits, loyalty, and conflict.
6. A faction decision with a visible consequence.
7. One dangerous encounter solvable through flight, negotiation, deception, or combat.
8. Persistent ship wear, debt, reputation, and world state.

Everything else should be judged by whether it strengthens this slice or distracts from it.
