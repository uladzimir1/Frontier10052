# Frontier 10052 — Gameplay Systems

## 1. Core loop

1. Gather market, political, navigational, and personal intelligence.
2. Select a destination, contract, cargo, passengers, and risk profile.
3. Prepare the ship: fuel, maintenance, crew assignments, permits, cover stories, and route.
4. Undock and travel through orbital space to the warp boundary.
5. Resolve travel events, crew developments, breakdowns, encounters, and changing priorities.
6. Arrive with information that may be current, stale, unique, or dangerous.
7. Trade, negotiate, deliver, investigate, repair, recruit, and build reputation.
8. Live with the consequences and choose the next run.

The loop must remain satisfying even when the player never fires a weapon.

## 2. Navigation and travel

Travel has three distinct layers:

- **Local flight:** docking, stations, moons, planets, belts, hazards, patrols, and tactical encounters.
- **Warp routing:** interstellar journeys with fuel decay, reliability, time, and information value.
- **Gate transit:** rare, scheduled, bureaucratic, expensive, and strategically transformative.

### Route planning inputs

- distance and drive rating;
- pinch supply and decay;
- gravity-well departure time;
- known hazards and Silent Zone detours;
- patrol coverage and law;
- repair and refueling access;
- market age and expected competition;
- crew fatigue and morale;
- warrants, permits, and faction standing;
- courier traffic that may outrun the player.

## 3. Trade and economy

Markets should be simulated from production, consumption, storage, transport capacity, disruption, policy, and information delay.

Important cargo categories include food, water, medicine, industrial feedstock, machine parts, luxury goods, cultural artifacts, data archives, biological materials, weapons, controlled substances, passengers, and migration contracts.

A price is never just a number. The interface should expose:

- price age;
- source;
- confidence;
- expected demand drivers;
- legality;
- storage requirements;
- spoilage or degradation;
- volume and mass;
- insurance implications;
- likely competitors.

## 4. Information economy

Information is a first-class inventory type.

Examples:

- market prices;
- station damage reports;
- strike notices;
- Gate queue changes;
- recalibration rumors;
- military movements;
- warrants;
- migration policy changes;
- salvage coordinates;
- medical outbreaks;
- election or succession results;
- proof that discredits another report.

Each information item has provenance, timestamp, confidence, sensitivity, geographic relevance, and decay. It can be sold, withheld, copied, corroborated, poisoned, leaked, or used privately.

## 5. Ship systems

The ship is divided into interconnected systems:

- hull and pressure;
- metric drive and pinch lattice;
- conventional engines and maneuvering thrusters;
- power generation and distribution;
- thermal control;
- sensors and communications;
- navigation;
- cargo handling;
- life support;
- medical bay;
- crew quarters;
- weapons and countermeasures;
- shields or defensive fields where canon permits;
- docking and airlock systems;
- computing and legal identity modules.

Damage propagates through dependencies. A damaged radiator may force reduced power, which limits sensors, which makes an ambush more likely. Repairs consume parts, time, expertise, money, and sometimes favors.

## 6. Crew

Crew roles initially include pilot, engineer, security, and medic, later expanding to navigator, broker, steward, scientist, and specialist roles.

Each crew member has:

- professional skill;
- traits such as steady, pious, greedy, loyal, ambitious, fearful, or secretive;
- personal history;
- beliefs and faction sympathies;
- relationships with other crew;
- needs, boundaries, and ambitions;
- injury, stress, fatigue, and morale;
- trust in the captain;
- evolving memories of player decisions.

Crew should disagree for intelligible reasons. Orders can be obeyed, questioned, delayed, sabotaged, or refused depending on loyalty, law, fear, and values.

## 7. Encounters and combat

Combat is real-time, tactical, and dangerous. It should emphasize positioning, signatures, subsystem damage, escape windows, surrender, boarding risk, and consequences.

Possible approaches:

- avoid detection;
- transmit credentials;
- negotiate;
- bribe;
- bluff;
- call a contact;
- dump cargo;
- disable pursuit;
- flee toward patrol coverage;
- fight;
- surrender;
- board or resist boarding.

Weapons should not turn the game into frictionless power fantasy. Ammunition, heat, repair cost, law, injuries, insurance, evidence, and reputation persist after victory.

## 8. Law, crime, and reputation

Law is jurisdictional and delayed. A crime may be known locally before a warrant physically reaches another system.

Track separately:

- legal status by polity;
- faction reputation;
- personal relationships;
- commercial trust;
- insurance risk;
- criminal credibility;
- public notoriety.

Illegal play should be viable but demanding. Smuggling requires route knowledge, concealment, paperwork, contacts, timing, and acceptable crew. Prison is a playable consequence with social and narrative opportunities, including access to people who can enable an illegal return from Andromeda.

## 9. Exploration and salvage

Exploration focuses on incomplete knowledge rather than map completion.

Sites include:

- Colonial Wars wrecks;
- lost fleet traces;
- abandoned stations;
- failed colonies;
- Builder fragments;
- Silent Margin anomalies;
- unregistered settlements;
- derelict couriers containing time-sensitive news;
- hidden migration routes.

Salvage requires surveying, legal claims, hazard management, physical recovery, and deciding what to reveal. Discoveries should create economic and political consequences.

## 10. Progression

Progression is horizontal before vertical.

The player gains:

- better reliability;
- more route options;
- trusted contacts;
- stronger crew cohesion;
- legal privileges;
- specialized ship configurations;
- information networks;
- access to restricted markets;
- social leverage;
- knowledge.

The player may become powerful, but never exempt from distance, law, maintenance, trust, or scarcity.

## 11. Dynamic events

Regional events should be systemic and multi-stage:

- condenser outage;
- Gate recalibration;
- epidemic;
- labor strike;
- blockade;
- pirate campaign;
- migration surge;
- station accident;
- faction succession;
- insurance collapse;
- discovery of a wreck or Builder site;
- false rumor deliberately introduced into the market.

Events propagate at the speed of ships. Different systems can temporarily act on different versions of reality.

## 12. Saving and persistence

Persist:

- market stocks and disruptions;
- information movement;
- crew memories and relationships;
- ship wear and modifications;
- faction state;
- legal state;
- discovered routes and sites;
- unresolved promises, debts, favors, and contracts;
- important NPC movement;
- player-generated historical consequences.

The save should feel like a unique history, not merely a collection of completed quests.