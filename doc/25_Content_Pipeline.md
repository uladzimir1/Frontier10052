# Frontier 10052 — Content Pipeline

## Principles

Content is data-driven, versioned, reviewable, localizable, testable, and traceable to canon. Every entity uses a stable namespaced ID. Production tools should make invalid content difficult to create and easy to diagnose.

## Source formats

Markdown for design and long-form lore; JSON or YAML for structured content; dedicated authoring tools for quest graphs, dialogue, star systems, economy, and cinematic timelines. Build output is normalized into versioned runtime packages.

## Core entities

StarSystem, Location, Faction, Commodity, Market, InformationItem, ShipHull, ShipModule, CrewCharacter, Contract, WorldEvent, Encounter, Dialogue, NarrativeFact, AudioCue, CinematicSequence, and LocalizationEntry.

## Workflow

Brief → canonical research → authoring → automated validation → simulation preview → narrative/design review → localization → asset binding → integration build → playtest → revision → release manifest.

## Validation

Check duplicate or missing IDs, broken references, missing localization, invalid factions or jurisdictions, impossible routes, technology violations, quest reachability, deadline feasibility, economy bounds, unsupported assets, accessibility metadata, performance budgets, and save compatibility.

## Canon control

Canon changes require explicit review because they affect writing, economy, travel, art, audio, quests, and code. Record decisions in version control and update Appendix canon principles when rules change.

## Localization

All player-facing text uses localization keys, grammatical context, placeholders, plural rules, speaker metadata, and length guidance. Avoid concatenating sentence fragments.

## Assets

Define naming, source files, import settings, compression, LOD, collision, audio loudness, licenses, ownership, review status, and platform budgets.

## Automation

CI builds content packages, emits reports, creates searchable indexes, compares manifests, detects unintended changes, and runs smoke simulations before merging.
