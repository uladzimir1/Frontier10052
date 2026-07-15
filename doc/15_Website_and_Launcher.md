# Frontier 10052 — Website and Launcher

## Goals

The public website and launcher form one coherent cinematic journey. They introduce the setting, communicate the game pillars, showcase the ship and regions, capture interest, manage accounts and saves, expose settings, and launch the game with minimal friction.

## Current foundation

The repository contains a .NET 10 Blazor Web App with a cinematic landing page, animated starfield, orbital traffic, responsive Apple-inspired interface, and game start route.

## Information architecture

- Home and core promise;
- world, factions, ships, crew, and development pages;
- media, news, roadmap, FAQ, accessibility, and support;
- account, profile, cloud saves, settings, diagnostics, and launch;
- legal, privacy, cookies, telemetry, and community rules.

## Launcher flow

Detect compatibility → authenticate optionally → select profile/save → verify content version → show update or migration notes → configure accessibility and graphics → launch → recover gracefully from failure.

## Performance

Prioritize fast first paint, optimized media, progressive loading, caching, reduced JavaScript, responsive images, and graceful fallback. Animated backgrounds must pause when hidden and honor reduced-motion settings.

## SEO and sharing

Use semantic HTML, canonical metadata, structured data, localized titles and descriptions, social preview images, meaningful URLs, sitemap, robots policy, and accessible text alternatives.

## Operations

Support maintenance notices, release channels, rollback, version compatibility, save migration warnings, status reporting, crash diagnostics, and consent-based telemetry.

## MVP

Polished home page, documentation/news navigation, start route, local profile selection, settings, reduced-motion mode, responsive layout, and clear error states.
