# Design QA — Visual 1 landing implementation

## Comparison target

- Source visual truth: `doc/design/landing-visual-01-source.png`
- Browser-rendered implementation: `doc/design/landing-implementation-1440x1024.png`
- Responsive evidence: `doc/design/landing-implementation-390x844.png`
- Launcher evidence: `doc/design/launcher-implementation-1440x1024.png`
- Viewport: 1440 × 1024 CSS pixels at 1× device density. The 1487 × 1058 source was normalized to 1440 × 1024 for comparison.
- State: landing route `/`, final Andromeda reveal, returning visitor, dark theme, sound off, neutral pointer position.

## Comparison evidence

- Full view: `doc/design/landing-comparison.png` (source left, implementation right).
- Focused hero typography and CTA region: `doc/design/landing-comparison-focused.png` (source left, implementation right).
- The focused comparison was required because the display-letter spacing, CTA height, label spacing, and lower-left alignment were too small to judge reliably in the full-width pair.

## Findings

- No actionable P0, P1, or P2 differences remain.
- Fonts and typography: the implementation preserves the source's thin uppercase display treatment, expanded tracking, compact hierarchy, and single-line desktop wordmark. Space Grotesk/Helvetica fallbacks are optically close rather than a pixel-identical reconstruction of the generated concept lettering; this is acceptable P3 drift.
- Spacing and layout rhythm: galaxy placement, lower-left wordmark, menu position, CTA baseline, first-action frame, and negative-space balance align with the target. Mobile converts the hero actions into a readable vertical stack without overlap or clipping.
- Colors and visual tokens: black, near-white, muted gray, and ice-blue tokens follow the source. The implementation adds no gradients, ornamental card treatment, or rounded UI that would change the direction.
- Image quality and asset fidelity: the implementation uses generated raster plates derived from the selected visual, with the same Milky Way/Andromeda composition. The cleaned plate is intentionally a little darker and less star-dense than the source after removing embedded mock text; the subject, crop, sharpness, and art direction remain faithful. No CSS art, inline SVG, emoji, or placeholder imagery replaces visible target assets.
- Copy and content: `FRONTIER 10052`, `PLAY NOW`, `WATCH TRAILER`, `THE UNIVERSE`, and `MENU` match the selected target. Supplemental product copy appears only below the hero and in requested routes.
- Icons: the target does not use visible icons in the landing hero, and none were introduced.
- Accessibility and responsive behavior: semantic links/buttons, a skip link, labeled dialog, keyboard focus, reduced-motion handling, and mobile tap targets are present. The programmatically focused heading no longer receives an unintended hero outline.

## Comparison history

1. Initial full-view and focused comparison
   - Earlier findings: [P2] the hero heading was oversized and received a visible focus outline; [P2] coordinate/replay/scroll/sound controls made the final state busier than Visual 1; [P2] the primary CTA was too short and the title/CTA rhythm sat too low.
   - Fixes: removed final-frame supplemental HUD copy, hid intro-only controls after reveal, removed heading focus decoration, tightened the title size while increasing tracking, and matched the source CTA height and vertical spacing.
   - Post-fix evidence: `doc/design/landing-comparison.png` and `doc/design/landing-comparison-focused.png`.

2. Refined comparison and interaction pass
   - Earlier findings: [P2] hovering the transparent full-width header exposed an unintended horizontal divider; [P2] the trailer action did not reserve enough horizontal measure, pulling `THE UNIVERSE` left; [P2] the new-commander action updated only after input blur.
   - Fixes: limited the header treatment to the visible action group, matched the trailer action measure, and changed callsign binding to update on input.
   - Post-fix evidence: the current full/focused comparisons plus browser interaction assertions listed below.

## Browser verification

- First-visit cinematic entered the playing state and `Skip intro` reached the final reveal.
- Main menu opened and exposed all 11 requested navigation links, then closed.
- Concept trailer opened, paused, resumed state correctly, and closed.
- Routes verified: Universe, Factions, Star Map, Ships, Colonies, Crew, Lore, Roadmap, and Community.
- Launcher verified: Continue → Prepare departure → Command accepted; New Commander disabled with an empty callsign and enabled while typing; Settings applied reduced-motion state.
- Desktop and 390 × 844 mobile landing screenshots were captured.
- Browser console errors: 0.
- Unhandled page errors: 0.
- HTTP responses ≥ 400: 0.

## Implementation checklist

- [x] Match Visual 1's final hero composition.
- [x] Preserve the cinematic first-visit transition and reduced-motion alternative.
- [x] Make the menu, trailer, primary routes, launcher controls, and input states functional.
- [x] Verify desktop/mobile rendering and browser runtime signals.
- [x] Recompare the corrected implementation against the same source and state.

## Follow-up polish

- P3: a licensed custom display face could narrow the remaining optical difference from the generated concept lettering if a brand font is selected later.

## First-contract operations extension

### Evidence

- Desktop station workspace: `doc/design/operations-implementation-1440x1024.png` at 1440 × 1024.
- Mobile authorized handoff: `doc/design/operations-implementation-390x844.png` at 390 × 844.
- Visual source: the selected Visual 1 landing plate plus `doc/design/launcher-implementation-1440x1024.png`.
- App UI classifier: task-focused workspace. The implementation uses a calm three-zone hierarchy—progress, primary work, persistent manifest—rather than a dashboard-card mosaic.

### Design review findings and fixes

- [P1 fixed] The launcher’s visually hidden global header remained in keyboard order on `/play` and `/operations`. Launcher routes now remove that duplicate header from layout and accessibility navigation.
- [P1 fixed] Authorizing departure on a long mobile page preserved the old scroll position, which could open the cinematic handoff below its headline. Successful authorization now resets the viewport to the top after the immutable manifest snapshot renders.
- [P2 fixed] The skip link could appear after mouse-driven focus recovery when the authorization button left the document. It now reveals only for keyboard-visible focus.
- [P2 fixed] Launcher utility, wordmark, compact menu, close, and overwrite-confirmation hit areas were smaller than the 44-pixel interaction target. Their invisible interaction boxes were enlarged without changing the launcher composition.
- No unresolved P0, P1, or P2 findings remain. The workspace preserves Visual 1’s near-black plate, ice-blue signal color, restrained aerospace typography, square geometry, fine rules, and full-bleed station imagery. It introduces no new gradient identity, ornamental icons, bubbly cards, or generic dashboard chrome.

### Interaction and responsive verification

- Desktop New Commander flow completed: callsign → briefing → contract → report assessment → speculative coolant purchase → manifest analysis → departure authorization → reload.
- Mobile 390 × 844 flow completed independently with optional trade skipped and drive inspection selected; the authorized manifest reloaded at command sequence 5.
- Continue resumed the browser’s real Earth journey and displayed its persisted command sequence.
- New Commander remained disabled for an existing journey until the explicit permanent-overwrite checkbox was selected.
- Initial departure review exposed four blockers beside the disabled authorization control; optional trading was not a blocker.
- Keyboard Tab focus produced a 2-pixel ice-blue visible outline; reduced-motion mode left zero long-running animations.
- Mobile document width equaled viewport width (390 pixels) at initial and authorized states.
- Browser console errors: 0. Unhandled page errors: 0. HTTP responses ≥ 400: 0.

final result: passed

## Second-contract Mars turnaround extension

### Evidence

- Desktop Pluto departure and encounter: `doc/design/second-contract-pluto-departure-1536x1000.png` and `doc/design/second-contract-pluto-encounter-1536x1000.png` at 1536 × 1000.
- Mobile Mars offer decision, Ceres encounter, and Ceres settlement: `doc/design/second-contract-turnaround-390x844.png`, `doc/design/second-contract-ceres-encounter-390x844.png`, and `doc/design/second-contract-ceres-complete-390x844.png` at 390 × 844.
- Both branches were played from a new commander through first-contract delivery, Mars service decisions, mutually exclusive contract selection, second-voyage encounter, destination arrival, cargo custody, and launcher resume.

### Design review findings and fixes

- [P2 fixed] Destination settlement exposed internal station IDs in player-facing outcome text. Journey presentation now resolves every authored station reference through the content pack before rendering.
- [P2 fixed] Departure review exposed persisted enum identifiers such as `IlyaFieldService` and `TurnaroundWatches`. Lien, repair, rest, and service-history values now use explicit player-facing labels.
- [P2 fixed] Repeated generic `Choose` and `Select contract` controls produced ambiguous accessible names. Action and offer controls now include their specific service or contract title in the accessible label.
- [P2 fixed] The launcher described every in-flight save as the Earth–Mars transfer. It now derives the active origin and destination from the authoritative contract and route state.
- No unresolved P0, P1, or P2 findings remain. The turnaround, travel, and destination workspaces preserve the existing full-bleed black plate, restrained display typography, square geometry, fine-rule progress rail, sparse signal color, and persistent manifest hierarchy.

### Runtime, accessibility, and responsive verification

- Pluto desktop covered lien service, certified repair, full layover, the humanitarian response, reload during the encounter, on-time TCA settlement, and phase-aware launcher continuation.
- Ceres mobile covered lien deferral, Ilya field service, turnaround watches, reload during turnaround and the debris encounter, Mara's evasive response, gray-market settlement, legal exposure, and transformed Pluto history.
- Keyboard Tab focus produced the intended 2-pixel ice-blue outline. Disabled actions retained adjacent explanations for money, time, preparation, capacity, fuel, and route blockers.
- Mobile document and body width remained exactly 390 pixels through turnaround, encounter, and settlement, with no horizontal clipping.
- A browser context with `prefers-reduced-motion: reduce` reported the preference active, zero running animations, and zero long-running animations at 390 × 844.
- Fresh browser verification after the final presentation fixes reported 0 console errors, 0 unhandled page errors, and 0 HTTP responses ≥ 400.
- Release build completed with 0 warnings and 0 errors. All 41 tests passed locally, in Podman, and in Docker. Both runtime images returned the rendered application from `http://localhost:8080/`.

final result: passed

## Earth–Mars travel and settlement extension

### Evidence and findings

- Live desktop journey verified at 1536 × 1000; live reduced-motion mobile journey verified at 390 × 844.
- Both runs completed New Commander → Earth preparation → authorized manifest → undock → deterministic encounter → encounter reload → Mars approach → docking → explicit coolant sale → medical delivery → launcher Continue.
- The selected seed and committed manifest produced the Continuity patrol inspection on both runs. The encounter title and available choices were identical before and after browser reload.
- Desktop and mobile completion renders preserved the launcher/operations language: full-bleed cinematic plate, near-black aerospace workspace, square geometry, restrained ice-blue signals, fine rules, and no dashboard-card or ornamental visual identity.
- [P1 fixed] Mars settlement initially received a literal parameter name instead of the browser player key, preventing sale and delivery commands from locating the authoritative save. The real key is now passed through the typed component parameter.
- [P2 fixed] Completed delivery removed the medical line from the active manifest but the proof panel described it as missing. Completion now reports accepted custody and intentional manifest removal.
- [P2 fixed] Completion focus now moves to the outcome heading after the delivery control leaves the document, preserving keyboard position and preventing unrelated focus recovery.
- [P2 fixed] Two-response encounters initially reserved a third empty desktop column. Response cards now auto-fit the authored option count while three-response pirate encounters retain three equal columns.
- No unresolved P0, P1, or P2 findings remain.

### Runtime and responsive verification

- Desktop document width equaled its 1536-pixel viewport; mobile document width equaled its 390-pixel viewport.
- Mobile full content remained readable as a single vertical settlement flow without horizontal clipping.
- Reduced motion disabled long-running travel and signal animation while leaving all authoritative commands available.
- Final persisted state reloaded as `Completed` at command sequence 13, and launcher Continue returned to Mars operations.
- Browser console errors: 0. Unhandled page errors: 0. HTTP responses ≥ 400: 0.

final result: passed
