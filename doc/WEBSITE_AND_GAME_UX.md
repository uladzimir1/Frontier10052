# Frontier 10052 — Website and Game UX

## 1. One product, not two visual identities

The public website, account area, launcher, loading flow, main menu, and game interface should feel like stages of the same journey.

The website introduces scale and desire. The launcher establishes identity and readiness. The game UI becomes the ship's operational language.

The transition should feel like entering the vessel rather than opening a separate application.

## 2. Website goals

The marketing site must communicate within seconds:

- this is a serious cinematic science-fiction game;
- the player commands an aging trading ship;
- humanity is old, divided, and still expanding;
- Andromeda is reachable but not freely reversible;
- trade, crew, information, exploration, and consequence are central;
- the project has a playable product direction, not only concept art.

### Suggested page structure

1. Cinematic hero with ship, planet, traffic, and restrained motion.
2. “The year is 10052” world premise.
3. Core player fantasy: captain, trader, courier, survivor.
4. Interactive galaxy or route preview.
5. Ship and crew section.
6. Factions and regions.
7. Travel sequence reel.
8. Development status and media.
9. Start game / wishlist / follow call to action.

## 3. Website-to-game transition

Recommended flow:

1. User selects **Start Game**.
2. The hero camera tracks toward the player's ship or a station window.
3. Marketing navigation fades into a minimal launch overlay.
4. Account, save, graphics capability, and asset readiness are checked.
5. The same scene resolves into the main menu or ship interior.
6. Music and ambient sound continue without an obvious reset.

Fallback devices may use a shorter pre-rendered or 2D transition while preserving the composition and sound.

## 4. Main menu

The main menu should be spatially grounded in the world: captain's cabin, bridge display, station observation deck, or ship exterior—not an unrelated abstract screen.

Primary actions:

- Continue;
- New Journey;
- Load Journey;
- Crew / Ship preview;
- Settings;
- Accessibility;
- Codex;
- Credits;
- Exit session.

The menu should show current ship condition, location, date, outstanding danger, and last known objective without overwhelming the player.

## 5. Information architecture

The major in-game workspaces are:

- **Bridge:** flight, destination, alerts, local contacts.
- **Navigation:** system map, route planning, fuel, time, risk.
- **Market:** cargo, prices, history, source age, confidence, legality.
- **Contracts:** obligations, deadlines, collateral, penalties, provenance.
- **Crew:** roles, health, stress, loyalty, conflicts, assignments.
- **Ship:** systems, damage, power, heat, maintenance, modules.
- **Comms and Intelligence:** messages, rumors, reports, warrants, source chains.
- **Standing:** factions, law, insurance, debt, favors, notoriety.
- **Codex:** player-known facts with uncertainty and sources.

These should share patterns but retain different visual rhythms. The market is dense and comparative; the bridge is glanceable; crew is personal; navigation is spatial.

## 6. HUD principles

The HUD should display only information required for the current activity.

### Local flight HUD

- velocity and vector;
- target and distance;
- thrust and maneuver state;
- hull, power, heat, and critical subsystem alerts;
- signature and detection state;
- docking or route guidance;
- concise crew callouts.

### Encounter HUD

Add weapons, countermeasures, target subsystems, communication choices, surrender status, and escape conditions only when relevant.

### Accessibility

Never rely on color alone. Support scalable text, high contrast, reduced motion, subtitle customization, remapping, hold/toggle options, aim and flight assistance, camera shake controls, and alternatives for time-sensitive interactions.

## 7. Visual language

The visual identity should combine:

- ancient institutional grandeur;
- maintained but worn industrial hardware;
- precise navigation and contract typography;
- warm human objects inside cold machinery;
- faction-specific materials and motion;
- restrained premium interfaces inspired by aerospace, finance, maritime operations, and archival systems.

Avoid generic neon cyberpunk, excessive holograms, and floating UI without physical or operational logic.

## 8. Faction differentiation

- **Terran Continuity Authority:** restored forms, ceremonial spacing, archival typography, controlled colors, visible history.
- **Sirius Corporate Compact:** efficient density, financial dashboards, contractual language, premium materials, surveillance cues.
- **Frontier Freeholds:** modular hardware, local repairs, practical labels, mixed standards, personal marks.
- **Kuiper Syndicates:** discreet interfaces, layered permissions, coded provenance, plausible deniability.
- **Andromedan Federation:** calm, accessible, warm, highly polished, subtly curated, almost no visual noise.
- **Gate Authority:** neutral, monumental, exact, machine-audited, unmistakably non-negotiable.

## 9. Cinematic travel grammar

Travel should use a reusable but varied cinematic system.

Required sequences include:

- undocking;
- leaving planetary orbit;
- passing major stations;
- accelerating toward another planet;
- approaching a moon;
- entering an asteroid belt;
- flying through debris;
- escaping pirates;
- police pursuit;
- docking;
- Gate activation;
- interstellar jump;
- arrival in a new system;
- emergency landing;
- damaged ship drifting;
- entering Andromeda through a major Gate.

Camera families:

- exterior chase;
- distant cinematic;
- cockpit;
- hull close-up;
- engine-focused rear;
- side tracking;
- station observation;
- planetary flyby;
- orbital rotation;
- wide establishing shot.

Effects should emphasize realistic engine glow, maneuvering thrusters, hull reflections, dust, debris, planetary light, restrained motion blur, subtle camera shake, atmospheric effects, weapon fire, shield impacts, engine damage, jump distortion, and mechanical docking movement.

Sequences must remain skippable after first viewing and should adapt to ship condition, location, urgency, and faction environment.

## 10. Onboarding

Teach through the first contract rather than through a detached tutorial.

The opening should introduce:

1. moving through the ship or operational interface;
2. talking to crew;
3. inspecting a contract;
4. understanding price age and source confidence;
5. buying cargo;
6. assigning crew;
7. undocking;
8. responding to one complication;
9. arriving before or after a competing courier;
10. seeing how the world changed during travel.

Every lesson should solve an immediate problem.

## 11. Crew presentation

Crew portraits and 3D representations should be charismatic and narratively legible. Their profession and history may be visible through clothing, scars, religious objects, posture, equipment, and environment, but avoid reducing them to one trait.

Portraits should support states such as healthy, injured, exhausted, angry, frightened, wanted, undercover, promoted, or changed by cybernetics.

The crew screen should prioritize relationships and current concerns over raw statistics.

## 12. Market UX

For each price, show:

- latest known value;
- observation time and place;
- source;
- confidence;
- likely reason for movement;
- legal and storage constraints;
- route cost;
- estimated profit range rather than false certainty.

A player should be able to compare “high theoretical profit from old data” against “lower but trustworthy profit from recent corroborated data.”

## 13. Failure UX

Failure should explain consequences without flattening them into a game-over screen.

Examples:

- debt restructuring;
- loss of cargo;
- crew injury or departure;
- arrest and prison play;
- damaged ship and emergency contracts;
- damaged reputation;
- forced sale of a module;
- rescue that creates an obligation.

Permanent death may exist as an optional or specific mode, but most failure should generate new play.

## 14. UX definition of done

A feature is not complete until:

- its purpose is understandable without external documentation;
- it works with mouse, keyboard, and controller where applicable;
- it supports accessibility settings;
- loading, empty, error, and offline/fallback states exist;
- information provenance is visible where relevant;
- common actions are fast;
- expert detail is available without cluttering the first view;
- it uses the same visual and interaction language as the rest of the product.