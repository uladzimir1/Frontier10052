# Frontier 10052 — Save System

## Philosophy

A save represents a unique history, not merely a list of completed quests. It must preserve the movement of goods, information, people, obligations, and consequences.

## Persisted domains

- world time, routes, discoveries, and hazards;
- market inventories, production, demand, and disruptions;
- information items, provenance, confidence, and propagation;
- ship configuration, wear, damage, repairs, debt, and identity;
- crew state, memories, relationships, injuries, and arcs;
- factions, law, warrants, reputation, insurance, and notoriety;
- contracts, promises, favors, evidence, and unresolved disputes;
- important NPC movement and player-created historical events;
- procedural seeds and resolved generated content.

## Storage strategy

Use versioned snapshots plus compact domain events where replay, diagnostics, or narrative history adds value. Simulation rules remain independent from serialization models.

## Reliability

Atomic writes, checksums, rolling backups, explicit save slots, autosave rotation, corruption detection, recovery mode, and safe shutdown. Never overwrite the only known-good save during migration.

## Versioning

Every save has game version, schema version, content manifest, enabled mods, generator versions, and migration history. Migrations are automated, testable, idempotent where practical, and able to fail safely.

## Cloud synchronization

Use immutable revisions and explicit conflict handling. Present time, device, playtime, version, and summary before choosing local or cloud state. Preserve both revisions when uncertain.

## Modes

Normal profiles, optional restricted-save or ironman modes, and developer/debug saves. Difficulty and accessibility settings are not treated as cheating.

## Testing

Golden saves, migration chains, interrupted writes, missing content, changed mods, large simulations, clock edge cases, and deterministic replay tests.
