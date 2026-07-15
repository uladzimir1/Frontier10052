# Frontier 10052 — Procedural Generation

## Purpose

Procedural generation makes the world larger, variable, and reactive without replacing authored identity. It creates contracts, shortages, minor NPCs, rumors, wreck details, relationships, local events, traffic, and regional variations.

## Canon constraints

Generated content must respect star-system properties, faction authority, technology limits, history, economy state, travel time, law, culture, and player history. A validator rejects impossible routes, anachronisms, invalid jurisdictions, unsupported cargo, and contradictory narrative facts.

## Seeds and reproducibility

World generation uses explicit seeds and versioned generators. Important outputs store their resolved data in the save so later generator changes do not rewrite history.

## Generation layers

- economy events from supply, demand, disruption, and transport;
- contracts from actors, needs, routes, and risks;
- rumors from facts, distortions, incentives, and source chains;
- NPCs from regional culture, role, faction, history, and relationships;
- salvage sites from historical conflicts, location hazards, and legal claims;
- traffic from population, production, policy, and current events.

## Authored/procedural blend

Authored hubs and characters provide emotional and cultural anchors. Procedural systems create context around them. Generated elements can become persistent history and trigger authored callbacks.

## Anti-wiki principle

Not every mystery receives a definitive answer. Some wrecks, descendants, local customs, minor powers, and disputed events vary by save. The codex stores what the player discovered, including provenance and uncertainty.

## MVP

Generate validated freight contracts, market disruptions, rumors with source and age, minor traders, and one salvage site. All outputs must be reproducible from seed and persist after discovery.
