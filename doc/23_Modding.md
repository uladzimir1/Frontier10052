# Frontier 10052 — Modding

## Goals

Modding extends the life of the single-player game, supports creative communities, and improves internal content production. It must protect save integrity, security, accessibility, and multiplayer compatibility.

## Supported content

Cargo, markets, contracts, events, dialogue, portraits, audio, UI themes, star systems, locations, ship hulls and modules, crew templates, factions, rulesets, and carefully sandboxed scripts.

## Data format

Use documented schemas with stable namespaced IDs, package manifests, semantic versions, dependencies, load order, localization keys, licenses, and optional signatures. Validate all references before launch.

## Scripting

Prefer declarative conditions, effects, state machines, and sandboxed scripting. Arbitrary native code is not a default requirement. Expose a versioned API with capability restrictions, execution budgets, and diagnostic messages.

## Compatibility

Save files record the exact mod manifest. Removing required content produces a clear compatibility report and recovery options. Mods may provide migrations. Base-game IDs cannot be silently overwritten without explicit replacement rules.

## Tools

Schema documentation, examples, validators, content preview, localization checks, quest graph inspection, market simulation tests, packaging, and error logs. Internal tools should use the same public pipeline whenever feasible.

## Security

No unrestricted filesystem, process, credential, or network access. Sanitize archives and paths. Clearly distinguish trusted, signed, and unverified packages.

## Multiplayer

Online sessions may disallow mods, permit cosmetic-only packages, or require identical signed manifests. Single-player mod freedom remains the priority.

## Distribution

Support local packages first. Community repositories or workshop integration require moderation, reporting, licensing, dependency resolution, and takedown processes.
